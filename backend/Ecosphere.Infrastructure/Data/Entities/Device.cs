namespace Ecosphere.Infrastructure.Data.Entities;

public class Device : BaseEntity
{
    public long UserId { get; set; }
    public string DeviceToken { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset? LastActiveAt { get; set; }
    public string? ConnectionId { get; set; }
}
