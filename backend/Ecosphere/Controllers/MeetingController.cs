using Ecosphere.Application.Meetings;
using Ecosphere.Infrastructure.Infrastructure.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosphere.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MeetingController : ControllerBase
{
    private readonly IMediator _mediator;

    public MeetingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [AuthorizeRole("User")]
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("join")]
    [AuthorizeRole("User")]
    public async Task<IActionResult> JoinMeeting([FromBody] JoinMeetingRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{meetingCode}")]
    [AuthorizeRole("User")]
    public async Task<IActionResult> GetMeeting(string meetingCode, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMeetingRequest { MeetingCode = meetingCode }, cancellationToken);
        return Ok(result);
    }
}
