using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ecosphere.Application.Messages;

public class SendMessageRequest : IRequest<BaseResponse<MessageDto>>
{
    internal long SenderId { get; set; } // Set by controller, not by client
    public long ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public long? MeetingId { get; set; }
}

public class SendMessageHandler : IRequestHandler<SendMessageRequest, BaseResponse<MessageDto>>
{
    private readonly EcosphereDbContext _context;
    private readonly ILogger<SendMessageHandler> _logger;

    public SendMessageHandler(EcosphereDbContext context, ILogger<SendMessageHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BaseResponse<MessageDto>> Handle(SendMessageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Validate sender exists (protects against deleted users with active JWTs)
            var sender = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.SenderId, cancellationToken);

            if (sender is null)
            {
                _logger.LogWarning("SendMessage => Sender with id {SenderId} was not found", request.SenderId);
                return BaseResponse<MessageDto>.Failure("Sender account not found. Please login again.");
            }

            // Step 2: Validate content
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BaseResponse<MessageDto>.Failure("Message content cannot be empty");
            }

            // Step 3: If meeting message, validate meeting exists and user is a participant
            if (request.MeetingId.HasValue)
            {
                var isParticipant = await _context.MeetingParticipants
                    .AnyAsync(mp => mp.MeetingId == request.MeetingId.Value &&
                                   mp.UserId == request.SenderId &&
                                   mp.IsActive, cancellationToken);

                if (!isParticipant)
                {
                    _logger.LogWarning("SendMessage => User {SenderId} is not a participant in meeting {MeetingId}",
                        request.SenderId, request.MeetingId.Value);
                    return BaseResponse<MessageDto>.Failure("Meeting not found or you are not a participant");
                }
            }
            else
            {
                // Step 4: Validate receiver exists and they are contacts for direct messages
                var receiver = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == request.ReceiverId, cancellationToken);

                if (receiver is null)
                {
                    return BaseResponse<MessageDto>.Failure("Receiver not found");
                }

                // Verify they are contacts (bidirectional check)
                var areContacts = await _context.Contacts
                    .AnyAsync(c => (c.UserId == request.SenderId && c.ContactUserId == request.ReceiverId) ||
                                  (c.UserId == request.ReceiverId && c.ContactUserId == request.SenderId),
                             cancellationToken);

                if (!areContacts)
                {
                    _logger.LogWarning("SendMessage => User {SenderId} and {ReceiverId} are not contacts",
                        request.SenderId, request.ReceiverId);
                    return BaseResponse<MessageDto>.Failure("You can only send messages to your contacts");
                }
            }

            // Step 5: Create and save message
            var message = new Message
            {
                SenderId = request.SenderId,
                ReceiverId = request.ReceiverId,
                Content = request.Content,
                MeetingId = request.MeetingId,
                Type = request.MeetingId.HasValue ? MessageType.Meeting : MessageType.Direct,
                SentAt = DateTimeOffset.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync(cancellationToken);

            // Step 6: Build response DTO
            var messageDto = new MessageDto
            {
                Id = message.Id,
                SenderId = message.SenderId,
                SenderName = sender.DisplayName ?? sender.UserName,
                ReceiverId = message.ReceiverId,
                Content = message.Content,
                SentAt = message.SentAt,
                IsRead = message.IsRead,
                ReadAt = message.ReadAt,
                MeetingId = message.MeetingId,
                Type = message.Type.ToString()
            };

            return BaseResponse<MessageDto>.Success(messageDto, "Message sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return BaseResponse<MessageDto>.Failure("Failed to send message");
        }
    }
}

public class MessageDto
{
    public long Id { get; set; }
    public long SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public long ReceiverId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public long? MeetingId { get; set; }
    public string Type { get; set; } = string.Empty;
}
