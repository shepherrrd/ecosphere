using System.Collections.Concurrent;
using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosphere.Infrastructure.Infrastructure.SignalR;

[Authorize]
public class MeetingHub : Hub
{
    private readonly ILogger<MeetingHub> _logger;
    private readonly EcosphereDbContext _context;

    // Key: MeetingCode, Value: Dictionary<UserId, List<ConnectionId>>
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<long, HashSet<string>>> MeetingConnections = new();

    // Key: ConnectionId, Value: (MeetingCode, UserId)
    private static readonly ConcurrentDictionary<string, (string MeetingCode, long UserId)> ConnectionMeetings = new();

    public MeetingHub(ILogger<MeetingHub> logger, EcosphereDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        _logger.LogInformation($"[MeetingHub] User {userId} connected with connectionId {Context.ConnectionId}");

        // Add to user group for receiving invites
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        if (ConnectionMeetings.TryRemove(connectionId, out var meetingInfo))
        {
            var (meetingCode, userId) = meetingInfo;

            if (MeetingConnections.TryGetValue(meetingCode, out var participants))
            {
                if (participants.TryGetValue(userId, out var userConnections))
                {
                    userConnections.Remove(connectionId);

                    // If user has no more connections, remove them from meeting
                    if (userConnections.Count == 0)
                    {
                        participants.TryRemove(userId, out _);

                        // Notify others that participant left
                        await Clients.Group(meetingCode).SendAsync("ParticipantLeft", new
                        {
                            MeetingCode = meetingCode,
                            UserId = userId
                        });

                        _logger.LogInformation($"[MeetingHub] User {userId} left meeting {meetingCode}");

                        // Update database - mark participant as inactive
                        var meeting = await _context.Meetings
                            .Where(m => m.MeetingCode == meetingCode)
                            .FirstOrDefaultAsync();

                        if (meeting != null)
                        {
                            var participant = await _context.MeetingParticipants
                                .FirstOrDefaultAsync(mp => mp.UserId == userId && mp.MeetingId == meeting.Id);

                            if (participant != null)
                            {
                                participant.IsActive = false;
                                participant.LeftAt = DateTimeOffset.UtcNow;
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                }

                // If meeting is empty, clean up
                if (participants.Count == 0)
                {
                    MeetingConnections.TryRemove(meetingCode, out _);
                    _logger.LogInformation($"[MeetingHub] Meeting {meetingCode} is now empty");
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<object> JoinMeeting(string meetingCode)
    {
        try
        {
            var userId = GetUserId();
            var connectionId = Context.ConnectionId;

            _logger.LogInformation($"[MeetingHub] User {userId} attempting to join meeting {meetingCode}");

            // Verify meeting exists and is active
            var meeting = await _context.Meetings
                .FirstOrDefaultAsync(m => m.MeetingCode == meetingCode && m.IsActive);

            if (meeting == null)
            {
                throw new HubException("Meeting not found or is not active");
            }

            _logger.LogInformation($"[MeetingHub] Meeting IsPublic: {meeting.IsPublic}, HostId: {meeting.HostId}, RequestingUserId: {userId}");

            // Check if meeting is private and user is not the host
            if (!meeting.IsPublic && userId != meeting.HostId)
            {
                _logger.LogInformation($"[MeetingHub] Meeting is private and user {userId} is not the host - checking for join request");

                // Check if there's already a pending or approved join request
                var existingRequest = await _context.MeetingJoinRequests
                    .FirstOrDefaultAsync(mjr => mjr.MeetingId == meeting.Id && mjr.UserId == userId);

                _logger.LogInformation($"[MeetingHub] Existing request status: {existingRequest?.Status ?? "null"}");

                if (existingRequest == null)
                {
                    // Create a new join request
                    var joinRequest = new MeetingJoinRequest
                    {
                        MeetingId = meeting.Id,
                        UserId = userId,
                        ConnectionId = connectionId, // Store connection ID for later notification
                        Status = "Pending",
                        TimeCreated = DateTimeOffset.UtcNow,
                        TimeUpdated = DateTimeOffset.UtcNow
                    };

                    _context.MeetingJoinRequests.Add(joinRequest);
                    await _context.SaveChangesAsync();

                    // Notify host about join request
                    var requestingUser = await _context.Users.FindAsync(userId);

                    // Send to all host's connections in the meeting
                    _logger.LogInformation($"[MeetingHub] Looking for host {meeting.HostId} in meeting connections");

                    if (MeetingConnections.TryGetValue(meetingCode, out var meetingParticipants))
                    {
                        _logger.LogInformation($"[MeetingHub] Found meeting connections. Participants count: {meetingParticipants.Count}");

                        if (meetingParticipants.TryGetValue(meeting.HostId, out var hostConnections))
                        {
                            _logger.LogInformation($"[MeetingHub] Found host connections. Count: {hostConnections.Count}");

                            foreach (var hostConnectionId in hostConnections)
                            {
                                _logger.LogInformation($"[MeetingHub] Sending join request notification to host connection: {hostConnectionId}");

                                await Clients.Client(hostConnectionId).SendAsync("JoinRequestReceived", new
                                {
                                    MeetingCode = meetingCode,
                                    RequestId = joinRequest.Id,
                                    UserId = userId,
                                    UserName = requestingUser?.UserName,
                                    DisplayName = requestingUser?.DisplayName,
                                    ProfileImageUrl = requestingUser?.ProfileImageUrl
                                });
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"[MeetingHub] Host {meeting.HostId} not found in meeting connections");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"[MeetingHub] Meeting code {meetingCode} not found in MeetingConnections");
                    }

                    _logger.LogInformation($"[MeetingHub] Join request created for user {userId} to meeting {meetingCode}");

                    return new
                    {
                        Success = false,
                        Message = "Join request sent. Waiting for host approval.",
                        RequiresApproval = true
                    };
                }
                else if (existingRequest.Status == "Pending")
                {
                    _logger.LogInformation($"[MeetingHub] Pending request exists - re-notifying host if they're in the meeting");

                    // Update connection ID in case user reconnected
                    existingRequest.ConnectionId = connectionId;
                    await _context.SaveChangesAsync();

                    // Re-send notification to host if they're in the meeting
                    var requestingUser = await _context.Users.FindAsync(userId);

                    if (MeetingConnections.TryGetValue(meetingCode, out var meetingParticipants))
                    {
                        _logger.LogInformation($"[MeetingHub] Found meeting connections. Participants count: {meetingParticipants.Count}");

                        if (meetingParticipants.TryGetValue(meeting.HostId, out var hostConnections))
                        {
                            _logger.LogInformation($"[MeetingHub] Found host connections. Count: {hostConnections.Count}");

                            foreach (var hostConnectionId in hostConnections)
                            {
                                _logger.LogInformation($"[MeetingHub] Re-sending join request notification to host connection: {hostConnectionId}");

                                await Clients.Client(hostConnectionId).SendAsync("JoinRequestReceived", new
                                {
                                    MeetingCode = meetingCode,
                                    RequestId = existingRequest.Id,
                                    UserId = userId,
                                    UserName = requestingUser?.UserName,
                                    DisplayName = requestingUser?.DisplayName,
                                    ProfileImageUrl = requestingUser?.ProfileImageUrl
                                });
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"[MeetingHub] Host {meeting.HostId} not found in meeting connections");
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"[MeetingHub] Meeting code {meetingCode} not found in MeetingConnections");
                    }

                    return new
                    {
                        Success = false,
                        Message = "Join request already sent. Waiting for host approval.",
                        RequiresApproval = true
                    };
                }
                else if (existingRequest.Status == "Rejected")
                {
                    throw new HubException("Your join request was rejected by the host");
                }
                // If status is "Approved", continue with joining
            }

            // Check if meeting is full
            var activeMeetingConnections = MeetingConnections.GetOrAdd(meetingCode, _ => new ConcurrentDictionary<long, HashSet<string>>());
            if (activeMeetingConnections.Count >= meeting.MaxParticipants && !activeMeetingConnections.ContainsKey(userId))
            {
                throw new HubException("Meeting is full");
            }

            // Add connection to meeting
            var userConnections = activeMeetingConnections.GetOrAdd(userId, _ => new HashSet<string>());
            userConnections.Add(connectionId);

            ConnectionMeetings[connectionId] = (meetingCode, userId);

            // Add to SignalR group
            await Groups.AddToGroupAsync(connectionId, meetingCode);

            // Get current participants (excluding self)
            var currentParticipants = new List<object>();
            foreach (var (participantUserId, participantConnections) in activeMeetingConnections)
            {
                if (participantUserId != userId)
                {
                    var user = await _context.Users.FindAsync(participantUserId);
                    if (user != null)
                    {
                        // Get the first connection ID for this user
                        var participantConnectionId = participantConnections.FirstOrDefault();
                        if (participantConnectionId != null)
                        {
                            currentParticipants.Add(new
                            {
                                UserId = user.Id,
                                UserName = user.UserName,
                                DisplayName = user.DisplayName,
                                ProfileImageUrl = user.ProfileImageUrl,
                                ConnectionId = participantConnectionId
                            });
                        }
                    }
                }
            }

            // Notify others that new participant joined
            var joiningUser = await _context.Users.FindAsync(userId);
            await Clients.GroupExcept(meetingCode, connectionId).SendAsync("ParticipantJoined", new
            {
                MeetingCode = meetingCode,
                UserId = userId,
                UserName = joiningUser?.UserName,
                DisplayName = joiningUser?.DisplayName,
                ProfileImageUrl = joiningUser?.ProfileImageUrl,
                ConnectionId = connectionId
            });

            _logger.LogInformation($"[MeetingHub] User {userId} joined meeting {meetingCode} successfully");

            return new
            {
                Success = true,
                Message = "Joined meeting successfully",
                MeetingCode = meetingCode,
                CurrentParticipants = currentParticipants
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[MeetingHub] Error joining meeting");
            throw;
        }
    }

    public async Task LeaveMeeting(string meetingCode)
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;

        _logger.LogInformation($"[MeetingHub] User {userId} leaving meeting {meetingCode}");

        if (ConnectionMeetings.TryRemove(connectionId, out var meetingInfo))
        {
            if (MeetingConnections.TryGetValue(meetingCode, out var participants))
            {
                if (participants.TryGetValue(userId, out var userConnections))
                {
                    userConnections.Remove(connectionId);

                    if (userConnections.Count == 0)
                    {
                        participants.TryRemove(userId, out _);

                        await Clients.Group(meetingCode).SendAsync("ParticipantLeft", new
                        {
                            MeetingCode = meetingCode,
                            UserId = userId
                        });
                    }
                }
            }
        }

        await Groups.RemoveFromGroupAsync(connectionId, meetingCode);
    }

    // WebRTC Signaling for multi-peer mesh
    public async Task SendOffer(string meetingCode, long targetUserId, string sdp)
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;

        _logger.LogInformation($"[MeetingHub] User {userId} sending offer to user {targetUserId} in meeting {meetingCode}");

        if (MeetingConnections.TryGetValue(meetingCode, out var participants))
        {
            if (participants.TryGetValue(targetUserId, out var targetConnections))
            {
                // Send to all target user's connections
                foreach (var targetConnectionId in targetConnections)
                {
                    await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", new
                    {
                        MeetingCode = meetingCode,
                        FromUserId = userId,
                        FromConnectionId = connectionId,
                        Sdp = sdp
                    });
                }
            }
        }
    }

    public async Task SendAnswer(string meetingCode, string targetConnectionId, string sdp)
    {
        var userId = GetUserId();

        _logger.LogInformation($"[MeetingHub] User {userId} sending answer to connection {targetConnectionId}");

        await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", new
        {
            MeetingCode = meetingCode,
            FromUserId = userId,
            FromConnectionId = Context.ConnectionId,
            Sdp = sdp
        });
    }

    public async Task SendIceCandidate(string meetingCode, string targetConnectionId, string candidate, string sdpMid, int sdpMLineIndex)
    {
        var userId = GetUserId();

        await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", new
        {
            MeetingCode = meetingCode,
            FromUserId = userId,
            FromConnectionId = Context.ConnectionId,
            Candidate = candidate,
            SdpMid = sdpMid,
            SdpMLineIndex = sdpMLineIndex
        });
    }

    public async Task ApproveJoinRequest(long requestId)
    {
        var userId = GetUserId();

        var joinRequest = await _context.MeetingJoinRequests.FindAsync(requestId);
        if (joinRequest == null)
        {
            throw new HubException("Join request not found");
        }

        var meeting = await _context.Meetings.FindAsync(joinRequest.MeetingId);
        if (meeting == null || meeting.HostId != userId)
        {
            throw new HubException("Only the host can approve join requests");
        }

        joinRequest.Status = "Approved";
        joinRequest.RespondedAt = DateTimeOffset.UtcNow;
        joinRequest.TimeUpdated = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        // Notify the user that their request was approved using their connection ID
        await Clients.Client(joinRequest.ConnectionId).SendAsync("JoinRequestApproved", new
        {
            MeetingCode = meeting.MeetingCode,
            RequestId = requestId
        });

        _logger.LogInformation($"[MeetingHub] Host {userId} approved join request {requestId}");
    }

    public async Task RejectJoinRequest(long requestId)
    {
        var userId = GetUserId();

        var joinRequest = await _context.MeetingJoinRequests.FindAsync(requestId);
        if (joinRequest == null)
        {
            throw new HubException("Join request not found");
        }

        var meeting = await _context.Meetings.FindAsync(joinRequest.MeetingId);
        if (meeting == null || meeting.HostId != userId)
        {
            throw new HubException("Only the host can reject join requests");
        }

        joinRequest.Status = "Rejected";
        joinRequest.RespondedAt = DateTimeOffset.UtcNow;
        joinRequest.TimeUpdated = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        // Notify the user that their request was rejected using their connection ID
        await Clients.Client(joinRequest.ConnectionId).SendAsync("JoinRequestRejected", new
        {
            MeetingCode = meeting.MeetingCode,
            RequestId = requestId,
            Message = "Your join request was rejected by the host"
        });

        _logger.LogInformation($"[MeetingHub] Host {userId} rejected join request {requestId}");
    }

    /// <summary>
    /// Invite a user to join the meeting
    /// </summary>
    public async Task InviteUserToMeeting(string meetingCode, long invitedUserId)
    {
        var userId = GetUserId();

        try
        {
            // Verify meeting exists and user is a participant
            var meeting = await _context.Meetings
                .FirstOrDefaultAsync(m => m.MeetingCode == meetingCode);

            if (meeting == null)
            {
                await Clients.Caller.SendAsync("Error", "Meeting not found");
                return;
            }

            // Check if sender is in the meeting
            var isParticipant = await _context.MeetingParticipants
                .AnyAsync(mp => mp.MeetingId == meeting.Id && mp.UserId == userId);

            if (!isParticipant)
            {
                await Clients.Caller.SendAsync("Error", "You must be in the meeting to invite others");
                return;
            }

            // Check if they are contacts
            var areContacts = await _context.Contacts
                .AnyAsync(c =>
                    (c.UserId == userId && c.ContactUserId == invitedUserId) ||
                    (c.UserId == invitedUserId && c.ContactUserId == userId));

            if (!areContacts)
            {
                await Clients.Caller.SendAsync("Error", "You can only invite your contacts");
                return;
            }

            // Get inviter info
            var inviter = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => new { u.UserName, u.DisplayName })
                .FirstAsync();

            // Send invite to the user
            await Clients.Group($"user_{invitedUserId}").SendAsync("MeetingInvite", new
            {
                MeetingCode = meetingCode,
                MeetingTitle = meeting.Title,
                InviterId = userId,
                InviterName = inviter.DisplayName ?? inviter.UserName,
                MeetingId = meeting.Id,
                IsPublic = meeting.IsPublic
            });

            _logger.LogInformation($"[MeetingHub] User {userId} invited user {invitedUserId} to meeting {meetingCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[MeetingHub] Error inviting user {invitedUserId} to meeting {meetingCode}");
            await Clients.Caller.SendAsync("Error", "Failed to send meeting invite");
        }
    }

    private long GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            throw new HubException("User not authenticated");
        }
        return userId;
    }
}
