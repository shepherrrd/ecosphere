using Ecosphere.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecosphere.Infrastructure.Infrastructure.Persistence;

public static class SeedData
{
    public static void Seed(this ModelBuilder builder)
    {
        // Seed Roles
        builder.Entity<ApplicationRole>().HasData(
            new ApplicationRole
            {
                Id = 1,
                Name = "User",
                NormalizedName = "USER",
                ConcurrencyStamp = Guid.NewGuid().ToString("D"),
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            },
            new ApplicationRole
            {
                Id = 2,
                Name = "Admin",
                NormalizedName = "ADMIN",
                ConcurrencyStamp = Guid.NewGuid().ToString("D"),
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            }
        );
    }

    private static readonly Func<string> GenerateSecurityStamp = () =>
    {
        var guid = Guid.NewGuid();
        return string.Concat(Array.ConvertAll(guid.ToByteArray(), b => b.ToString("X2")));
    };
}
