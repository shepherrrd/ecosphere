namespace Ecosphere.Infrastructure.Data.Entities;

public class Meeting : BaseEntity
{
    public long HostId { get; set; } 
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string MeetingCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = false;
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int MaxParticipants { get; set; } = 50;
    public bool RequiresApproval { get; set; } = false;
}

public class MeetingParticipant : BaseEntity
{
    public long MeetingId { get; set; }
    public long UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MeetingJoinRequest : BaseEntity
{
    public long MeetingId { get; set; }
    public long UserId { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTimeOffset? RespondedAt { get; set; }
}
