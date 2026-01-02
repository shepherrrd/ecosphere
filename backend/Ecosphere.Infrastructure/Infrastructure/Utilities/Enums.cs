using System.ComponentModel;

namespace Ecosphere.Infrastructure.Infrastructure.Utilities;

public enum CallType
{
    [Description("Audio")]
    Audio = 1,
    [Description("Video")]
    Video = 2,
    [Description("Conference")]
    Conference = 3
}

public enum CallStatus
{
    [Description("Initiating")]
    Initiating = 1,
    [Description("Ringing")]
    Ringing = 2,
    [Description("Active")]
    Active = 3,
    [Description("Ended")]
    Ended = 4,
    [Description("Missed")]
    Missed = 5,
    [Description("Rejected")]
    Rejected = 6,
    [Description("Busy")]
    Busy = 7,
    [Description("Failed")]
    Failed = 8
}

public enum CallParticipantStatus
{
    [Description("Ringing")]
    Ringing = 1,
    [Description("Joined")]
    Joined = 2,
    [Description("Left")]
    Left = 3,
    [Description("Missed")]
    Missed = 4,
    [Description("Rejected")]
    Rejected = 5
}

public enum UserType
{
    [Description("User")]
    User = 1,
    [Description("Admin")]
    Admin = 2
}
