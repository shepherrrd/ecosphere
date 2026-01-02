using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ecosphere.Application.Contacts;

public class RejectContactRequest : IRequest<BaseResponse<string>>
{
    public long RequestId { get; set; }
}

public class RejectContactRequestHandler : IRequestHandler<RejectContactRequest, BaseResponse<string>>
{
    private readonly EcosphereDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RejectContactRequestHandler> _logger;

    public RejectContactRequestHandler(
        EcosphereDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RejectContactRequestHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(RejectContactRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = long.Parse(_httpContextAccessor.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var contactRequest = await _context.ContactRequests
                .FirstOrDefaultAsync(cr => cr.Id == request.RequestId && cr.ReceiverId == userId, cancellationToken);

            if (contactRequest == null)
                return new BaseResponse<string>(false, "Contact request not found");

            if (contactRequest.Status != ContactRequestStatus.Pending)
                return new BaseResponse<string>(false, "Contact request is not pending");

            contactRequest.Status = ContactRequestStatus.Rejected;
            contactRequest.TimeUpdated = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"User {userId} rejected contact request from {contactRequest.SenderId}");

            return new BaseResponse<string>(true, "Contact request rejected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REJECT_CONTACT_REQUEST => Something went wrong");
            return new BaseResponse<string>(false, "An error occurred while rejecting contact request");
        }
    }
}
