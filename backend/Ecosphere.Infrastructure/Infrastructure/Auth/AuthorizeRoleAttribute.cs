using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Ecosphere.Infrastructure.Infrastructure.Auth;

/// <summary>
/// Custom authorization attribute that validates user exists and has required role(s)
/// Usage: [AuthorizeRole("Admin")] or [AuthorizeRole("Admin;User")] for multiple roles
/// </summary>
public class AuthorizeRoleAttribute : TypeFilterAttribute
{
    public AuthorizeRoleAttribute(string roles) : base(typeof(AuthorizeRoleFilter))
    {
        Arguments = new object[] { roles };
    }

    private class AuthorizeRoleFilter : IAsyncAuthorizationFilter
    {
        private readonly UserManager<EcosphereUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ILogger<AuthorizeRoleFilter> _logger;
        private readonly string _roles;

        public AuthorizeRoleFilter(
            string roles,
            UserManager<EcosphereUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger<AuthorizeRoleFilter> logger)
        {
            _roles = roles;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            // Split roles by semicolon for multi-role support
            var requiredRoles = _roles.Split(";", StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .ToList();

            // Extract user ID from claims
            long userId = context.HttpContext.User.Identity?.GetProfileId() ?? 0;

            if (userId == 0)
            {
                _logger.LogWarning("Authorization failed: Unable to extract user ID from token");
                context.Result = new UnauthorizedObjectResult(new
                {
                    Status = false,
                    Message = "Invalid or missing authentication token"
                });
                return;
            }

            try
            {
                // Find user (this validates user still exists in database)
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                {
                    _logger.LogWarning("Authorization failed: User {UserId} not found in database (possibly deleted)", userId);
                    context.Result = new UnauthorizedObjectResult(new
                    {
                        Status = false,
                        Message = "User account not found. Please login again."
                    });
                    return;
                }

                // Check if user is locked out
                if (await _userManager.IsLockedOutAsync(user))
                {
                    _logger.LogWarning("Authorization failed: User {UserId} is locked out", userId);
                    context.Result = new UnauthorizedObjectResult(new
                    {
                        Status = false,
                        Message = "User account is locked. Please contact support."
                    });
                    return;
                }

                // Get user's roles
                var userRoles = await _userManager.GetRolesAsync(user);

                // Check if user has any of the required roles
                foreach (var role in requiredRoles)
                {
                    if (userRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Authorization successful: User {UserId} has role {Role}", userId, role);
                        return; // User has required role - authorization successful
                    }
                }

                // User doesn't have any of the required roles
                _logger.LogWarning("Authorization failed: User {UserId} does not have required role(s). Required: [{Roles}], User has: [{UserRoles}]",
                    userId, string.Join(", ", requiredRoles), string.Join(", ", userRoles));

                context.Result = new ForbidResult(); // 403 Forbidden
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authorization for user {UserId}", userId);
                context.Result = new UnauthorizedObjectResult(new
                {
                    Status = false,
                    Message = "An error occurred during authorization"
                });
            }
        }
    }
}
