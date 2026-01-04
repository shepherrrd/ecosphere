using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosphere.Infrastructure.Infrastructure.SignalR;

[Authorize]
public class MessageHub : Hub
{
    private readonly EcosphereDbContext _context;
    private readonly ILogger<MessageHub> _logger;

    public MessageHub(EcosphereDbContext context, ILogger<MessageHub> logger)
    {
        _context = context;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.Identity?.GetProfileId();
        if (userId.HasValue)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId.Value}");
            _logger.LogInformation($"[MessageHub] User {userId.Value} connected with ConnectionId: {Context.ConnectionId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.Identity?.GetProfileId();
        if (userId.HasValue)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId.Value}");
            _logger.LogInformation($"[MessageHub] User {userId.Value} disconnected");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Send a direct message to a contact
    /// </summary>
    public async Task SendDirectMessage(long receiverId, string content)
    {
        var senderId = Context.User?.Identity?.GetProfileId();
        if (!senderId.HasValue)
        {
            _logger.LogWarning("[MessageHub] Unauthenticated user attempted to send message");
            return;
        }

        try
        {
            // Validate content
            if (string.IsNullOrWhiteSpace(content))
            {
                await Clients.Caller.SendAsync("MessageError", "Message content cannot be empty");
                return;
            }

            // Check if users are contacts
            var areContacts = await _context.Contacts
                .AnyAsync(c =>
                    (c.UserId == senderId.Value && c.ContactUserId == receiverId) ||
                    (c.UserId == receiverId && c.ContactUserId == senderId.Value));

            if (!areContacts)
            {
                await Clients.Caller.SendAsync("MessageError", "You can only send messages to your contacts");
                return;
            }

            // Save message to database
            var message = new Message
            {
                SenderId = senderId.Value,
                ReceiverId = receiverId,
                Content = content,
                Type = MessageType.Direct,
                SentAt = DateTimeOffset.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Load sender info
            var sender = await _context.Users
                .Where(u => u.Id == senderId.Value)
                .Select(u => new { u.UserName, u.DisplayName })
                .FirstAsync();

            var messageDto = new
            {
                id = message.Id,
                senderId = message.SenderId,
                senderName = sender.DisplayName ?? sender.UserName,
                receiverId = message.ReceiverId,
                content = message.Content,
                sentAt = message.SentAt,
                isRead = message.IsRead,
                type = "Direct"
            };

            // Send to receiver
            await Clients.Group($"user_{receiverId}").SendAsync("ReceiveDirectMessage", messageDto);

            // Confirm to sender
            await Clients.Caller.SendAsync("MessageSent", messageDto);

            _logger.LogInformation($"[MessageHub] Message sent from {senderId.Value} to {receiverId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageHub] Error sending direct message");
            await Clients.Caller.SendAsync("MessageError", "Failed to send message");
        }
    }

    /// <summary>
    /// Send a message in a meeting
    /// </summary>
    public async Task SendMeetingMessage(long meetingId, string content)
    {
        var senderId = Context.User?.Identity?.GetProfileId();
        if (!senderId.HasValue)
        {
            _logger.LogWarning("[MessageHub] Unauthenticated user attempted to send meeting message");
            return;
        }

        try
        {
            // Validate content
            if (string.IsNullOrWhiteSpace(content))
            {
                await Clients.Caller.SendAsync("MessageError", "Message content cannot be empty");
                return;
            }

            // Check if user is in the meeting
            var isParticipant = await _context.MeetingParticipants
                .AnyAsync(mp => mp.MeetingId == meetingId && mp.UserId == senderId.Value);

            if (!isParticipant)
            {
                await Clients.Caller.SendAsync("MessageError", "You must be a participant to send messages");
                return;
            }

            // Save message to database
            var message = new Message
            {
                SenderId = senderId.Value,
                ReceiverId = senderId.Value, // For meeting messages, receiver same as sender
                Content = content,
                MeetingId = meetingId,
                Type = MessageType.Meeting,
                SentAt = DateTimeOffset.UtcNow,
                IsRead = true // Meeting messages are considered "read" for all
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // Load sender info
            var sender = await _context.Users
                .Where(u => u.Id == senderId.Value)
                .Select(u => new { u.UserName, u.DisplayName })
                .FirstAsync();

            var messageDto = new
            {
                id = message.Id,
                senderId = message.SenderId,
                senderName = sender.DisplayName ?? sender.UserName,
                receiverId = message.ReceiverId,
                content = message.Content,
                sentAt = message.SentAt,
                isRead = message.IsRead,
                meetingId = message.MeetingId,
                type = "Meeting"
            };

            // Send to all users in the meeting
            await Clients.Group($"meeting_{meetingId}").SendAsync("ReceiveMeetingMessage", messageDto);

            _logger.LogInformation($"[MessageHub] Meeting message sent by {senderId.Value} in meeting {meetingId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageHub] Error sending meeting message");
            await Clients.Caller.SendAsync("MessageError", "Failed to send meeting message");
        }
    }

    /// <summary>
    /// Mark messages as read
    /// </summary>
    public async Task MarkMessagesAsRead(long contactUserId)
    {
        var userId = Context.User?.Identity?.GetProfileId();
        if (!userId.HasValue)
        {
            return;
        }

        try
        {
            var unreadMessages = await _context.Messages
                .Where(m => m.SenderId == contactUserId && m.ReceiverId == userId.Value && !m.IsRead)
                .ToListAsync();

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
                message.ReadAt = DateTimeOffset.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"[MessageHub] Marked {unreadMessages.Count} messages as read for user {userId.Value}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageHub] Error marking messages as read");
        }
    }
}
