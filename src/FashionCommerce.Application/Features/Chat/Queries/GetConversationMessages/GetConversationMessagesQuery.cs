using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Chat.DTOs;
using FashionCommerce.Domain.Entities.Chat;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Chat.Queries.GetConversationMessages;

public record GetConversationMessagesQuery(Guid ConversationId, Guid RequesterProfileId, UserRole RequesterRole) : IRequest<List<MessageDto>>;

public class GetConversationMessagesQueryHandler : IRequestHandler<GetConversationMessagesQuery, List<MessageDto>>
{
    private readonly IApplicationDbContext _context;

    public GetConversationMessagesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<MessageDto>> Handle(GetConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _context.Conversations.FirstOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);
        if (conversation is null)
            throw new NotFoundException(nameof(Conversation), request.ConversationId);

        var isParticipant = request.RequesterRole switch
        {
            UserRole.Customer => conversation.CustomerId == request.RequesterProfileId,
            UserRole.Vendor => conversation.VendorId == request.RequesterProfileId,
            UserRole.Admin or UserRole.SuperAdmin or UserRole.SupportAgent => true,
            _ => false
        };

        if (!isParticipant)
            throw new ForbiddenAccessException("You do not have access to this conversation.");

        var isAdmin = request.RequesterRole is UserRole.Admin or UserRole.SuperAdmin or UserRole.SupportAgent;

        var messages = await _context.Messages
            .Where(m => m.ConversationId == request.ConversationId)
            .Where(m => isAdmin || !m.IsBlocked) // blocked messages are only ever visible to Admin's review trail
            .OrderBy(m => m.SentAt)
            .ToListAsync(cancellationToken);

        return messages.Select(m => new MessageDto(
            m.Id, m.SenderId, m.SenderType.ToString(), m.Type.ToString(),
            m.Content, m.AttachmentUrl, m.IsRead, m.SentAt)).ToList();
    }
}
