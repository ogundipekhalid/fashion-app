using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Chat.DTOs;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Chat.Queries.GetMyConversations;

public record GetMyConversationsQuery(Guid RequesterProfileId, UserRole RequesterRole) : IRequest<List<ConversationSummaryDto>>;

public class GetMyConversationsQueryHandler : IRequestHandler<GetMyConversationsQuery, List<ConversationSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyConversationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ConversationSummaryDto>> Handle(GetMyConversationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Conversations
            .Include(c => c.Product).ThenInclude(p => p.Images)
            .Include(c => c.Customer)
            .Include(c => c.Vendor)
            .Include(c => c.Messages)
            .AsQueryable();

        query = request.RequesterRole == UserRole.Vendor
            ? query.Where(c => c.VendorId == request.RequesterProfileId)
            : query.Where(c => c.CustomerId == request.RequesterProfileId);

        var conversations = await query.OrderByDescending(c => c.LastMessageAt).ToListAsync(cancellationToken);

        return conversations.Select(c => new ConversationSummaryDto(
            c.Id, c.Type.ToString(), c.Status.ToString(),
            c.ProductId, c.Product.Name, c.Product.Images.FirstOrDefault(i => i.IsPrimary)?.Url,
            c.VendorId, c.Vendor.BusinessName,
            c.CustomerId, c.Customer.FullName,
            c.Messages.OrderByDescending(m => m.SentAt).FirstOrDefault(m => !m.IsBlocked)?.Content,
            c.LastMessageAt,
            c.Messages.Count(m => !m.IsRead && !m.IsBlocked && m.SenderId != request.RequesterProfileId)
        )).ToList();
    }
}
