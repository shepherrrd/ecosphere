namespace Ecosphere.Infrastructure.Data.Entities;

public class TurnCredentials : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Credential { get; set; } = string.Empty;
    public List<string> Urls { get; set; } = new();
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}
