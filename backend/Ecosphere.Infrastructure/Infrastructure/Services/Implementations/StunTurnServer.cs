using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Ecosphere.Infrastructure.Infrastructure.Services.Interfaces;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosphere.Infrastructure.Infrastructure.Services.Implementations;

public class StunTurnServer : IStunTurnServer, IHostedService
{
    private readonly ILogger<StunTurnServer> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private UdpClient? _stunServer;
    private UdpClient? _turnServer;
    private TcpTurnRelay? _tcpTurnRelay;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _stunTask;
    private Task? _turnTask;
    private int _stunPort = 3478;
    private int _turnPort = 3479;
    private int _tcpTurnPort = 5479; 
    private string _publicIp = string.Empty;

    private readonly ConcurrentDictionary<string, TurnAllocation> _turnAllocations = new();

    private readonly string _turnSharedSecret;
    private readonly int _credentialValidityHours;

    private List<string> _stunUrls = new();
    private List<string> _turnUrls = new();
    private string? _turnUsername;
    private string? _turnCredential;

    public bool IsRunning { get; private set; }

    public StunTurnServer(ILogger<StunTurnServer> logger, ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _configuration = configuration;

        // Load TURN configuration from appsettings.json
        _turnSharedSecret = _configuration["StunTurn:SharedSecret"]
            ?? throw new InvalidOperationException("StunTurn:SharedSecret not configured in appsettings.json");

        _credentialValidityHours = int.Parse(_configuration["StunTurn:CredentialValidityHours"] ?? "24");

        // Load configured STUN URLs from appsettings.json
        var stunUrls = _configuration.GetSection("StunTurn:StunUrls").Get<List<string>>();
        if (stunUrls != null && stunUrls.Any())
        {
            _stunUrls = stunUrls;
            _logger.LogInformation($"[STUN/TURN] Loaded {_stunUrls.Count} STUN URLs from configuration");
            foreach (var url in _stunUrls)
            {
                _logger.LogInformation($"[STUN/TURN]   - {url}");
            }
        }

        var turnUrls = _configuration.GetSection("StunTurn:TurnUrls").Get<List<string>>();
        if (turnUrls != null && turnUrls.Any())
        {
            _turnUrls = turnUrls;
            _logger.LogInformation($"[STUN/TURN] Loaded {_turnUrls.Count} TURN URLs from configuration");
            foreach (var url in _turnUrls)
            {
                _logger.LogInformation($"[STUN/TURN]   - {url}");
            }
        }

        _turnUsername = _configuration["StunTurn:TurnUsername"];
        _turnCredential = _configuration["StunTurn:TurnCredential"];

        if (!string.IsNullOrEmpty(_turnUsername) && !string.IsNullOrEmpty(_turnCredential))
        {
            _logger.LogInformation($"[STUN/TURN] Using configured TURN credentials (username: {_turnUsername})");
        }

        _logger.LogInformation($"[STUN/TURN] Credential validity: {_credentialValidityHours} hours");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await StartAsync(_stunPort, _turnPort);
    }

    public async Task<bool> StartAsync(int stunPort = 3478, int turnPort = 3479)
    {
        try
        {
            _stunPort = stunPort;
            _turnPort = turnPort;
            _cancellationTokenSource = new CancellationTokenSource();

            // Get server's local IP address (the one clients will connect to)
            _publicIp = GetLocalIpAddress();
            _logger.LogInformation($"[STUN/TURN] Using IP address: {_publicIp}");

            // Start STUN server
            _stunServer = new UdpClient(_stunPort);
            _stunTask = Task.Run(() => RunStunServerAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            _logger.LogInformation($"[STUN] Server started on port {_stunPort}");

            // Start TURN server
            _turnServer = new UdpClient(_turnPort);
            _turnTask = Task.Run(() => RunTurnServerAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
            _logger.LogInformation($"[TURN] Server started on UDP port {_turnPort}");

            // Start TCP TURN relay
            _tcpTurnPort = int.Parse(_configuration["StunTurn:TcpTurnPort"] ?? "5479");
            _tcpTurnRelay = new TcpTurnRelay(_loggerFactory.CreateLogger<TcpTurnRelay>(), _configuration);
            await _tcpTurnRelay.StartAsync(_tcpTurnPort);
            _logger.LogInformation($"[TURN] TCP relay started on port {_tcpTurnPort}");

            IsRunning = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STUN/TURN] Failed to start servers");
            return false;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopAsync();
    }

    public async Task StopAsync()
    {
        try
        {
            _cancellationTokenSource?.Cancel();

            if (_stunTask != null)
                await _stunTask;

            if (_turnTask != null)
                await _turnTask;

            _stunServer?.Close();
            _turnServer?.Close();

            if (_tcpTurnRelay != null)
                await _tcpTurnRelay.StopAsync();

            IsRunning = false;
            _logger.LogInformation("[STUN/TURN] All servers stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[STUN/TURN] Error stopping servers");
        }
    }

    private async Task RunStunServerAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[STUN] Server listening for requests...");

        while (!cancellationToken.IsCancellationRequested && _stunServer != null)
        {
            try
            {
                var result = await _stunServer.ReceiveAsync(cancellationToken);
                _ = Task.Run(() => HandleStunRequestAsync(result.Buffer, result.RemoteEndPoint), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[STUN] Error receiving data");
            }
        }
    }

    private async Task HandleStunRequestAsync(byte[] data, IPEndPoint remoteEndPoint)
    {
        try
        {
            var request = StunMessage.Parse(data);
            if (request == null)
            {
                _logger.LogWarning($"[STUN] Invalid request from {remoteEndPoint}");
                return;
            }

            if (request.MessageType == StunMessageType.BindingRequest)
            {
               // _logger.LogInformation($"[STUN] Binding request from {remoteEndPoint.Address}:{remoteEndPoint.Port}");

                var response = new StunMessage
                {
                    MessageType = StunMessageType.BindingResponse,
                    TransactionId = request.TransactionId,
                    Attributes = new List<StunAttribute>
                    {
                        StunAttribute.CreateXorMappedAddress(remoteEndPoint, request.TransactionId),
                        StunAttribute.CreateMappedAddress(remoteEndPoint),
                        StunAttribute.CreateSoftware("Ecosphere STUN Server 1.0")
                    }
                };

                var responseBytes = response.ToBytes();
                await _stunServer!.SendAsync(responseBytes, responseBytes.Length, remoteEndPoint);

               // _logger.LogInformation($"[STUN] Sent binding response to {remoteEndPoint}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[STUN] Error handling request from {remoteEndPoint}");
        }
    }

    private async Task RunTurnServerAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[TURN] Server listening for requests...");

        while (!cancellationToken.IsCancellationRequested && _turnServer != null)
        {
            try
            {
                var result = await _turnServer.ReceiveAsync(cancellationToken);
                _ = Task.Run(() => HandleTurnRequestAsync(result.Buffer, result.RemoteEndPoint), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TURN] Error receiving data");
            }
        }
    }

    private async Task HandleTurnRequestAsync(byte[] data, IPEndPoint remoteEndPoint)
    {
        try
        {
            var request = StunMessage.Parse(data);
            if (request == null) return;

            var allocationKey = $"{remoteEndPoint.Address}:{remoteEndPoint.Port}";
            if (!_turnAllocations.ContainsKey(allocationKey))
            {
                var allocation = new TurnAllocation
                {
                    ClientEndPoint = remoteEndPoint,
                    RelayEndPoint = new IPEndPoint(IPAddress.Parse(_publicIp), GetAvailableRelayPort()),
                    Created = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
                };

                _turnAllocations.TryAdd(allocationKey, allocation);
                //_logger.LogInformation($"[TURN] Created allocation for {remoteEndPoint} -> {allocation.RelayEndPoint}");
            }

            var response = new StunMessage
            {
                MessageType = StunMessageType.BindingResponse,
                TransactionId = request.TransactionId,
                Attributes = new List<StunAttribute>
                {
                    StunAttribute.CreateXorMappedAddress(remoteEndPoint, request.TransactionId),
                    StunAttribute.CreateSoftware("Ecosphere TURN Server 1.0")
                }
            };

            var responseBytes = response.ToBytes();
            await _turnServer!.SendAsync(responseBytes, responseBytes.Length, remoteEndPoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[TURN] Error handling request from {remoteEndPoint}");
        }
    }

    public string GetPublicIpAddress()
    {
        return _publicIp;
    }

    public StunServerConfig GetStunConfig()
    {
        return new StunServerConfig
        {
            Host = _publicIp,
            Port = _stunPort,
            Urls = _stunUrls
        };
    }

    public TurnServerConfig GetTurnConfig(long userId, int validityHours = 0)
    {
        string username;
        string credential;

        // If external TURN credentials are configured (e.g., Metered), use them
        if (!string.IsNullOrEmpty(_turnUsername) && !string.IsNullOrEmpty(_turnCredential))
        {
            username = _turnUsername;
            credential = _turnCredential;
        }
        else
        {
            if (validityHours <= 0)
                validityHours = _credentialValidityHours;

            var creds = GenerateTemporaryCredentials(userId, validityHours);
            username = creds.username;
            credential = creds.credential;
        }

      
        return new TurnServerConfig
        {
            Host = _publicIp,
            Port = _turnPort,
            Urls = _turnUrls,
            Username = username,
            Credential = credential
        };
    }

    /// <summary>
    /// Generate time-limited TURN credentials using HMAC
    /// Format: username = "timestamp:userId", credential = HMAC-SHA1(username, sharedSecret)
    /// </summary>
    private (string username, string credential) GenerateTemporaryCredentials(long userId, int validityHours)
    {
        var expirationTime = DateTimeOffset.UtcNow.AddHours(validityHours).ToUnixTimeSeconds();

        
        var username = $"{expirationTime}:{userId}";

        using var hmac = new System.Security.Cryptography.HMACSHA1(
            System.Text.Encoding.UTF8.GetBytes(_turnSharedSecret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(username));
        var credential = Convert.ToBase64String(hash);

        return (username, credential);
    }

    /// <summary>
    /// Verify if TURN credentials are valid (called by TURN server when client connects)
    /// </summary>
    private bool ValidateTurnCredentials(string username, string providedCredential)
    {
        try
        {
            // Parse username: "timestamp:userId"
            var parts = username.Split(':');
            if (parts.Length != 2) return false;

            if (!long.TryParse(parts[0], out var expirationTimestamp))
                return false;

            var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expirationTimestamp);
            if (DateTimeOffset.UtcNow > expirationTime)
            {
                _logger.LogWarning($"[TURN] Expired credentials for username: {username}");
                return false;
            }

            using var hmac = new System.Security.Cryptography.HMACSHA1(
                System.Text.Encoding.UTF8.GetBytes(_turnSharedSecret));
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(username));
            var expectedCredential = Convert.ToBase64String(hash);

            
            if (expectedCredential != providedCredential)
            {
                _logger.LogWarning($"[TURN] Invalid credential for username: {username}");
                return false;
            }

            _logger.LogInformation($"[TURN] Valid credentials for username: {username}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TURN] Error validating credentials");
            return false;
        }
    }

    private string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    private int GetAvailableRelayPort()
    { 
        //could use a port pool here incase of conflicts
        return new Random().Next(49152, 65535);
    }

    private class TurnAllocation
    {
        public IPEndPoint ClientEndPoint { get; set; } = null!;
        public IPEndPoint RelayEndPoint { get; set; } = null!;
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
