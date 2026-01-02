using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using Ecosphere.Infrastructure.Infrastructure.Utilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ecosphere.Application.Contacts;

public class GetContactsRequest : IRequest<BaseResponse<List<ContactDto>>>
{
}

public class GetContactsRequestHandler : IRequestHandler<GetContactsRequest, BaseResponse<List<ContactDto>>>
{
    private readonly EcosphereDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GetContactsRequestHandler> _logger;

    public GetContactsRequestHandler(
        EcosphereDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GetContactsRequestHandler> logger)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BaseResponse<List<ContactDto>>> Handle(GetContactsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = long.Parse(_httpContextAccessor.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var contacts = await _context.Contacts
                .Where(c => c.UserId == userId)
                .Select(c => new ContactDto
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    ContactUserId = c.ContactUserId,
                    ContactUser = _context.Users
                        .Where(u => u.Id == c.ContactUserId)
                        .Select(u => new UserInfo
                        {
                            Id = u.Id,
                            UserName = u.UserName!,
                            Email = u.Email!,
                            DisplayName = u.DisplayName,
                            ProfileImageUrl = u.ProfileImageUrl
                        })
                        .FirstOrDefault(),
                    CreatedAt = c.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync(cancellationToken);

            return new BaseResponse<List<ContactDto>>(true, "Contacts retrieved successfully", contacts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET_CONTACTS_REQUEST => Something went wrong");
            return new BaseResponse<List<ContactDto>>(false, "An error occurred while retrieving contacts");
        }
    }
}

public class ContactDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ContactUserId { get; set; }
    public UserInfo? ContactUser { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
