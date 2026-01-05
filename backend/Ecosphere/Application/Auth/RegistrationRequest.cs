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
public class RegistrationRequest : IRequest<BaseResponse<LoginResponse>>
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? DeviceToken { get; set; }
    public string? DeviceName { get; set; }
}

// Validator
public class RegistrationRequestValidator : AbstractValidator<RegistrationRequest>
{
    public RegistrationRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(50)
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("Username can only contain letters, numbers, and underscores");

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one number")
            .Matches("[!@#$%&^]").WithMessage("Password must contain at least one special character (!@#$%&^)");

        RuleFor(x => x.DisplayName)
            .MaximumLength(100);
    }
}

// Handler
public class RegistrationRequestHandler : IRequestHandler<RegistrationRequest, BaseResponse<LoginResponse>>
{
    private readonly UserManager<EcosphereUser> _userManager;
    private readonly IJwtHandler _jwtHandler;
    private readonly EcosphereDbContext _context;
    private readonly ILogger<RegistrationRequestHandler> _logger;

    public RegistrationRequestHandler(
        UserManager<EcosphereUser> userManager,
        IJwtHandler jwtHandler,
        EcosphereDbContext context,
        ILogger<RegistrationRequestHandler> logger)
    {
        _userManager = userManager;
        _jwtHandler = jwtHandler;
        _context = context;
        _logger = logger;
    }

    public async Task<BaseResponse<LoginResponse>> Handle(RegistrationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Normalize email and username
            var normalizedEmail = request.Email?.Trim().ToLower() ?? string.Empty;
            var normalizedUserName = request.UserName?.Trim().ToLower() ?? string.Empty;

            var validationErrors = new List<string>();

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
            if (existingUser != null)
                validationErrors.Add("Email already registered");

            var existingUserName = await _userManager.FindByNameAsync(normalizedUserName);
            if (existingUserName != null)
                validationErrors.Add("Username already taken");

            // If we have validation errors, return them all
            if (validationErrors.Any())
            {
                return new BaseResponse<LoginResponse>(false, "Registration validation failed", validationErrors);
            }

            // Create new user
            var user = new EcosphereUser
            {
                UserName = normalizedUserName,
                Email = normalizedEmail,
                DisplayName = request.DisplayName ?? normalizedUserName,
                IsOnline = true,
                LastSeen = DateTimeOffset.UtcNow,
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return new BaseResponse<LoginResponse>(false, "Registration failed", errors);
            }

            // Assign default role
            await _userManager.AddToRoleAsync(user, "User");

            // Generate JWT tokens
            var jwtRequest = new JwtRequest
            {
                UserId = user.Id,
                EmailAddress = user.Email,
                UserName = user.UserName,
                UserType = UserType.User,
                Roles = new List<string> { "User" }
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

            // Register device
            if (!string.IsNullOrEmpty(request.DeviceToken))
            {
                var device = new Device
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

            await _context.SaveChangesAsync(cancellationToken);

            // Populate user info in response
            loginResponse.User = new UserInfo
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                DisplayName = user.DisplayName,
                ProfileImageUrl = user.ProfileImageUrl,
                Roles = new List<string> { "User" }
            };

            _logger.LogInformation($"New user registered: {user.Email}");

            return new BaseResponse<LoginResponse>(true, "Registration successful", loginResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REGISTRATION_REQUEST => Something went wrong");
            return new BaseResponse<LoginResponse>(false, "An error occurred during registration");
        }
    }
}
