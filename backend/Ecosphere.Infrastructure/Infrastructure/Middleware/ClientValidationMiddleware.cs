using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ecosphere.Infrastructure.Infrastructure.Middleware;


public class ClientValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ClientValidationMiddleware> _logger;

    private static readonly string[] ExcludedPaths =
    {
        "/swagger",
        "/health",
        "/hubs/", 
        "/_framework",
        "/api/auth/login",
        "/api/auth/register"
    };

    public ClientValidationMiddleware(RequestDelegate next, ILogger<ClientValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip validation for excluded paths
        var requestPath = context.Request.Path.Value ?? string.Empty;
        if (ExcludedPaths.Any(path => requestPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Get client ID from header
        var clientId = context.Request.Headers["X-ClientId"].ToString();

        // Get IP address (handle proxies)
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            ipAddress = context.Request.Headers["X-Forwarded-For"].ToString().Split(',').FirstOrDefault()?.Trim();
        }

        // Validate both client ID and IP are present
        if (string.IsNullOrEmpty(clientId))
        {
            _logger.LogWarning("Request blocked: Missing X-ClientId header. Path: {Path}, IP: {IP}",
                context.Request.Path, ipAddress ?? "Unknown");

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                Status = false,
                Message = "Unable to verify request sender."
            });
            return;
        }

        if (string.IsNullOrEmpty(ipAddress))
        {
            _logger.LogWarning("Request blocked: Unable to determine IP address. Path: {Path}, ClientId: {ClientId}",
                context.Request.Path, clientId);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                Status = false,
                Message = "Unable to verify request origin."
            });
            return;
        }

        context.Items["ClientId"] = clientId;
        context.Items["IpAddress"] = ipAddress;

        _logger.LogDebug("Request validated - ClientId: {ClientId}, IP: {IP}, Path: {Path}",
            clientId, ipAddress, context.Request.Path);

        await _next(context);
    }
}
