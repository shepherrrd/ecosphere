using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ecosphere.Application.Contacts;

public class RemoveContactRequest : IRequest<BaseResponse<string>>
{
    public long ContactId { get; set; }
}

public class RemoveContactRequestHandler : IRequestHandler<RemoveContactRequest, BaseResponse<string>>
{
    private readonly EcosphereDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RemoveContactRequestHandler> _logger;

    public RemoveContactRequestHandler(
        EcosphereDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RemoveContactRequestHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(RemoveContactRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = long.Parse(_httpContextAccessor.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var contact = await _context.Contacts
                .FirstOrDefaultAsync(c => c.Id == request.ContactId && c.UserId == userId, cancellationToken);

            if (contact == null)
                return new BaseResponse<string>(false, "Contact not found");

            _context.Contacts.Remove(contact);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"User {userId} removed contact {contact.ContactUserId}");

            return new BaseResponse<string>(true, "Contact removed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REMOVE_CONTACT_REQUEST => Something went wrong");
            return new BaseResponse<string>(false, "An error occurred while removing contact");
        }
    }
}
