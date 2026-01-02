using Ecosphere.Infrastructure.Infrastructure.Utilities;

namespace Ecosphere.Infrastructure.Data.Entities;

public class Call : BaseEntity
{
    public string CallUuid { get; set; } = Guid.NewGuid().ToString();
    public CallType CallType { get; set; }
    public CallStatus Status { get; set; }
    public long InitiatorId { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? EndReason { get; set; }
}
