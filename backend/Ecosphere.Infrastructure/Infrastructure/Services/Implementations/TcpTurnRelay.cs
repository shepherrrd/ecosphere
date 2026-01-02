using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecosphere.Infrastructure.Infrastructure.Services.Implementations;

/// <summary>
/// Simple TCP-based TURN relay server for forwarding through Dev Tunnel
/// </summary>
public class TcpTurnRelay
{
    private readonly ILogger<TcpTurnRelay> _logger;
    private readonly IConfiguration _configuration;
    private TcpListener? _tcpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenerTask;
    private int _tcpPort = 5479; 
    private readonly ConcurrentDictionary<string, TcpRelaySession> _sessions = new();

    public bool IsRunning { get; private set; }

    public TcpTurnRelay(ILogger<TcpTurnRelay> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<bool> StartAsync(int tcpPort = 5479)
    {
        try
        {
            _tcpPort = tcpPort;
            _cancellationTokenSource = new CancellationTokenSource();

            // Start TCP listener
            _tcpListener = new TcpListener(IPAddress.Any, _tcpPort);
            _tcpListener.Start();

            _listenerTask = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            _logger.LogInformation($"[TCP-TURN] Relay server started on TCP port {_tcpPort}");

            IsRunning = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TCP-TURN] Failed to start TCP relay server");
            return false;
        }
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _tcpListener != null)
        {
            try
            {
                var client = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
                _logger.LogInformation($"[TCP-TURN] Client connected from {client.Client.RemoteEndPoint}");

                // Handle each client in a separate task
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TCP-TURN] Error accepting client");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid().ToString();
        var remoteEndPoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

        try
        {
            using (client)
            {
                var stream = client.GetStream();
                var buffer = new byte[8192];

                _logger.LogInformation($"[TCP-TURN] Session {sessionId} established with {remoteEndPoint}");

                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead == 0)
                    {
                        _logger.LogInformation($"[TCP-TURN] Session {sessionId} closed by client");
                        break;
                    }

                    // Simple TURN relay logic:
                    // 1. Check if this is a STUN binding request
                    // 2. If so, respond with the client's reflexive address
                    // 3. Otherwise, relay the data to the peer

                    if (IsStunBindingRequest(buffer, bytesRead))
                    {
                        _logger.LogInformation($"[TCP-TURN] STUN Binding Request from {remoteEndPoint}");
                        var response = CreateStunBindingResponse(client.Client.RemoteEndPoint as IPEndPoint);
                        await stream.WriteAsync(response, 0, response.Length, cancellationToken);
                    }
                    else
                    {
                        _logger.LogDebug($"[TCP-TURN] Relaying {bytesRead} bytes for session {sessionId}");
                        await stream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation($"[TCP-TURN] Session {sessionId} cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[TCP-TURN] Error handling session {sessionId}");
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
            _logger.LogInformation($"[TCP-TURN] Session {sessionId} ended");
        }
    }

    private bool IsStunBindingRequest(byte[] data, int length)
    {
        // STUN message type for Binding Request is 0x0001
        // STUN magic cookie is 0x2112A442
        if (length < 20) return false;

        return data[0] == 0x00 && data[1] == 0x01 && // Binding Request
               data[4] == 0x21 && data[5] == 0x12 &&
               data[6] == 0xA4 && data[7] == 0x42;   // Magic Cookie
    }

    private byte[] CreateStunBindingResponse(IPEndPoint? clientEndPoint)
    {
        if (clientEndPoint == null)
            return Array.Empty<byte>();

        // Simplified STUN Binding Response
        // Format: Message Type (2) + Length (2) + Magic Cookie (4) + Transaction ID (12) + Attributes
        var response = new byte[68]; // Fixed size for simplicity

        // Message Type: Binding Success Response (0x0101)
        response[0] = 0x01;
        response[1] = 0x01;

        // Message Length (not including 20-byte header)
        response[2] = 0x00;
        response[3] = 0x30; // 48 bytes of attributes

        // Magic Cookie
        response[4] = 0x21;
        response[5] = 0x12;
        response[6] = 0xA4;
        response[7] = 0x42;

        var random = new Random();
        for (int i = 8; i < 20; i++)
        {
            response[i] = (byte)random.Next(256);
        }

        // XOR-MAPPED-ADDRESS attribute (0x0020)
        response[20] = 0x00;
        response[21] = 0x20;

        // Attribute length
        response[22] = 0x00;
        response[23] = 0x08; // 8 bytes

        // Address family (IPv4)
        response[24] = 0x00;
        response[25] = 0x01;

        // Port (XOR with magic cookie)
        var port = (ushort)clientEndPoint.Port;
        response[26] = (byte)((port >> 8) ^ 0x21);
        response[27] = (byte)((port & 0xFF) ^ 0x12);

        // IP Address (XOR with magic cookie)
        var ipBytes = clientEndPoint.Address.GetAddressBytes();
        response[28] = (byte)(ipBytes[0] ^ 0x21);
        response[29] = (byte)(ipBytes[1] ^ 0x12);
        response[30] = (byte)(ipBytes[2] ^ 0xA4);
        response[31] = (byte)(ipBytes[3] ^ 0x42);

        return response;
    }

    public async Task StopAsync()
    {
        try
        {
            _cancellationTokenSource?.Cancel();

            if (_listenerTask != null)
                await _listenerTask;

            _tcpListener?.Stop();

            IsRunning = false;
            _logger.LogInformation("[TCP-TURN] Relay server stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TCP-TURN] Error stopping relay server");
        }
    }

    private class TcpRelaySession
    {
        public TcpClient Client { get; set; } = null!;
        public string SessionId { get; set; } = string.Empty;
        public DateTimeOffset Created { get; set; }
    }
}
