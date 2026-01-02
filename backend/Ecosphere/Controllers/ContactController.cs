using Ecosphere.Application.Contact;
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
    public async Task<IActionResult> GetContacts()
    {
        var result = await _mediator.Send(new GetContactsRequest());
        return Ok(result);
    }

    [HttpGet("requests/pending")]
    public async Task<IActionResult> GetPendingContactRequests()
    {
        var result = await _mediator.Send(new GetPendingContactRequestsRequest());
        return Ok(result);
    }

    [HttpPost("request")]
    public async Task<IActionResult> SendContactRequest([FromBody] AddContactRequest request)
    {
        var result = await _mediator.Send(request);
        return Ok(result);
    }

    [HttpPost("request/{requestId}/approve")]
    public async Task<IActionResult> ApproveContactRequest(long requestId)
    {
        var result = await _mediator.Send(new ApproveContactRequest { RequestId = requestId });
        return Ok(result);
    }

    [HttpPost("request/{requestId}/reject")]
    public async Task<IActionResult> RejectContactRequest(long requestId)
    {
        var result = await _mediator.Send(new RejectContactRequest { RequestId = requestId });
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveContact(long id)
    {
        var result = await _mediator.Send(new RemoveContactRequest { ContactId = id });
        return Ok(result);
    }
}
