using System.Collections.Concurrent;
using Ecosphere.Infrastructure.Infrastructure.Services.Interfaces;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;

namespace Ecosphere.Infrastructure.Infrastructure.Services.Implementations;

/// <summary>
/// SFU (Selective Forwarding Unit) Media Service
/// Handles server-side RTCPeerConnections for media forwarding
/// </summary>
public class SFUMediaService : ISFUMediaService
{
    private readonly ILogger<SFUMediaService> _logger;
    private readonly IStunTurnServer _stunTurnServer;

    // Track peer connections: ConnectionId -> RTCPeerConnection
    private readonly ConcurrentDictionary<string, RTCPeerConnection> _peerConnections = new();

    // Track which room each connection belongs to: ConnectionId -> RoomId
    private readonly ConcurrentDictionary<string, string> _connectionRooms = new();

    public SFUMediaService(ILogger<SFUMediaService> logger, IStunTurnServer stunTurnServer)
    {
        _logger = logger;
        _stunTurnServer = stunTurnServer;
    }

    public async Task<RTCPeerConnection> CreatePeerConnectionAsync(string connectionId, string roomId)
    {
        try
        {
            _logger.LogInformation($"[SFU Media] Creating peer connection for {connectionId} in room {roomId}");

            // Create RTCPeerConnection with ICE servers
            var config = new RTCConfiguration
            {
                iceServers = new List<RTCIceServer>()
            };

            // Add STUN server
            var stunConfig = _stunTurnServer.GetStunConfig();
            if (stunConfig.Urls != null && stunConfig.Urls.Any())
            {
                foreach (var url in stunConfig.Urls)
                {
                    config.iceServers.Add(new RTCIceServer { urls = url });
                }
            }

            var turnConfig = _stunTurnServer.GetTurnConfig(0); // 0 for server connections
            if (turnConfig.Urls != null && turnConfig.Urls.Any())
            {
                foreach (var url in turnConfig.Urls)
                {
                    config.iceServers.Add(new RTCIceServer
                    {
                        urls = url,
                        username = turnConfig.Username,
                        credential = turnConfig.Credential,
                        credentialType = RTCIceCredentialType.password
                    });
                }
            }

            var peerConnection = new RTCPeerConnection(config);


            // Add audio track (recvonly) - using PCMU (payload type 0)
            var audioFormats = new List<SDPAudioVideoMediaFormat>
            {
                new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.audio, 0, "PCMU", 8000)
            };
            var audioTrack = new MediaStreamTrack(
                SDPMediaTypesEnum.audio,
                false,
                audioFormats,
                MediaStreamStatusEnum.RecvOnly);
            peerConnection.addTrack(audioTrack);
            _logger.LogInformation($"[SFU Media] Added audio track (recvonly) for {connectionId}");

            // Add video track (recvonly) - using VP8 (payload type 96)
            var videoFormats = new List<SDPAudioVideoMediaFormat>
            {
                new SDPAudioVideoMediaFormat(SDPMediaTypesEnum.video, 96, "VP8", 90000)
            };
            var videoTrack = new MediaStreamTrack(
                SDPMediaTypesEnum.video,
                false,
                videoFormats,
                MediaStreamStatusEnum.RecvOnly);
            peerConnection.addTrack(videoTrack);
            _logger.LogInformation($"[SFU Media] Added video track (recvonly) for {connectionId}");

            // Handle ICE connection state changes
            peerConnection.oniceconnectionstatechange += (RTCIceConnectionState state) =>
            {
                _logger.LogInformation($"[SFU Media] ICE connection state for {connectionId}: {state}");

                if (state == RTCIceConnectionState.failed || state == RTCIceConnectionState.disconnected)
                {
                    _ = RemovePeerConnectionAsync(connectionId);
                }
            };

            peerConnection.onconnectionstatechange += (RTCPeerConnectionState state) =>
            {
                _logger.LogInformation($"[SFU Media] Connection state for {connectionId}: {state}");
            };

            // Handle incoming video frames - forward to other peers
            peerConnection.OnVideoFrameReceived += (ep, timestamp, frame, format) =>
            {
                _logger.LogInformation($"[SFU Media] Received video frame from {connectionId}, timestamp: {timestamp}, size: {frame.Length} bytes");

                // Forward video to other peers in the room using SendVideo
                ForwardVideoToOtherPeers(connectionId, roomId, timestamp, frame);
            };

            // Handle incoming RTP packets for both audio and video
            peerConnection.OnRtpPacketReceived += (ep, mediaType, rtpPacket) =>
            {
                // Forward audio RTP packets to other peers
                if (mediaType == SDPMediaTypesEnum.audio)
                {
                    _logger.LogInformation($"[SFU Media] Received audio RTP packet from {connectionId}, payload: {rtpPacket.Payload.Length} bytes");
                    ForwardAudioRtpToOtherPeers(connectionId, roomId, rtpPacket);
                }
            };

            // Store the peer connection
            _peerConnections.TryAdd(connectionId, peerConnection);
            _connectionRooms.TryAdd(connectionId, roomId);

            _logger.LogInformation($"[SFU Media] Peer connection created for {connectionId}");

            return peerConnection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[SFU Media] Error creating peer connection for {connectionId}");
            throw;
        }
    }

    public async Task RemovePeerConnectionAsync(string connectionId)
    {
        try
        {
            if (_peerConnections.TryRemove(connectionId, out var peerConnection))
            {
                _logger.LogInformation($"[SFU Media] Removing peer connection for {connectionId}");

                peerConnection.close();
                peerConnection.Dispose();

                _connectionRooms.TryRemove(connectionId, out _);

                _logger.LogInformation($"[SFU Media] Peer connection removed for {connectionId}");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[SFU Media] Error removing peer connection for {connectionId}");
        }
    }

    public RTCPeerConnection? GetPeerConnection(string connectionId)
    {
        _peerConnections.TryGetValue(connectionId, out var peerConnection);
        return peerConnection;
    }

    public IEnumerable<(string ConnectionId, RTCPeerConnection PeerConnection)> GetPeerConnectionsInRoom(string roomId)
    {
        return _connectionRooms
            .Where(kvp => kvp.Value == roomId)
            .Select(kvp => (kvp.Key, _peerConnections[kvp.Key]))
            .ToList();
    }

    /// <summary>
    /// Forward video frame to all other peers in the same room
    /// Uses SIPSorcery's SendVideo method following the pattern from WebRTC examples
    /// </summary>
    private void ForwardVideoToOtherPeers(string senderConnectionId, string roomId, uint timestamp, byte[] frame)
    {
        try
        {
            var otherPeers = GetPeerConnectionsInRoom(roomId)
                .Where(p => p.ConnectionId != senderConnectionId)
                .ToList();

            if (otherPeers.Count == 0)
            {
                _logger.LogDebug($"[SFU Media] No other peers to forward video to (sender: {senderConnectionId})");
                return;
            }

            _logger.LogInformation($"[SFU Media] Forwarding video from {senderConnectionId} to {otherPeers.Count} peer(s), frame size: {frame.Length} bytes");

            foreach (var (connectionId, peerConnection) in otherPeers)
            {
                try
                {
                    // Forward video using SendVideo - this is the correct SIPSorcery pattern
                    peerConnection.SendVideo(timestamp, frame);
                    _logger.LogDebug($"[SFU Media] Sent video to {connectionId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[SFU Media] Error forwarding video to {connectionId}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[SFU Media] Error forwarding video from {senderConnectionId}");
        }
    }

    /// <summary>
    /// Forward audio RTP packet to all other peers in the same room
    /// Uses SIPSorcery's SendAudio method with payload length and data
    /// </summary>
    private void ForwardAudioRtpToOtherPeers(string senderConnectionId, string roomId, SIPSorcery.Net.RTPPacket rtpPacket)
    {
        try
        {
            var otherPeers = GetPeerConnectionsInRoom(roomId)
                .Where(p => p.ConnectionId != senderConnectionId)
                .ToList();

            if (otherPeers.Count == 0)
            {
                _logger.LogDebug($"[SFU Media] No other peers to forward audio to (sender: {senderConnectionId})");
                return;
            }

            _logger.LogInformation($"[SFU Media] Forwarding audio from {senderConnectionId} to {otherPeers.Count} peer(s), payload: {rtpPacket.Payload.Length} bytes");

            foreach (var (connectionId, peerConnection) in otherPeers)
            {
                try
                {
                    // Forward audio using SendAudio with payload length and data
                    peerConnection.SendAudio((uint)rtpPacket.Payload.Length, rtpPacket.Payload);
                    _logger.LogDebug($"[SFU Media] Sent audio to {connectionId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[SFU Media] Error forwarding audio to {connectionId}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[SFU Media] Error forwarding audio from {senderConnectionId}");
        }
    }
}
