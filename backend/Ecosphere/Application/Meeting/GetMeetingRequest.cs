using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ecosphere.Application.Meeting;

public class GetMeetingRequest : IRequest<BaseResponse<MeetingDto>>
{
    public string MeetingCode { get; set; } = string.Empty;
}

public class GetMeetingRequestValidator : AbstractValidator<GetMeetingRequest>
{
    public GetMeetingRequestValidator()
    {
        RuleFor(x => x.MeetingCode)
            .NotEmpty().WithMessage("Meeting code is required")
            .Length(10).WithMessage("Meeting code must be exactly 10 characters");
    }
}

public class GetMeetingRequestHandler : IRequestHandler<GetMeetingRequest, BaseResponse<MeetingDto>>
{
    private readonly EcosphereDbContext _context;
    private readonly ILogger<GetMeetingRequestHandler> _logger;

    public GetMeetingRequestHandler(
        EcosphereDbContext context,
        ILogger<GetMeetingRequestHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BaseResponse<MeetingDto>> Handle(GetMeetingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var meeting = await _context.Meetings
                .FirstOrDefaultAsync(m => m.MeetingCode == request.MeetingCode, cancellationToken);

            if (meeting == null)
            {
                return new BaseResponse<MeetingDto>(false, "Meeting not found");
            }

            var host = await _context.Users.FindAsync(meeting.HostId);

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

            return new BaseResponse<MeetingDto>(true, "Meeting retrieved successfully", meetingDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET_MEETING_REQUEST => Something went wrong");
            return new BaseResponse<MeetingDto>(false, "An error occurred while retrieving meeting");
        }
    }
}
