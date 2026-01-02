using Microsoft.AspNetCore.Identity;

namespace Ecosphere.Infrastructure.Data.Entities;

public class EcosphereUser : IdentityUser<long>
{
    public string? DisplayName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? Bio { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool IsOnline { get; set; }
    public DateTimeOffset? LastSeen { get; set; }
    public DateTimeOffset TimeCreated { get; set; }
    public DateTimeOffset TimeUpdated { get; set; }
}
