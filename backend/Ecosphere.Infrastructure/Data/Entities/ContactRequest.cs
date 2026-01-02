namespace Ecosphere.Infrastructure.Data.Entities;

public class ContactRequest : BaseEntity
{
    public long SenderId { get; set; }
    public long ReceiverId { get; set; }
    public ContactRequestStatus Status { get; set; } = ContactRequestStatus.Pending;
}

public enum ContactRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
