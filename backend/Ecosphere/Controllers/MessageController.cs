using Ecosphere.Application.Messages;
using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Auth;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosphere.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class MessageController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<MessageController> _logger;

    public MessageController(IMediator mediator, ILogger<MessageController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("send")]
    [AuthorizeRole("User")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto, CancellationToken cancellationToken)
    {
        var userId = User.Identity?.GetProfileId() ?? 0;

        var request = new SendMessageRequest
        {
            SenderId = userId, // Set from JWT, not from client
            ReceiverId = dto.ReceiverId,
            Content = dto.Content,
            MeetingId = dto.MeetingId
        };

        var result = await _mediator.Send(request, cancellationToken);
        return result.Status ? Ok(result) : BadRequest(result);
    }

    [HttpGet("conversation/{contactUserId}")]
    [AuthorizeRole("User")]
    public async Task<IActionResult> GetConversation(
        long contactUserId,
        [FromQuery] int pageSize = 50,
        [FromQuery] long? beforeMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = User.Identity?.GetProfileId() ?? 0;

        var request = new GetMessagesRequest
        {
            UserId = userId, // Set from JWT, not from client
            ContactUserId = contactUserId,
            PageSize = pageSize,
            BeforeMessageId = beforeMessageId
        };

        var result = await _mediator.Send(request, cancellationToken);
        return result.Status ? Ok(result) : BadRequest(result);
    }

    [HttpGet("meeting/{meetingId}")]
    [AuthorizeRole("User")]
    public async Task<IActionResult> GetMeetingMessages(
        long meetingId,
        [FromQuery] int pageSize = 100,
        [FromQuery] long? beforeMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var userId = User.Identity?.GetProfileId() ?? 0;

        var request = new GetMeetingMessagesRequest
        {
            UserId = userId, // Set from JWT, not from client
            MeetingId = meetingId,
            PageSize = pageSize,
            BeforeMessageId = beforeMessageId
        };

        var result = await _mediator.Send(request, cancellationToken);
        return result.Status ? Ok(result) : BadRequest(result);
    }

    [HttpGet("unread-counts")]
    [AuthorizeRole("User")]
    public async Task<IActionResult> GetUnreadCounts(CancellationToken cancellationToken)
    {
        var userId = User.Identity?.GetProfileId() ?? 0;

        var request = new GetUnreadCountsRequest
        {
            UserId = userId // Set from JWT, not from client
        };

        var result = await _mediator.Send(request, cancellationToken);
        return result.Status ? Ok(result) : BadRequest(result);
    }
}

public record SendMessageDto(long ReceiverId, string Content, long? MeetingId = null);
