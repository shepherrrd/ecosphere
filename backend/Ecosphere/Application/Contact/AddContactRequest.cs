using Ecosphere.Infrastructure.Data.Entities;
using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ecosphere.Application.Contacts;

public class AddContactRequest : IRequest<BaseResponse<string>>
{
    internal long UserId { get; set; } // Set by controller, not by client
    public string Username { get; set; } = string.Empty;
}

public class AddContactRequestValidator : AbstractValidator<AddContactRequest>
{
    public AddContactRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters");
    }
}

public class AddContactRequestHandler : IRequestHandler<AddContactRequest, BaseResponse<string>>
{
    private readonly EcosphereDbContext _context;
    private readonly ILogger<AddContactRequestHandler> _logger;

    public AddContactRequestHandler(
        EcosphereDbContext context,
        ILogger<AddContactRequestHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(AddContactRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Validate current user exists
            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (currentUser == null)
            {
                _logger.LogWarning("AddContact => User with id {UserId} was not found", request.UserId);
                return new BaseResponse<string>(false, "User account not found. Please login again.");
            }

            // Step 2: Cannot add yourself as contact
            if (currentUser.UserName == request.Username)
                return new BaseResponse<string>(false, "You cannot add yourself as a contact");

            // Step 3: Check if contact user exists
            var contactUser = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == request.Username, cancellationToken);
            if (contactUser == null)
                return new BaseResponse<string>(false, "User not found");

            // Step 4: Check if contact request already exists
            var existingRequest = await _context.ContactRequests
                .FirstOrDefaultAsync(cr => cr.SenderId == request.UserId && cr.ReceiverId == contactUser.Id, cancellationToken);

            if (existingRequest != null)
            {
                if (existingRequest.Status == ContactRequestStatus.Pending)
                    return new BaseResponse<string>(false, "Contact request already sent");
                if (existingRequest.Status == ContactRequestStatus.Approved)
                    return new BaseResponse<string>(false, "Already in contacts");
                if (existingRequest.Status == ContactRequestStatus.Rejected)
                {
                    // Allow resending after rejection
                    existingRequest.Status = ContactRequestStatus.Pending;
                    existingRequest.TimeUpdated = DateTimeOffset.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                    return new BaseResponse<string>(true, "Contact request resent");
                }
            }

            // Step 5: Create new contact request
            var contactRequest = new ContactRequest
            {
                SenderId = request.UserId,
                ReceiverId = contactUser.Id,
                Status = ContactRequestStatus.Pending,
                TimeCreated = DateTimeOffset.UtcNow,
                TimeUpdated = DateTimeOffset.UtcNow
            };

            await _context.ContactRequests.AddAsync(contactRequest, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("AddContact => User {UserId} sent contact request to {ContactUserId}",
                request.UserId, contactUser.Id);

            return new BaseResponse<string>(true, "Contact request sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ADD_CONTACT_REQUEST => Something went wrong");
            return new BaseResponse<string>(false, "An error occurred while sending contact request");
        }
    }
}
