using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ecosphere.Application.Contacts;

public class ApproveContactRequest : IRequest<BaseResponse<string>>
{
    public long RequestId { get; set; }
}

public class ApproveContactRequestHandler : IRequestHandler<ApproveContactRequest, BaseResponse<string>>
{
    private readonly EcosphereDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ApproveContactRequestHandler> _logger;

    public ApproveContactRequestHandler(
        EcosphereDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ApproveContactRequestHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(ApproveContactRequest request, CancellationToken cancellationToken)
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

            // Update request status
            contactRequest.Status = ContactRequestStatus.Approved;
            contactRequest.TimeUpdated = DateTimeOffset.UtcNow;

            // Create bidirectional contact relationships
            var contact1 = new Contact
            {
                UserId = userId,
                ContactUserId = contactRequest.SenderId,
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            };

            var contact2 = new Contact
            {
                UserId = contactRequest.SenderId,
                ContactUserId = userId,
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            };

            await _context.Contacts.AddAsync(contact1, cancellationToken);
            await _context.Contacts.AddAsync(contact2, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"User {userId} approved contact request from {contactRequest.SenderId}");

            return new BaseResponse<string>(true, "Contact request approved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "APPROVE_CONTACT_REQUEST => Something went wrong");
            return new BaseResponse<string>(false, "An error occurred while approving contact request");
        }
    }
}
