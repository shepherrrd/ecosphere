using System.Collections.Concurrent;
using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosphere.Infrastructure.Infrastructure.SignalR;

[Authorize]
public class CallHub : Hub
{
    private readonly EcosphereDbContext _context;
    private readonly ILogger<CallHub> _logger;

    // In-memory tracking of connected users and their devices could change to redis if it grows big
    
    private static readonly ConcurrentDictionary<long, HashSet<string>> UserConnections = new();

    // Key: ConnectionId, Value: DeviceInfo
    private static readonly ConcurrentDictionary<string, DeviceInfo> ConnectionDevices = new();

    // Active calls tracking
    private static readonly ConcurrentDictionary<string, CallSessionInfo> ActiveCalls = new();

    public CallHub(EcosphereDbContext context, ILogger<CallHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Connection Management

    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = Context.User?.Identity?.GetProfileId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("[CallHub] Connection attempt without valid user ID");
                Context.Abort();
                return;
            }

            var connectionId = Context.ConnectionId;
            _logger.LogInformation($"[CallHub] User {userId} connected with ConnectionId: {connectionId}");

            // Add to user connections
            UserConnections.AddOrUpdate(
                userId.Value,
                new HashSet<string> { connectionId },
                (key, existing) =>
                {
                    existing.Add(connectionId);
                    return existing;
                });

            // Get device info from query string 
            var deviceToken = Context.GetHttpContext()?.Request.Query["deviceToken"].ToString();
            var deviceName = Context.GetHttpContext()?.Request.Query["deviceName"].ToString() ?? "Unknown Device";

            // Store device info
            var deviceInfo = new DeviceInfo
            {
                UserId = userId.Value,
                ConnectionId = connectionId,
                DeviceToken = deviceToken,
                DeviceName = deviceName,
                ConnectedAt = DateTimeOffset.UtcNow
            };
            ConnectionDevices.TryAdd(connectionId, deviceInfo);

            // Update device in database
            if (!string.IsNullOrEmpty(deviceToken))
            {
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.DeviceToken == deviceToken);

                if (device != null)
                {
                    device.ConnectionId = connectionId;
                    device.IsActive = true;
                    device.LastActiveAt = DateTimeOffset.UtcNow;
                    device.TimeUpdated = DateTimeOffset.UtcNow;
                }
            }

            // Mark user as online
            var user = await _context.Users.FindAsync(userId.Value);
            if (user != null)
            {
                user.IsOnline = true;
                user.LastSeen = DateTimeOffset.UtcNow;
                user.TimeUpdated = DateTimeOffset.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Notify all user's other devices about new connection
            await NotifyUserDevices(userId.Value, "DeviceConnected", new
            {
                DeviceName = deviceName,
                ConnectionId = connectionId
            }, excludeConnectionId: connectionId);

            // Send ConnectionInitialized to the client 
            await Clients.Caller.SendAsync("ConnectionInitialized", connectionId);

            _logger.LogInformation($"[CallHub] Connection initialized for user {userId} with ID: {connectionId}");

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CallHub] Error in OnConnectedAsync");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var userId = Context.User?.Identity?.GetProfileId();
            var connectionId = Context.ConnectionId;

            if (userId.HasValue)
            {
                _logger.LogInformation($"[CallHub] User {userId} disconnected: {connectionId}");

                // Remove from user connections
                if (UserConnections.TryGetValue(userId.Value, out var connections))
                {
                    connections.Remove(connectionId);
                    if (connections.Count == 0)
                    {
                        UserConnections.TryRemove(userId.Value, out _);

                        // Mark user as offline if no more connections
                        var user = await _context.Users.FindAsync(userId.Value);
                        if (user != null)
                        {
                            user.IsOnline = false;
                            user.LastSeen = DateTimeOffset.UtcNow;
                            user.TimeUpdated = DateTimeOffset.UtcNow;
                        }
                    }
                }

                // Get device info before removing
                ConnectionDevices.TryRemove(connectionId, out var deviceInfo);

                // Update device in database
                if (deviceInfo != null && !string.IsNullOrEmpty(deviceInfo.DeviceToken))
                {
                    var device = await _context.Devices
                        .FirstOrDefaultAsync(d => d.DeviceToken == deviceInfo.DeviceToken);

                    if (device != null)
                    {
                        device.ConnectionId = null;
                        device.IsActive = false;
                        device.LastActiveAt = DateTimeOffset.UtcNow;
                        device.TimeUpdated = DateTimeOffset.UtcNow;
                    }
                }

                await _context.SaveChangesAsync();

                // End any active calls this user was in
                await HandleDisconnectedUserCalls(userId.Value, connectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CallHub] Error in OnDisconnectedAsync");
        }
    }

    #endregion

    #region Call Initiation

    /// <summary>
    /// Initiate a call to another user
    /// </summary>
    public async Task<string> InitiateCall(long targetUserId, string callType, bool isVideoCall)
    {
        try
        {
            var callerId = Context.User?.Identity?.GetProfileId();
            if (!callerId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return string.Empty;
            }

            if (callerId.Value == targetUserId)
            {
                await Clients.Caller.SendAsync("Error", "Cannot call yourself");
                return string.Empty;
            }

            _logger.LogInformation($"[CallHub] User {callerId} initiating call to user {targetUserId}");

            // Get caller and target user info
            var caller = await _context.Users.FindAsync(callerId.Value);
            var targetUser = await _context.Users.FindAsync(targetUserId);

            if (caller == null || targetUser == null)
            {
                await Clients.Caller.SendAsync("Error", "User not found");
                return string.Empty;
            }

            // Check if target user is online
            if (!UserConnections.ContainsKey(targetUserId))
            {
                await Clients.Caller.SendAsync("CallFailed", new
                {
                    Reason = "UserOffline",
                    Message = $"{targetUser.DisplayName ?? targetUser.UserName} is offline"
                });
                return string.Empty;
            }

            // Create call in database
            var callUuid = Guid.NewGuid().ToString();
            var call = new Call
            {
                CallUuid = callUuid,
                CallType = isVideoCall ? CallType.Video : CallType.Audio,
                Status = CallStatus.Initiating,
                InitiatorId = callerId.Value,
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            };

            await _context.Calls.AddAsync(call);
            await _context.SaveChangesAsync();

            // Add participants (
            var callerParticipant = new CallParticipant
            {
                CallId = call.Id,
                UserId = callerId.Value,
                IsInitiator = true,
                Status = CallParticipantStatus.Joined,
                JoinedAt = DateTimeOffset.UtcNow,
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            };

            var targetParticipant = new CallParticipant
            {
                CallId = call.Id,
                UserId = targetUserId,
                IsInitiator = false,
                Status = CallParticipantStatus.Ringing,
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            };

            await _context.CallParticipants.AddAsync(callerParticipant);
            await _context.CallParticipants.AddAsync(targetParticipant);
            await _context.SaveChangesAsync();

            // Track active call
            var callSession = new CallSessionInfo
            {
                CallUuid = callUuid,
                CallId = call.Id,
                InitiatorUserId = callerId.Value,
                InitiatorConnectionId = Context.ConnectionId,  // Store caller's device ConnectionId
                TargetUserId = targetUserId,
                TargetConnectionId = null,  // Not answered yet
                IsVideoCall = isVideoCall,
                Status = CallStatus.Ringing,
                StartedAt = DateTimeOffset.UtcNow
            };
            ActiveCalls.TryAdd(callUuid, callSession);

            // Update call status to Ringing
            call.Status = CallStatus.Ringing;
            await _context.SaveChangesAsync();

            // Notify caller that call is ringing
            await Clients.Caller.SendAsync("CallInitiated", new
            {
                CallUuid = callUuid,
                CallId = call.Id,
                TargetUser = new
                {
                    Id = targetUser.Id,
                    UserName = targetUser.UserName,
                    DisplayName = targetUser.DisplayName,
                    ProfileImageUrl = targetUser.ProfileImageUrl
                },
                IsVideoCall = isVideoCall
            });

            // Ring all target user's devices
            await RingUserDevices(targetUserId, new
            {
                CallUuid = callUuid,
                CallId = call.Id,
                Caller = new
                {
                    Id = caller.Id,
                    UserName = caller.UserName,
                    DisplayName = caller.DisplayName,
                    ProfileImageUrl = caller.ProfileImageUrl
                },
                IsVideoCall = isVideoCall
            });

            _logger.LogInformation($"[CallHub] Call {callUuid} initiated successfully");
            return callUuid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CallHub] Error initiating call");
            await Clients.Caller.SendAsync("Error", "Failed to initiate call");
            return string.Empty;
        }
    }

    #endregion

    #region Call Response

    /// <summary>
    /// Accept an incoming call
    /// </summary>
    public async Task AcceptCall(string callUuid)
    {
        try
        {
            var userId = Context.User?.Identity?.GetProfileId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            var connectionId = Context.ConnectionId;
            _logger.LogInformation($"[CallHub] User {userId} accepting call {callUuid}");

            if (!ActiveCalls.TryGetValue(callUuid, out var callSession))
            {
                await Clients.Caller.SendAsync("Error", "Call not found");
                return;
            }

            // Verify user is the target
            if (callSession.TargetUserId != userId.Value)
            {
                await Clients.Caller.SendAsync("Error", "You are not the target of this call");
                return;
            }

            // Get device info
            ConnectionDevices.TryGetValue(connectionId, out var deviceInfo);

            // Update call in database
            var call = await _context.Calls.FirstOrDefaultAsync(c => c.CallUuid == callUuid);
            if (call != null)
            {
                call.Status = CallStatus.Active;
                call.StartedAt = DateTimeOffset.UtcNow;
                call.TimeUpdated = DateTimeOffset.UtcNow;
            }

            // Update participant
            var participant = await _context.CallParticipants
                .FirstOrDefaultAsync(cp => cp.CallId == call!.Id && cp.UserId == userId.Value);

            if (participant != null)
            {
                participant.Status = CallParticipantStatus.Joined;
                participant.JoinedAt = DateTimeOffset.UtcNow;
                participant.TimeUpdated = DateTimeOffset.UtcNow;

                // Set which device answered
                if (deviceInfo != null && !string.IsNullOrEmpty(deviceInfo.DeviceToken))
                {
                    var device = await _context.Devices
                        .FirstOrDefaultAsync(d => d.DeviceToken == deviceInfo.DeviceToken);
                    if (device != null)
                    {
                        participant.DeviceId = device.Id;
                    }
                }
            }

            await _context.SaveChangesAsync();

            callSession.Status = CallStatus.Active;
            callSession.TargetConnectionId = connectionId; 

            await NotifyUserDevices(callSession.InitiatorUserId, "CallAccepted", new
            {
                CallUuid = callUuid,
                AnsweredBy = new
                {
                    UserId = userId.Value,
                    DeviceName = deviceInfo?.DeviceName,
                    ConnectionId = connectionId
                }
            });

            // Notify this device (answering device) that call is connected
            await Clients.Caller.SendAsync("CallConnected", new
            {
                CallUuid = callUuid,
                CallId = call?.Id
            });

            // Stop ringing on all other devices of the target user
            await NotifyUserDevices(userId.Value, "CallAnsweredElsewhere", new
            {
                CallUuid = callUuid,
                AnsweredDeviceName = deviceInfo?.DeviceName
            }, excludeConnectionId: connectionId);

            _logger.LogInformation($"[CallHub] Call {callUuid} accepted by user {userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CallHub] Error accepting call");
            await Clients.Caller.SendAsync("Error", "Failed to accept call");
        }
    }

    /// <summary>
    /// Reject an incoming call
    /// </summary>
    public async Task RejectCall(string callUuid, string? reason = null)
    {
        try
        {
            var userId = Context.User?.Identity?.GetProfileId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            _logger.LogInformation($"[CallHub] User {userId} rejecting call {callUuid}");

            if (!ActiveCalls.TryGetValue(callUuid, out var callSession))
            {
                await Clients.Caller.SendAsync("Error", "Call not found");
                return;
            }

            // Update call in database
            var call = await _context.Calls.FirstOrDefaultAsync(c => c.CallUuid == callUuid);
            if (call != null)
            {
                call.Status = CallStatus.Rejected;
                call.EndedAt = DateTimeOffset.UtcNow;
                call.EndReason = reason ?? "Rejected";
                call.TimeUpdated = DateTimeOffset.UtcNow;
            }

            // Update participant
            var participant = await _context.CallParticipants
                .FirstOrDefaultAsync(cp => cp.CallId == call!.Id && cp.UserId == userId.Value);

            if (participant != null)
            {
                participant.Status = CallParticipantStatus.Rejected;
                participant.TimeUpdated = DateTimeOffset.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Remove from active calls
            ActiveCalls.TryRemove(callUuid, out _);

            // Notify caller that call was rejected
            await NotifyUserDevices(callSession.InitiatorUserId, "CallRejected", new
            {
                CallUuid = callUuid,
                Reason = reason ?? "Call rejected"
            });

            // Stop ringing on all target user's devices
            await NotifyUserDevices(userId.Value, "CallEnded", new
            {
                CallUuid = callUuid,
                Reason = "You rejected the call"
            });

            _logger.LogInformation($"[CallHub] Call {callUuid} rejected by user {userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CallHub] Error rejecting call");
            await Clients.Caller.SendAsync("Error", "Failed to reject call");
        }
    }

    /// <summary>
    /// End an active call
    /// </summary>
    public async Task EndCall(string callUuid)
    {
        try
        {
            var userId = Context.User?.Identity?.GetProfileId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "User not authenticated");
                return;
            }

            _logger.LogInformation($"[CallHub] User {userId} ending call {callUuid}");

            if (!ActiveCalls.TryGetValue(callUuid, out var callSession))
            {
                await Clients.Caller.SendAsync("Error", "Call not found");
                return;
            }

            // Verify user is a participant in this call
            if (callSession.InitiatorUserId != userId.Value && callSession.TargetUserId != userId.Value)
            {
                await Clients.Caller.SendAsync("Error", "You are not a participant in this call");
                return;
            }

            // Update call in database
            var call = await _context.Calls.FirstOrDefaultAsync(c => c.CallUuid == callUuid);
            if (call != null)
            {
                call.Status = CallStatus.Ended;
                call.EndedAt = DateTimeOffset.UtcNow;
                call.EndReason = "Normal";

                if (call.StartedAt.HasValue)
                {
                    call.Duration = call.EndedAt.Value - call.StartedAt.Value;
                }

                call.TimeUpdated = DateTimeOffset.UtcNow;
            }

            // Update all participants
            var participants = await _context.CallParticipants
                .Where(cp => cp.CallId == call!.Id)
                .ToListAsync();

            foreach (var participant in participants)
            {
                if (participant.Status == CallParticipantStatus.Joined)
                {
                    participant.Status = CallParticipantStatus.Left;
                    participant.LeftAt = DateTimeOffset.UtcNow;
                    participant.TimeUpdated = DateTimeOffset.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            // Remove from active calls
            ActiveCalls.TryRemove(callUuid, out _);

            // Notify all participants that call ended
            await NotifyUserDevices(callSession.InitiatorUserId, "CallEnded", new
            {
                CallUuid = callUuid,
                Reason = "Call ended",
                Duration = call?.Duration?.TotalSeconds
            });

            await NotifyUserDevices(callSession.TargetUserId, "CallEnded", new
            {
                CallUuid = callUuid,
                Reason = "Call ended",
                Duration = call?.Duration?.TotalSeconds
            });

            _logger.LogInformation($"[CallHub] Call {callUuid} ended successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CallHub] Error ending call");
            await Clients.Caller.SendAsync("Error", "Failed to end call");
        }
    }

    #endregion

    #region WebRTC Signaling

    /// <summary>
    /// Send WebRTC offer (SDP) to the other peer
    /// SECURITY: Only sends to the SPECIFIC device in the call, not all user devices!
    /// </summary>
    public async Task SendOffer(string callUuid, string sdp)
    {
        try
        {
            var userId = Context.User?.Identity?.GetProfileId();
            if (!userId.HasValue) return;

            if (!ActiveCalls.TryGetValue(callUuid, out var callSession)) return;

            string? targetConnectionId = null;

            if (callSession.InitiatorUserId == userId.Value)
            {
                // Initiator is sending offer → Send to answering device
                targetConnectionId = callSession.TargetConnectionId;
            }
            else
            {
                // Target is sending offer 
                targetConnectionId = callSession.InitiatorConnectionId;
            }

            if (string.IsNullOrEmpty(targetConnectionId))
            {
                _logger.LogWarning($"[CallHub] No target device for offer in call {callUuid}");
                return;
            }

            _logger.LogInformation($"[CallHub] Sending offer for call {callUuid} to ConnectionId {targetConnectionId}");

            await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", new
            {
                CallUuid = callUuid,
                Sdp = sdp,
                FromUserId = userId.Value
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CallHub] Error sending offer");
        }
    }

    /// <summary>
    /// Send WebRTC answer (SDP) to the other peer
    /// SECURITY: Only sends to the SPECIFIC device in the call!
    /// </summary>
    public async Task SendAnswer(string callUuid, string sdp)
    {
        try
        {
            var userId = Context.User?.Identity?.GetProfileId();
            if (!userId.HasValue) return;

            if (!ActiveCalls.TryGetValue(callUuid, out var callSession)) return;

            // Answer always goes to the initiator's device
            var targetConnectionId = callSession.InitiatorConnectionId;

            _logger.LogInformation($"[CallHub] Sending answer for call {callUuid} to ConnectionId {targetConnectionId}");

            await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", new
            {
                CallUuid = callUuid,
                Sdp = sdp,
                FromUserId = userId.Value
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CallHub] Error sending answer");
        }
    }

    /// <summary>
    /// Send ICE candidate to the other peer
    /// SECURITY: Only sends to the SPECIFIC device in the call!
    /// </summary>
    public async Task SendIceCandidate(string callUuid, string candidate, string sdpMid, int sdpMLineIndex)
    {
        try
        {
            var userId = Context.User?.Identity?.GetProfileId();
            if (!userId.HasValue) return;

            if (!ActiveCalls.TryGetValue(callUuid, out var callSession)) return;

            // Determine target ConnectionId (specific device!)
            string? targetConnectionId = null;

            if (callSession.InitiatorUserId == userId.Value)
            {
                // Initiator sending ICE → Send to answering device
                targetConnectionId = callSession.TargetConnectionId;
            }
            else
            {
                // Target sending ICE → Send to initiator device
                targetConnectionId = callSession.InitiatorConnectionId;
            }

            if (string.IsNullOrEmpty(targetConnectionId))
            {
                _logger.LogWarning($"[CallHub] No target device for ICE candidate in call {callUuid}");
                return;
            }

            await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", new
            {
                CallUuid = callUuid,
                Candidate = candidate,
                SdpMid = sdpMid,
                SdpMLineIndex = sdpMLineIndex,
                FromUserId = userId.Value
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CallHub] Error sending ICE candidate");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Notify all devices of a user
    /// </summary>
    private async Task NotifyUserDevices(long userId, string method, object data, string? excludeConnectionId = null)
    {
        if (UserConnections.TryGetValue(userId, out var connections))
        {
            var targetConnections = connections.Where(c => c != excludeConnectionId).ToList();
            if (targetConnections.Any())
            {
                await Clients.Clients(targetConnections).SendAsync(method, data);
            }
        }
    }

    /// <summary>
    /// Ring all devices of a user
    /// </summary>
    private async Task RingUserDevices(long userId, object callData)
    {
        await NotifyUserDevices(userId, "IncomingCall", callData);
    }

    /// <summary>
    /// Handle calls when a user disconnects
    /// </summary>
    private async Task HandleDisconnectedUserCalls(long userId, string connectionId)
    {
        // Find all active calls involving this user
        var userCalls = ActiveCalls.Values.Where(c =>
            c.InitiatorUserId == userId || c.TargetUserId == userId).ToList();

        foreach (var callSession in userCalls)
        {
            // End the call
            await EndCall(callSession.CallUuid);
        }
    }

    #endregion
}

#region Supporting Classes

public class DeviceInfo
{
    public long UserId { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string? DeviceToken { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public DateTimeOffset ConnectedAt { get; set; }
}

public class CallSessionInfo
{
    public string CallUuid { get; set; } = string.Empty;
    public long CallId { get; set; }
    public long InitiatorUserId { get; set; }
    public string InitiatorConnectionId { get; set; } = string.Empty;  // Which device initiated
    public long TargetUserId { get; set; }
    public string? TargetConnectionId { get; set; }  // Which device answered (null until answered)
    public bool IsVideoCall { get; set; }
    public CallStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
}

#endregion
