namespace Ecosphere.Infrastructure.Data.Models;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public long Expires { get; set; }
    public UserInfo? User { get; set; }
}

public class UserInfo
{
    public long Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public List<string> Roles { get; set; } = new();
}
