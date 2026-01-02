using Ecosphere.Infrastructure.Infrastructure.Utilities;

namespace Ecosphere.Infrastructure.Data.Models;

public class JwtRequest
{
    public long UserId { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public UserType UserType { get; set; }
    public List<string> Roles { get; set; } = new();
}
