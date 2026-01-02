using SIPSorcery.Net;

namespace Ecosphere.Infrastructure.Infrastructure.Services.Interfaces;

public interface ISFUMediaService
{
    /// <summary>
    /// Create a new peer connection for a client
    /// </summary>
    Task<RTCPeerConnection> CreatePeerConnectionAsync(string connectionId, string roomId);

    /// <summary>
    /// Remove a peer connection
    /// </summary>
    Task RemovePeerConnectionAsync(string connectionId);

    /// <summary>
    /// Get a peer connection by connection ID
    /// </summary>
    RTCPeerConnection? GetPeerConnection(string connectionId);

    /// <summary>
    /// Get all peer connections in a room
    /// </summary>
    IEnumerable<(string ConnectionId, RTCPeerConnection PeerConnection)> GetPeerConnectionsInRoom(string roomId);
}
