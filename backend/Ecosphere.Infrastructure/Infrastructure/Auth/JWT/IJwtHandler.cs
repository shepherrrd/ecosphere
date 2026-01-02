using Ecosphere.Infrastructure.Data.Models;

namespace Ecosphere.Infrastructure.Infrastructure.Auth.JWT;

public interface IJwtHandler
{
    LoginResponse Create(JwtRequest request);
    string GenerateRefreshToken();
}
