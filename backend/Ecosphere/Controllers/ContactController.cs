using Ecosphere.Application.Contacts;
using Ecosphere.Infrastructure.Infrastructure.Auth;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosphere.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContactController : ControllerBase
{
    private readonly IMediator _mediator;

    public ContactController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [AuthorizeRole("User")]
    public async Task<IActionResult> GetContacts(CancellationToken cancellationToken)
    {
        var userId = User.Identity?.GetProfileId() ?? 0;
        var request = new GetContactsRequest { UserId = userId };
        var result = await _mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("requests/pending")]
    [AuthorizeRole("User")]
    public async Task<IActionResult> GetPendingContactRequests(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPendingContactRequestsRequest(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("request")]
    [AuthorizeRole("User")]
    public async Task<IActionResult> SendContactRequest([FromBody] AddContactRequest request, CancellationToken cancellationToken)
    {
        var userId = User.Identity?.GetProfileId() ?? 0;
        request.UserId = userId;
        var result = await _mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("request/{requestId}/approve")]
    [AuthorizeRole("User")]
    public async Task<IActionResult> ApproveContactRequest(long requestId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ApproveContactRequest { RequestId = requestId }, cancellationToken);
        return Ok(result);
    }

    [HttpPost("request/{requestId}/reject")]
    [AuthorizeRole("User")]
    public async Task<IActionResult> RejectContactRequest(long requestId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RejectContactRequest { RequestId = requestId }, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [AuthorizeRole("User")]
    public async Task<IActionResult> RemoveContact(long id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RemoveContactRequest { ContactId = id }, cancellationToken);
        return Ok(result);
    }
}
