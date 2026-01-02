using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ecosphere.Application.Meeting;

public class JoinMeetingRequest : IRequest<BaseResponse<string>>
{
    public string MeetingCode { get; set; } = string.Empty;
}

public class JoinMeetingRequestValidator : AbstractValidator<JoinMeetingRequest>
{
    public JoinMeetingRequestValidator()
    {
        RuleFor(x => x.MeetingCode)
            .NotEmpty().WithMessage("Meeting code is required")
            .Length(10).WithMessage("Meeting code must be 10 characters");
    }
}

public class JoinMeetingRequestHandler : IRequestHandler<JoinMeetingRequest, BaseResponse<string>>
{
    private readonly EcosphereDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<JoinMeetingRequestHandler> _logger;

    public JoinMeetingRequestHandler(
        EcosphereDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<JoinMeetingRequestHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(JoinMeetingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = long.Parse(_httpContextAccessor.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var meeting = await _context.Meetings
                .FirstOrDefaultAsync(m => m.MeetingCode == request.MeetingCode, cancellationToken);

            if (meeting == null)
                return new BaseResponse<string>(false, "Meeting not found");

            if (!meeting.IsActive)
                return new BaseResponse<string>(false, "Meeting is not active");

            // Check if user is the host (hosts can always join their own meetings)
            bool isHost = meeting.HostId == userId;

            // Check if user is already a participant
            var existingParticipant = await _context.MeetingParticipants
                .FirstOrDefaultAsync(mp => mp.MeetingId == meeting.Id && mp.UserId == userId && mp.IsActive, cancellationToken);

            if (existingParticipant != null)
                return new BaseResponse<string>(false, "You are already in this meeting");

            // Check participant limit
            var activeParticipantsCount = await _context.MeetingParticipants
                .CountAsync(mp => mp.MeetingId == meeting.Id && mp.IsActive, cancellationToken);

            if (activeParticipantsCount >= meeting.MaxParticipants)
                return new BaseResponse<string>(false, "Meeting is full");

            // If meeting is private and user is not the host, create join request
            if (!meeting.IsPublic && !isHost)
            {
                // Check if there's already a pending join request
                var existingRequest = await _context.MeetingJoinRequests
                    .FirstOrDefaultAsync(mjr => mjr.MeetingId == meeting.Id && mjr.UserId == userId && mjr.Status == "Pending", cancellationToken);

                if (existingRequest != null)
                    return new BaseResponse<string>(false, "You already have a pending join request for this meeting");

                // Create join request
                var joinRequest = new MeetingJoinRequest
                {
                    MeetingId = meeting.Id,
                    UserId = userId,
                    Status = "Pending",
                    TimeCreated = DateTimeOffset.UtcNow,
                    TimeUpdated = DateTimeOffset.UtcNow
                };

                await _context.MeetingJoinRequests.AddAsync(joinRequest, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation($"User {userId} requested to join private meeting {meeting.Id}");

                return new BaseResponse<string>(true, "Join request sent. Waiting for host approval.");
            }

            // If meeting is public or user is host, allow direct join
            var participant = new MeetingParticipant
            {
                MeetingId = meeting.Id,
                UserId = userId,
                JoinedAt = DateTimeOffset.UtcNow,
                IsActive = true,
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            };

            await _context.MeetingParticipants.AddAsync(participant, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"User {userId} joined meeting {meeting.Id}");

            return new BaseResponse<string>(true, "Successfully joined meeting");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JOIN_MEETING_REQUEST => Something went wrong");
            return new BaseResponse<string>(false, "An error occurred while joining meeting");
        }
    }
}
