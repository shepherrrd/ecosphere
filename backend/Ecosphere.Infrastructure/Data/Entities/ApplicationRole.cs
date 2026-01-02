using Microsoft.AspNetCore.Identity;

namespace Ecosphere.Infrastructure.Data.Entities;

public class ApplicationRole : IdentityRole<long>
{
    public DateTimeOffset TimeCreated { get; set; }
    public DateTimeOffset TimeUpdated { get; set; }
}
