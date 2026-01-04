using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecosphere.Infrastructure.Infrastructure.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
        services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));

        services.Configure<ClientRateLimitOptions>(options =>
        {
            configuration.GetSection("ClientRateLimiting").Bind(options);
            options.ClientIdHeader = "X-ClientId"; 
        });

        services.AddInMemoryRateLimiting();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

        services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

        services.AddSingleton<IClientResolveContributor, ClientResolveContributor>();

        return services;
    }

    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        app.UseIpRateLimiting();

        app.UseClientRateLimiting();

        return app;
    }
}


public class ClientResolveContributor : IClientResolveContributor
{
    public Task<string> ResolveClientAsync(HttpContext httpContext)
    {
        var clientId = httpContext.Items["ClientId"] as string ?? httpContext.Request.Headers["X-ClientId"].ToString();
        var ipAddress = httpContext.Items["IpAddress"] as string ?? httpContext.Connection.RemoteIpAddress?.ToString();

        if (string.IsNullOrEmpty(ipAddress) && httpContext.Request.Headers.ContainsKey("X-Forwarded-For"))
        {
            ipAddress = httpContext.Request.Headers["X-Forwarded-For"].ToString().Split(',').FirstOrDefault()?.Trim();
        }

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(ipAddress))
        {
            return Task.FromResult("invalid-client");
        }

        return Task.FromResult($"{ipAddress}:{clientId}");
    }
}
