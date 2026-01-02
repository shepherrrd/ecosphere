using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ecosphere.Application.Contacts;

public class GetPendingContactRequestsRequest : IRequest<BaseResponse<List<ContactRequestDto>>>
{
}

public class GetPendingContactRequestsHandler : IRequestHandler<GetPendingContactRequestsRequest, BaseResponse<List<ContactRequestDto>>>
{
    private readonly EcosphereDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GetPendingContactRequestsHandler> _logger;

    public GetPendingContactRequestsHandler(
        EcosphereDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GetPendingContactRequestsHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BaseResponse<List<ContactRequestDto>>> Handle(GetPendingContactRequestsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = long.Parse(_httpContextAccessor.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var pendingRequests = await _context.ContactRequests
                .Where(cr => cr.ReceiverId == userId && cr.Status == Ecosphere.Infrastructure.Data.Entities.ContactRequestStatus.Pending)
                .Select(cr => new ContactRequestDto
                {
                    Id = cr.Id,
                    SenderId = cr.SenderId,
                    Sender = _context.Users
                        .Where(u => u.Id == cr.SenderId)
                        .Select(u => new UserInfo
                        {
                            Id = u.Id,
                            UserName = u.UserName!,
                            Email = u.Email!,
                            DisplayName = u.DisplayName,
                            ProfileImageUrl = u.ProfileImageUrl
                        })
                        .FirstOrDefault(),
                    Status = cr.Status.ToString(),
                    CreatedAt = cr.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync(cancellationToken);

            return new BaseResponse<List<ContactRequestDto>>(true, "Pending contact requests retrieved successfully", pendingRequests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET_PENDING_CONTACT_REQUESTS => Something went wrong");
            return new BaseResponse<List<ContactRequestDto>>(false, "An error occurred while retrieving pending contact requests");
        }
    }
}

public class ContactRequestDto
{
    public long Id { get; set; }
    public long SenderId { get; set; }
    public UserInfo? Sender { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}
