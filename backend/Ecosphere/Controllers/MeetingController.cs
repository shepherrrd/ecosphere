using Ecosphere.Application.Meeting;
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
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingRequest request)
    {
        var result = await _mediator.Send(request);
        return Ok(result);
    }

    [HttpPost("join")]
    public async Task<IActionResult> JoinMeeting([FromBody] JoinMeetingRequest request)
    {
        var result = await _mediator.Send(request);
        return Ok(result);
    }

    [HttpGet("{meetingCode}")]
    public async Task<IActionResult> GetMeeting(string meetingCode)
    {
        var result = await _mediator.Send(new GetMeetingRequest { MeetingCode = meetingCode });
        return Ok(result);
    }
}
