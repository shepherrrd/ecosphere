using Ecosphere.Infrastructure.Data.Models;
using Ecosphere.Infrastructure.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosphere.Application.Messages;

public class GetUnreadCountsRequest : IRequest<BaseResponse<Dictionary<long, int>>>
{
    internal long UserId { get; set; }
}

public class GetUnreadCountsRequestHandler : IRequestHandler<GetUnreadCountsRequest, BaseResponse<Dictionary<long, int>>>
{
    private readonly EcosphereDbContext _context;
    private readonly ILogger<GetUnreadCountsRequestHandler> _logger;

    public GetUnreadCountsRequestHandler(EcosphereDbContext context, ILogger<GetUnreadCountsRequestHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BaseResponse<Dictionary<long, int>>> Handle(GetUnreadCountsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate user exists
            var userExists = await _context.Users
                .AnyAsync(u => u.Id == request.UserId, cancellationToken);

            if (!userExists)
            {
                _logger.LogWarning("GetUnreadCounts => User with id {UserId} was not found", request.UserId);
                return BaseResponse<Dictionary<long, int>>.Failure("User account not found. Please login again.");
            }

            // Get unread message counts grouped by sender
            var unreadCounts = await _context.Messages
                .Where(m => m.ReceiverId == request.UserId && !m.IsRead)
                .GroupBy(m => m.SenderId)
                .Select(g => new { SenderId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SenderId, x => x.Count, cancellationToken);

            _logger.LogInformation("GetUnreadCounts => Retrieved unread counts for user {UserId}: {Count} contacts with unread messages",
                request.UserId, unreadCounts.Count);

            return BaseResponse<Dictionary<long, int>>.Success(unreadCounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetUnreadCounts => Error retrieving unread message counts for user {UserId}", request.UserId);
            return BaseResponse<Dictionary<long, int>>.Failure("An error occurred while retrieving unread message counts.");
        }
    }
}
