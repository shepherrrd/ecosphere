using System.Collections.Concurrent;
using Ecosphere.Infrastructure.Infrastructure.Services.Interfaces;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;

namespace Ecosphere.Infrastructure.Infrastructure.SignalR;

/// <summary>
/// SignalR Hub for SFU (Selective Forwarding Unit) WebRTC
/// Server-side media forwarding for multi-party video conferences
/// </summary>
[Authorize]
public class SFUHub : Hub
{
    private readonly ILogger<SFUHub> _logger;
    private readonly ISFUMediaService _sfuMediaService;

    // Key: RoomId, Value: Dictionary<ConnectionId, PeerInfo>
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PeerInfo>> Rooms = new();

    // Key: ConnectionId, Value: RoomId
    private static readonly ConcurrentDictionary<string, string> ConnectionRooms = new();

    public SFUHub(ILogger<SFUHub> logger, ISFUMediaService sfuMediaService)
    {
        _logger = logger;
        _sfuMediaService = sfuMediaService;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = Context.User?.Identity?.GetProfileId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("[SFU Hub] Connection attempt without valid user ID");
                Context.Abort();
                return;
            }

            var connectionId = Context.ConnectionId;
            _logger.LogInformation($"[SFU Hub] User {userId} connected with ConnectionId: {connectionId}");
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SFU Hub] Error in OnConnectedAsync");
            throw;
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        // Remove server-side peer connection
        await _sfuMediaService.RemovePeerConnectionAsync(connectionId);

        if (ConnectionRooms.TryRemove(connectionId, out var roomId))
        {
            if (Rooms.TryGetValue(roomId, out var participants))
            {
                if (participants.TryRemove(connectionId, out var peerInfo))
                {
                    // Notify others in room
                    await Clients.OthersInGroup(roomId).SendAsync("PeerLeft", new
                    {
                        PeerId = connectionId
                    });

                    _logger.LogInformation($"[SFU Hub] Peer {connectionId} ({peerInfo.DisplayName}) left room {roomId}");
                }

                // Clean up empty room
                if (participants.Count == 0)
                {
                    Rooms.TryRemove(roomId, out _);
                    _logger.LogInformation($"[SFU Hub] Room {roomId} is now empty");
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join an SFU room and create server-side peer connection
    /// </summary>
    public async Task<object> JoinRoom(string roomId, string displayName)
    {
        try
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation($"[SFU Hub] {displayName} ({connectionId}) joining room {roomId}");

            // Create server-side peer connection for this client
            var peerConnection = await _sfuMediaService.CreatePeerConnectionAsync(connectionId, roomId);

            // Create or get room
            var room = Rooms.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, PeerInfo>());

            var peerInfo = new PeerInfo
            {
                ConnectionId = connectionId,
                DisplayName = displayName,
                JoinedAt = DateTimeOffset.UtcNow
            };

            room.TryAdd(connectionId, peerInfo);
            ConnectionRooms.TryAdd(connectionId, roomId);

            // Add to SignalR group
            await Groups.AddToGroupAsync(connectionId, roomId);

            // Get existing peers
            var existingPeers = room
                .Where(kvp => kvp.Key != connectionId)
                .Select(kvp => new
                {
                    PeerId = kvp.Key,
                    DisplayName = kvp.Value.DisplayName
                })
                .ToList();

            // Notify others that new peer joined
            await Clients.OthersInGroup(roomId).SendAsync("PeerJoined", new
            {
                PeerId = connectionId,
                DisplayName = displayName
            });

            _logger.LogInformation($"[SFU Hub] {displayName} joined room {roomId} with {existingPeers.Count} existing peers");

            return new
            {
                Success = true,
                PeerId = connectionId,
                ExistingPeers = existingPeers,
                Message = $"Joined room {roomId}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[SFU Hub] Error joining room {roomId}");
            return new { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Client sends offer to server
    /// Server processes and sends answer back to client
    /// </summary>
    public async Task SendOffer(string sdp, string type)
    {
        try
        {
            var connectionId = Context.ConnectionId;
            _logger.LogInformation($"[SFU Hub] Received offer from {connectionId}");

            var peerConnection = _sfuMediaService.GetPeerConnection(connectionId);
            if (peerConnection == null)
            {
                _logger.LogWarning($"[SFU Hub] No peer connection found for {connectionId}");
                return;
            }

            // Set remote description (client's offer)
            var offer = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = sdp
            };

            var setRemoteResult = peerConnection.setRemoteDescription(offer);
            if (setRemoteResult != SetDescriptionResultEnum.OK)
            {
                _logger.LogError($"[SFU Hub] Failed to set remote description for {connectionId}: {setRemoteResult}");
                return;
            }

            // Create answer
            var answer = peerConnection.createAnswer(null);
            if (answer == null)
            {
                _logger.LogError($"[SFU Hub] Failed to create answer for {connectionId}");
                return;
            }

            // Set local description (server's answer)
            peerConnection.setLocalDescription(answer);
            _logger.LogInformation($"[SFU Hub] Set local description for {connectionId}");

            _logger.LogInformation($"[SFU Hub] Sending answer to {connectionId}");

            // Send answer back to client
            await Clients.Caller.SendAsync("ReceiveAnswer", new
            {
                Sdp = answer.sdp,
                Type = answer.type.ToString().ToLower()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[SFU Hub] Error processing offer");
        }
    }

    /// <summary>
    /// Client sends ICE candidate to server
    /// </summary>
    public async Task SendIceCandidate(string candidate, string? sdpMid, int? sdpMLineIndex)
    {
        try
        {
            var connectionId = Context.ConnectionId;

            var peerConnection = _sfuMediaService.GetPeerConnection(connectionId);
            if (peerConnection == null)
            {
                _logger.LogWarning($"[SFU Hub] No peer connection found for {connectionId}");
                return;
            }

            // Add ICE candidate to server-side peer connection
            var init = new RTCIceCandidateInit
            {
                candidate = candidate,
                sdpMid = sdpMid,
                sdpMLineIndex = (ushort)(sdpMLineIndex ?? 0)
            };

            peerConnection.addIceCandidate(init);

            _logger.LogDebug($"[SFU Hub] Added ICE candidate for {connectionId}");

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[SFU Hub] Error adding ICE candidate");
        }
    }

    /// <summary>
    /// Get current room statistics
    /// </summary>
    public Task<object> GetRoomStats(string roomId)
    {
        if (Rooms.TryGetValue(roomId, out var participants))
        {
            return Task.FromResult<object>(new
            {
                RoomId = roomId,
                ParticipantCount = participants.Count,
                Participants = participants.Select(kvp => new
                {
                    PeerId = kvp.Key,
                    DisplayName = kvp.Value.DisplayName,
                    JoinedAt = kvp.Value.JoinedAt
                }).ToList()
            });
        }

        return Task.FromResult<object>(new
        {
            RoomId = roomId,
            ParticipantCount = 0,
            Participants = new List<object>()
        });
    }
}

/// <summary>
/// Information about a peer in an SFU room
/// </summary>
public class PeerInfo
{
    public string ConnectionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; }
}
