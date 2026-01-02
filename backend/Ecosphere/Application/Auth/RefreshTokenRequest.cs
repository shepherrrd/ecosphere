using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Auth.JWT;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecosphere.Application.Auth;

// Request DTO
public class RefreshTokenRequest : IRequest<BaseResponse<LoginResponse>>
{
    public string RefreshToken { get; set; } = string.Empty;
}

// Validator
public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty();
    }
}

// Handler
public class RefreshTokenRequestHandler : IRequestHandler<RefreshTokenRequest, BaseResponse<LoginResponse>>
{
    private readonly EcosphereDbContext _context;
    private readonly UserManager<EcosphereUser> _userManager;
    private readonly IJwtHandler _jwtHandler;
    private readonly ILogger<RefreshTokenRequestHandler> _logger;

    public RefreshTokenRequestHandler(
        EcosphereDbContext context,
        UserManager<EcosphereUser> userManager,
        IJwtHandler jwtHandler,
        ILogger<RefreshTokenRequestHandler> logger)
    {
        _context = context;
        _userManager = userManager;
        _jwtHandler = jwtHandler;
        _logger = logger;
    }

    public async Task<BaseResponse<LoginResponse>> Handle(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Find refresh token
            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken && !rt.IsRevoked, cancellationToken);

            if (storedToken == null)
                return new BaseResponse<LoginResponse>(false, "Invalid refresh token");

            // Check if token is expired
            if (storedToken.ExpiresAt < DateTimeOffset.UtcNow)
            {
                storedToken.IsRevoked = true;
                await _context.SaveChangesAsync(cancellationToken);
                return new BaseResponse<LoginResponse>(false, "Refresh token expired");
            }

            // Get user
            var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString());
            if (user == null)
                return new BaseResponse<LoginResponse>(false, "User not found");

            // Get roles
            var roles = await _userManager.GetRolesAsync(user);

            // Generate new JWT tokens
            var jwtRequest = new JwtRequest
            {
                UserId = user.Id,
                EmailAddress = user.Email!,
                UserName = user.UserName!,
                UserType = roles.Contains("Admin") ? UserType.Admin : UserType.User,
                Roles = roles.ToList()
            };

            var loginResponse = _jwtHandler.Create(jwtRequest);

            // Revoke old refresh token
            storedToken.IsRevoked = true;

            // Create new refresh token
            var newRefreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = loginResponse.RefreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                IsRevoked = false,
                DeviceToken = storedToken.DeviceToken,
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            };

            await _context.RefreshTokens.AddAsync(newRefreshToken, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Populate user info in response
            loginResponse.User = new UserInfo
            {
                Id = user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                DisplayName = user.DisplayName,
                ProfileImageUrl = user.ProfileImageUrl,
                Roles = roles.ToList()
            };

            _logger.LogInformation($"Token refreshed for user {user.Email}");

            return new BaseResponse<LoginResponse>(true, "Token refreshed successfully", loginResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REFRESH_TOKEN_REQUEST => Something went wrong");
            return new BaseResponse<LoginResponse>(false, "An error occurred while refreshing token");
        }
    }
}
