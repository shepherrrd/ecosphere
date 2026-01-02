using Ecosphere.Infrastructure.Infrastructure.Utilities;

namespace Ecosphere.Infrastructure.Data.Entities;

public class CallParticipant : BaseEntity
{
    public long CallId { get; set; }
    public long UserId { get; set; }
    public long? DeviceId { get; set; }
    public CallParticipantStatus Status { get; set; }
    public DateTimeOffset? JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
    public bool IsInitiator { get; set; }
}
