using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ecosphere.Application.Meeting;

public class CreateMeetingRequest : IRequest<BaseResponse<MeetingDto>>
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MaxParticipants { get; set; } = 50;
    public bool IsPublic { get; set; } = false; // Private by default
    public bool RequiresApproval { get; set; } = false;
}

public class CreateMeetingRequestValidator : AbstractValidator<CreateMeetingRequest>
{
    public CreateMeetingRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters");

        RuleFor(x => x.MaxParticipants)
            .GreaterThan(0).WithMessage("Max participants must be greater than 0")
            .LessThanOrEqualTo(1000).WithMessage("Max participants cannot exceed 1000");
    }
}

public class CreateMeetingRequestHandler : IRequestHandler<CreateMeetingRequest, BaseResponse<MeetingDto>>
{
    private readonly EcosphereDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CreateMeetingRequestHandler> _logger;

    public CreateMeetingRequestHandler(
        EcosphereDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CreateMeetingRequestHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BaseResponse<MeetingDto>> Handle(CreateMeetingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = long.Parse(_httpContextAccessor.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            string meetingCode;
            do
            {
                meetingCode = GenerateMeetingCode();
            }
            while (await _context.Meetings.AnyAsync(m => m.MeetingCode == meetingCode, cancellationToken));

            var meeting = new Meeting
            {
                HostId = userId,
                Title = request.Title,
                Description = request.Description,
                MeetingCode = meetingCode,
                IsActive = true,
                IsPublic = request.IsPublic,
                MaxParticipants = request.MaxParticipants,
                RequiresApproval = request.RequiresApproval,
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            };

            await _context.Meetings.AddAsync(meeting, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var host = await _context.Users.FindAsync(userId);

            var meetingDto = new MeetingDto
            {
                Id = meeting.Id,
                HostId = meeting.HostId,
                Host = host != null ? new UserInfo
                {
                    Id = host.Id,
                    UserName = host.UserName!,
                    Email = host.Email!,
                    DisplayName = host.DisplayName,
                    ProfileImageUrl = host.ProfileImageUrl
                } : null,
                Title = meeting.Title,
                Description = meeting.Description,
                MeetingCode = meeting.MeetingCode,
                IsActive = meeting.IsActive,
                IsPublic = meeting.IsPublic,
                MaxParticipants = meeting.MaxParticipants,
                RequiresApproval = meeting.RequiresApproval,
                CreatedAt = meeting.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss")
            };

            _logger.LogInformation($"User {userId} created meeting {meeting.Id} with code {meetingCode}");

            return new BaseResponse<MeetingDto>(true, "Meeting created successfully", meetingDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CREATE_MEETING_REQUEST => Something went wrong");
            return new BaseResponse<MeetingDto>(false, "An error occurred while creating meeting");
        }
    }

    private string GenerateMeetingCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 10)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

public class MeetingDto
{
    public long Id { get; set; }
    public long HostId { get; set; }
    public UserInfo? Host { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string MeetingCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsPublic { get; set; }
    public int MaxParticipants { get; set; }
    public bool RequiresApproval { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
