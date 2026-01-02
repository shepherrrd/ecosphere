using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecosphere.Infrastructure.Infrastructure.Persistence;

public static class Extensions
{
    public static IServiceCollection RegisterPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<EcosphereDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Ecosphere"))
            .ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        // Caching
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
            options.InstanceName = "EcosphereCache:";
        });

        return services;
    }
}
