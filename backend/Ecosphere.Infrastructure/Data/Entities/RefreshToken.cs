namespace Ecosphere.Infrastructure.Data.Entities;

public class RefreshToken : BaseEntity
{
    public long UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string? DeviceToken { get; set; } 
}
