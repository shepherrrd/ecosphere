using System.Security.Claims;
using System.Security.Principal;

namespace Ecosphere.Infrastructure.Infrastructure.Utilities;

public static class IdentityExtensions
{
    public static long? GetProfileId(this IIdentity? identity)
    {
        if (identity is not ClaimsIdentity claimsIdentity)
            return null;

        var userIdClaim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
            userIdClaim = claimsIdentity.FindFirst("sub");

        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            return null;

        return userId;
    }

    public static string? GetUserName(this IIdentity? identity)
    {
        if (identity is not ClaimsIdentity claimsIdentity)
            return null;

        return claimsIdentity.FindFirst("username")?.Value;
    }

    public static string? GetEmail(this IIdentity? identity)
    {
        if (identity is not ClaimsIdentity claimsIdentity)
            return null;

        return claimsIdentity.FindFirst("unique_name")?.Value;
    }
}
