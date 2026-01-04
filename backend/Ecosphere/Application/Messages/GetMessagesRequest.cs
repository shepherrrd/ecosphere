using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ecosphere.Application.Messages;

public class GetMessagesRequest : IRequest<BaseResponse<List<MessageDto>>>
{
    internal long UserId { get; set; } // Set by controller, not by client
    public long ContactUserId { get; set; }
    public int PageSize { get; set; } = 50;
    public long? BeforeMessageId { get; set; }
}

public class GetMessagesHandler : IRequestHandler<GetMessagesRequest, BaseResponse<List<MessageDto>>>
{
    private readonly EcosphereDbContext _context;
    private readonly ILogger<GetMessagesHandler> _logger;

    public GetMessagesHandler(EcosphereDbContext context, ILogger<GetMessagesHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BaseResponse<List<MessageDto>>> Handle(GetMessagesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Validate user exists
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user is null)
            {
                _logger.LogWarning("GetMessages => User with id {UserId} was not found", request.UserId);
                return BaseResponse<List<MessageDto>>.Failure("User account not found. Please login again.");
            }

            // Step 2: Validate contact exists
            var contact = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.ContactUserId, cancellationToken);

            if (contact is null)
            {
                return BaseResponse<List<MessageDto>>.Failure("Contact user not found");
            }

            // Step 3: Verify they are contacts (bidirectional check)
            var areContacts = await _context.Contacts
                .AnyAsync(c => (c.UserId == request.UserId && c.ContactUserId == request.ContactUserId) ||
                              (c.UserId == request.ContactUserId && c.ContactUserId == request.UserId),
                         cancellationToken);

            if (!areContacts)
            {
                _logger.LogWarning("GetMessages => User {UserId} and {ContactUserId} are not contacts",
                    request.UserId, request.ContactUserId);
                return BaseResponse<List<MessageDto>>.Failure("You can only view messages with your contacts");
            }

            // Step 4: Query messages
            var query = _context.Messages
                .Where(m => m.Type == MessageType.Direct)
                .Where(m =>
                    (m.SenderId == request.UserId && m.ReceiverId == request.ContactUserId) ||
                    (m.SenderId == request.ContactUserId && m.ReceiverId == request.UserId)
                );

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
            _logger.LogError(ex, "Error retrieving messages");
            return BaseResponse<List<MessageDto>>.Failure("Failed to retrieve messages");
        }
    }
}
