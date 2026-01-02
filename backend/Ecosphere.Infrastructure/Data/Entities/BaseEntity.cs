namespace Ecosphere.Infrastructure.Data.Entities;

public abstract class BaseEntity
{
    public long Id { get; set; }
    public DateTimeOffset TimeCreated { get; set; }
    public DateTimeOffset TimeUpdated { get; set; }
}
