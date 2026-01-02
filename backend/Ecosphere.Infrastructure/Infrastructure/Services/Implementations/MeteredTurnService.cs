using System.Text;
using System.Text.Json;
using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using Ecosphere.Infrastructure.Infrastructure.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecosphere.Infrastructure.Infrastructure.Services.Implementations;

public class MeteredTurnService : IMeteredTurnService
{
    private readonly ILogger<MeteredTurnService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly EcosphereDbContext _context;
    private readonly string _domain;
    private readonly string _secretKey;
    private readonly int _expiryInSeconds;

    // Static semaphore to ensure proper thread limiting to avoid concurrency issues
    private static readonly SemaphoreSlim _credentialFetchSemaphore = new SemaphoreSlim(1, 1);

    public MeteredTurnService(
        ILogger<MeteredTurnService> logger,
        IConfiguration configuration,
        HttpClient httpClient,
        EcosphereDbContext context)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
        _context = context;

        _domain = _configuration["Metered:Domain"]
            ?? throw new InvalidOperationException("Metered:Domain not configured in appsettings.json");
        _secretKey = _configuration["Metered:SecretKey"]
            ?? throw new InvalidOperationException("Metered:SecretKey not configured in appsettings.json");
        _expiryInSeconds = int.Parse(_configuration["Metered:ExpiryInSeconds"] ?? "14400");

        _logger.LogInformation($"[Metered] Initialized with domain: {_domain}, expiry: {_expiryInSeconds}s");
    }

    public async Task<List<IceServerConfig>> GetMeteredIceServersAsync(long userId)
    {
        try
        {
            var cachedCredentials = await _context.TurnCredentials
                .Where(tc => tc.ExpiresAt > DateTimeOffset.UtcNow)
                .FirstOrDefaultAsync();

            if (cachedCredentials != null)
            {
                _logger.LogInformation($"[Metered] Using cached TURN credentials (expires: {cachedCredentials.ExpiresAt})");
                return new List<IceServerConfig>
                {
                    new IceServerConfig
                    {
                        Urls = cachedCredentials.Urls,
                        Username = cachedCredentials.Username,
                        Credential = cachedCredentials.Credential
                    }
                };
            }

            try
            {
                var deletedCount = await _context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM \"TurnCredentials\" WHERE \"ExpiresAt\" <= {0}",
                    DateTimeOffset.UtcNow
                );

                if (deletedCount > 0)
                {
                    _logger.LogInformation($"[Metered] Deleted {deletedCount} expired TURN credentials");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[Metered] Error cleaning up expired credentials (non-critical): {ex.Message}");
            }

            var refreshedCachedCredentials = await _context.TurnCredentials
                .Where(tc => tc.ExpiresAt > DateTimeOffset.UtcNow)
                .FirstOrDefaultAsync();

            if (refreshedCachedCredentials != null)
            {
                _logger.LogInformation($"[Metered] Using refreshed cached TURN credentials (expires: {refreshedCachedCredentials.ExpiresAt})");
                return new List<IceServerConfig>
                {
                    new IceServerConfig
                    {
                        Urls = refreshedCachedCredentials.Urls,
                        Username = refreshedCachedCredentials.Username,
                        Credential = refreshedCachedCredentials.Credential
                    }
                };
            }

            _logger.LogInformation("[Metered] Acquiring semaphore to fetch new TURN credentials");
            await _credentialFetchSemaphore.WaitAsync();
            try
            {
                var finalCheck = await _context.TurnCredentials
                    .Where(tc => tc.ExpiresAt > DateTimeOffset.UtcNow)
                    .FirstOrDefaultAsync();

                if (finalCheck != null)
                {
                    _logger.LogInformation($"[Metered] Another request fetched credentials while waiting (expires: {finalCheck.ExpiresAt})");
                    return new List<IceServerConfig>
                    {
                        new IceServerConfig
                        {
                            Urls = finalCheck.Urls,
                            Username = finalCheck.Username,
                            Credential = finalCheck.Credential
                        }
                    };
                }

                _logger.LogInformation("[Metered] Fetching new TURN credentials from Metered API");

                var createCredentialUrl = $"https://{_domain}/api/v1/turn/credential?secretKey={_secretKey}";
                var requestBody = new
                {
                    expiryInSeconds = _expiryInSeconds,
                    label = $"shared-credential"
                };

                var createResponse = await _httpClient.PostAsync(
                    createCredentialUrl,
                    new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                );

                if (!createResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"[Metered] Failed to create credential: {createResponse.StatusCode}");
                    return new List<IceServerConfig>();
                }

                var createResult = await createResponse.Content.ReadAsStringAsync();
                var credentialResponse = JsonSerializer.Deserialize<MeteredCredentialResponse>(createResult);

                if (credentialResponse == null || string.IsNullOrEmpty(credentialResponse.apiKey))
                {
                    _logger.LogError("[Metered] Invalid credential response");
                    return new List<IceServerConfig>();
                }

                _logger.LogInformation($"[Metered] Created credential, apiKey: {credentialResponse.apiKey}");

                // Step 4: Fetch ICE servers using the apiKey
                var getIceServersUrl = $"https://{_domain}/api/v1/turn/credentials?apiKey={credentialResponse.apiKey}";
                var getResponse = await _httpClient.GetAsync(getIceServersUrl);

                if (!getResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"[Metered] Failed to fetch ICE servers: {getResponse.StatusCode}");
                    return new List<IceServerConfig>();
                }

                var iceServersJson = await getResponse.Content.ReadAsStringAsync();
                var iceServers = JsonSerializer.Deserialize<List<MeteredIceServer>>(iceServersJson);

                if (iceServers == null || !iceServers.Any())
                {
                    _logger.LogError("[Metered] No ICE servers returned");
                    return new List<IceServerConfig>();
                }

                var result = iceServers.Select(server => new IceServerConfig
                {
                    Urls = new List<string> { server.urls },
                    Username = credentialResponse.username,
                    Credential = credentialResponse.password
                }).ToList();

                _logger.LogInformation($"[Metered] Retrieved {result.Count} ICE servers");


                if (result.Any())
                {
                    var firstServer = result.First();
                    _logger.LogInformation($"[Metered] First ICE server URL: {firstServer.Username}, {firstServer.Credential},{firstServer.Urls.FirstOrDefault()}");
                    var allUrls = result.SelectMany(s => s.Urls).ToList();

                    var turnCredentials = new TurnCredentials
                    {
                        Username = firstServer.Username ?? string.Empty,
                        Credential = firstServer.Credential ?? string.Empty,
                        Urls = allUrls, // Save all URLs (STUN and TURN)
                        ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                        TimeCreated = DateTimeOffset.UtcNow,
                        TimeUpdated = DateTimeOffset.UtcNow
                    };

                    _context.TurnCredentials.Add(turnCredentials);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"[Metered] Saved {allUrls.Count} ICE server URLs to DB (expires: {turnCredentials.ExpiresAt})");
                }

                return result;
            }
            finally
            {
                _credentialFetchSemaphore.Release();
                _logger.LogInformation("[Metered] Released credential fetch semaphore");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Metered] Error fetching ICE servers");
            return new List<IceServerConfig>();
        }
    }

    private class MeteredCredentialResponse
    {
        public string username { get; set; } = string.Empty;
        public string password { get; set; } = string.Empty;
        public int expiryInSeconds { get; set; }
        public string label { get; set; } = string.Empty;
        public string apiKey { get; set; } = string.Empty;
    }

    private class MeteredIceServer
    {
        public string urls { get; set; } = string.Empty;
        public string? username { get; set; }
        public string? credential { get; set; }
    }
}
