using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Notifications.DTOs;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Notifications.Queries.GetMyNotifications;

public record GetMyNotificationsQuery : IRequest<PaginatedList<NotificationDto>>
{
    public Guid UserId { get; init; }
    public UserRole UserRole { get; init; }
    public bool UnreadOnly { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, PaginatedList<NotificationDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyNotificationsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Notifications.Where(n => n.UserId == request.UserId && n.UserRole == request.UserRole);

        if (request.UnreadOnly)
            query = query.Where(n => !n.IsRead);

        var projected = query.OrderByDescending(n => n.SentAt ?? n.CreatedAt)
            .Select(n => new NotificationDto(n.Id, n.Type.ToString(), n.Title, n.Body, n.MetadataJson, n.IsRead, n.SentAt));

        return await PaginatedList<NotificationDto>.CreateAsync(projected, request.PageNumber, request.PageSize);
    }
}
