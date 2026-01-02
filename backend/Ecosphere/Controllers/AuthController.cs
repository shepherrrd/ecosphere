using System.Net;
using Ecosphere.Application.Auth;
using Ecosphere.Infrastructure.Data.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Ecosphere.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("Register")]
    [ProducesResponseType(typeof(BaseResponse<LoginResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegistrationRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        if (!response.Status) return BadRequest(response);
        return Ok(response);
    }

    [HttpPost("Login")]
    [ProducesResponseType(typeof(BaseResponse<LoginResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        if (!response.Status) return BadRequest(response);
        return Ok(response);
    }

    [HttpPost("RefreshToken")]
    [ProducesResponseType(typeof(BaseResponse<LoginResponse>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ErrorResponse), (int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(request, cancellationToken);
        return response.Status ? Ok(response) : Unauthorized(response);
    }
}
