namespace Ecosphere.Infrastructure.Data.Entities;

public class Message : BaseEntity
{
    public long SenderId { get; set; }
    public EcosphereUser Sender { get; set; } = null!;

    public long ReceiverId { get; set; }
    public EcosphereUser Receiver { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsRead { get; set; } = false;

    public DateTimeOffset? ReadAt { get; set; }

    // Optional: For meeting chat messages
    public long? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    // Message type: Direct or Meeting
    public MessageType Type { get; set; } = MessageType.Direct;
}

public enum MessageType
{
    Direct = 0,      // Peer-to-peer message
    Meeting = 1      // Meeting group message
}
