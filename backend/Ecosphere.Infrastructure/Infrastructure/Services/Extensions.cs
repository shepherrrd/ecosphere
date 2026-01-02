using Ecosphere.Infrastructure.Infrastructure.Services.Implementations;
using Ecosphere.Infrastructure.Infrastructure.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Ecosphere.Infrastructure.Infrastructure.Services;

public static class Extensions
{
    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        services.AddSingleton<IStunTurnServer, StunTurnServer>();
        services.AddHostedService(provider => (StunTurnServer)provider.GetRequiredService<IStunTurnServer>());

        services.AddHttpClient<IMeteredTurnService, MeteredTurnService>();

        services.AddSingleton<ISFUMediaService, SFUMediaService>();

        return services;
    }
}
