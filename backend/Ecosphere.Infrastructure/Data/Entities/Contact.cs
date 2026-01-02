namespace Ecosphere.Infrastructure.Data.Entities;

public class Contact : BaseEntity
{
    public long UserId { get; set; }
    public long ContactUserId { get; set; }
    public string? ContactName { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsFavorite { get; set; }
}
