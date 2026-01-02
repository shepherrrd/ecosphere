using Ecosphere.Infrastructure.Data.Models;

namespace Ecosphere.Infrastructure.Infrastructure.Services.Interfaces;

public interface IMeteredTurnService
{
    Task<List<IceServerConfig>> GetMeteredIceServersAsync(long userId);
}
