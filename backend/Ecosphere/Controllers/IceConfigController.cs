using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Services.Interfaces;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosphere.Controllers;

[Route("api/[controller]")]
[ApiController]
public class IceConfigController : ControllerBase
{
    private readonly IStunTurnServer _stunTurnServer;
    private readonly IMeteredTurnService _meteredTurnService;
    private readonly ILogger<IceConfigController> _logger;

    public IceConfigController(
        IStunTurnServer stunTurnServer,
        IMeteredTurnService meteredTurnService,
        ILogger<IceConfigController> logger)
    {
        _stunTurnServer = stunTurnServer;
        _meteredTurnService = meteredTurnService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetIceServers()
    {
        try
        {
            // Get authenticated user ID from JWT token (or use default for testing)
            var userId = User.Identity?.GetProfileId() ?? 1;

            var iceServers = new List<IceServerConfig>();

            // Add local STUN/TURN servers if running
            
                var stunConfig = _stunTurnServer.GetStunConfig();
                var turnConfig = _stunTurnServer.GetTurnConfig(userId);

                _logger.LogInformation($"[ICE] Adding local STUN config with {stunConfig.Urls.Count} URLs");
                _logger.LogInformation($"[ICE] Adding local TURN config with {turnConfig.Urls.Count} URLs");

                iceServers.Add(new IceServerConfig
                {
                    Urls = stunConfig.Urls
                });

                iceServers.Add(new IceServerConfig
                {
                    Urls = turnConfig.Urls,
                    Username = turnConfig.Username,
                    Credential = turnConfig.Credential
                });
            

            // Add Metered TURN servers with cached credentials (expires every 30 minutes)
            var meteredServers = await _meteredTurnService.GetMeteredIceServersAsync(userId);
            if (meteredServers.Any())
            {
                _logger.LogInformation($"[ICE] Adding {meteredServers.Count} Metered ICE servers with cached credentials");
                iceServers.AddRange(meteredServers);
            }

            var response = new BaseResponse<List<IceServerConfig>>(
                true,
                "ICE servers retrieved successfully",
                iceServers
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ICE servers");
            return StatusCode(500, new BaseResponse(false, "Error retrieving ICE server configuration"));
        }
    }

    [HttpGet("status")]
    public IActionResult GetServerStatus()
    {
        var status = new
        {
            StunTurnServerRunning = _stunTurnServer.IsRunning,
            PublicIp = _stunTurnServer.GetPublicIpAddress(),
            StunConfig = _stunTurnServer.GetStunConfig(),
            TurnServerAvailable = _stunTurnServer.IsRunning
        };

        return Ok(new BaseResponse<object>(true, "Server status retrieved", status));
    }
}
