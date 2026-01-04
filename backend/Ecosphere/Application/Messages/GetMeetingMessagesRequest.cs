using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ecosphere.Application.Messages;

public class GetMeetingMessagesRequest : IRequest<BaseResponse<List<MessageDto>>>
{
    internal long UserId { get; set; } // Set by controller, not by client
    public long MeetingId { get; set; }
    public int PageSize { get; set; } = 100;
    public long? BeforeMessageId { get; set; }
}

public class GetMeetingMessagesHandler : IRequestHandler<GetMeetingMessagesRequest, BaseResponse<List<MessageDto>>>
{
    private readonly EcosphereDbContext _context;
    private readonly ILogger<GetMeetingMessagesHandler> _logger;

    public GetMeetingMessagesHandler(EcosphereDbContext context, ILogger<GetMeetingMessagesHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BaseResponse<List<MessageDto>>> Handle(GetMeetingMessagesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Validate user exists
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("GetMeetingMessages => User with id {UserId} was not found", request.UserId);
                return BaseResponse<List<MessageDto>>.Failure("User account not found. Please login again.");
            }

            // Step 2: Validate user is a participant in the meeting
            var isParticipant = await _context.MeetingParticipants
                .AnyAsync(mp => mp.MeetingId == request.MeetingId &&
                               mp.UserId == request.UserId &&
                               mp.IsActive, cancellationToken);

            if (!isParticipant)
            {
                _logger.LogWarning("GetMeetingMessages => User {UserId} is not a participant in meeting {MeetingId}",
                    request.UserId, request.MeetingId);
                return BaseResponse<List<MessageDto>>.Failure("Meeting not found or you are not a participant");
            }

            // Step 3: Query messages
            var query = _context.Messages
                .Where(m => m.Type == MessageType.Meeting && m.MeetingId == request.MeetingId);

            if (request.BeforeMessageId.HasValue)
            {
                query = query.Where(m => m.Id < request.BeforeMessageId.Value);
            }

            var messages = await query
                .OrderByDescending(m => m.SentAt)
                .Take(request.PageSize)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    SenderName = m.Sender.DisplayName ?? m.Sender.UserName,
                    ReceiverId = m.ReceiverId,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsRead = m.IsRead,
                    ReadAt = m.ReadAt,
                    MeetingId = m.MeetingId,
                    Type = m.Type.ToString()
                })
                .ToListAsync(cancellationToken);

            // Reverse to show oldest first
            messages.Reverse();

            return BaseResponse<List<MessageDto>>.Success(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving meeting messages");
            return BaseResponse<List<MessageDto>>.Failure("Failed to retrieve meeting messages");
        }
    }
}
