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
public class LoginRequest : IRequest<BaseResponse<LoginResponse>>
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DeviceToken { get; set; }
    public string? DeviceName { get; set; }
}

// Validator
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);
    }
}

// Handler
public class LoginRequestHandler : IRequestHandler<LoginRequest, BaseResponse<LoginResponse>>
{
    private readonly UserManager<EcosphereUser> _userManager;
    private readonly SignInManager<EcosphereUser> _signInManager;
    private readonly IJwtHandler _jwtHandler;
    private readonly EcosphereDbContext _context;
    private readonly ILogger<LoginRequestHandler> _logger;

    public LoginRequestHandler(
        UserManager<EcosphereUser> userManager,
        SignInManager<EcosphereUser> signInManager,
        IJwtHandler jwtHandler,
        EcosphereDbContext context,
        ILogger<LoginRequestHandler> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtHandler = jwtHandler;
        _context = context;
        _logger = logger;
    }

    public async Task<BaseResponse<LoginResponse>> Handle(LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Normalize email
            var normalizedEmail = request.Email?.Trim().ToLower() ?? string.Empty;

            var user = await _userManager.FindByEmailAsync(normalizedEmail);
            if (user == null)
                return new BaseResponse<LoginResponse>(false, "Invalid email or password");

            // Check if account is locked
            if (await _userManager.IsLockedOutAsync(user))
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                var remainingTime = lockoutEnd?.Subtract(DateTimeOffset.UtcNow);
                return new BaseResponse<LoginResponse>(false,
                    $"Account is locked. Try again in {remainingTime?.Minutes ?? 0} minutes");
            }

            // Verify password
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);

            if (!result.Succeeded)
            {
                if (result.IsLockedOut)
                    return new BaseResponse<LoginResponse>(false, "Account locked due to multiple failed login attempts");

                return new BaseResponse<LoginResponse>(false, "Invalid email or password");
            }

            // Reset failed login count on successful login
            await _userManager.ResetAccessFailedCountAsync(user);

            // Get user roles
            var roles = await _userManager.GetRolesAsync(user);

            // Generate JWT tokens
            var jwtRequest = new JwtRequest
            {
                UserId = user.Id,
                EmailAddress = user.Email!,
                UserName = user.UserName!,
                UserType = roles.Contains("Admin") ? UserType.Admin : UserType.User,
                Roles = roles.ToList()
            };

            var loginResponse = _jwtHandler.Create(jwtRequest);

            // Save refresh token
            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = loginResponse.RefreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                IsRevoked = false,
                DeviceToken = request.DeviceToken,
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            };

            await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);

            // Register or update device
            if (!string.IsNullOrEmpty(request.DeviceToken))
            {
                var device = await _context.Devices
                    .FirstOrDefaultAsync(d => d.DeviceToken == request.DeviceToken && d.UserId == user.Id, cancellationToken);

                if (device == null)
                {
                    device = new Device
                    {
                        UserId = user.Id,
                        DeviceToken = request.DeviceToken,
                        DeviceName = request.DeviceName ?? "Unknown Device",
                        DeviceType = "Web",
                        IsActive = true,
                        LastActiveAt = DateTimeOffset.UtcNow,
                        TimeCreated = DateTimeOffset.UtcNow,
                        TimeUpdated = DateTimeOffset.UtcNow
                    };
                    await _context.Devices.AddAsync(device, cancellationToken);
                }
                else
                {
                    device.IsActive = true;
                    device.LastActiveAt = DateTimeOffset.UtcNow;
                    device.TimeUpdated = DateTimeOffset.UtcNow;
                }
            }

            // Update user online status
            user.IsOnline = true;
            user.LastSeen = DateTimeOffset.UtcNow;
            user.TimeUpdated = DateTimeOffset.UtcNow;

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

            _logger.LogInformation($"User {user.Email} logged in successfully");

            return new BaseResponse<LoginResponse>(true, "Login successful", loginResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LOGIN_REQUEST => Something went wrong");
            return new BaseResponse<LoginResponse>(false, "An error occurred during login");
        }
    }
}
