using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Orders.DTOs;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Orders.Queries.GetOrderById;

public record GetOrderByIdQuery(Guid OrderId, Guid RequesterProfileId, UserRole RequesterRole) : IRequest<OrderDetailDto>;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDetailDto>
{
    private readonly IApplicationDbContext _context;

    public GetOrderByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<OrderDetailDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.Items).ThenInclude(i => i.Vendor)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            throw new NotFoundException(nameof(Order), request.OrderId);

        var isOwner = request.RequesterRole == UserRole.Customer && order.CustomerId == request.RequesterProfileId;
        var isVendorOnOrder = request.RequesterRole == UserRole.Vendor && order.Items.Any(i => i.VendorId == request.RequesterProfileId);
        var isAdmin = request.RequesterRole is UserRole.Admin or UserRole.SuperAdmin or UserRole.SupportAgent;

        if (!isOwner && !isVendorOnOrder && !isAdmin)
            throw new ForbiddenAccessException("You do not have access to this order.");

        // A Vendor only ever sees their own line items on a shared multi-vendor order, never another vendor's.
        var visibleItems = isVendorOnOrder && !isAdmin && !isOwner
            ? order.Items.Where(i => i.VendorId == request.RequesterProfileId)
            : order.Items;

        var items = visibleItems.Select(i => new OrderItemDto(
            i.Id, i.VendorId, i.Vendor.BusinessName, i.ProductNameSnapshot, i.ColorSnapshot, i.SizeSnapshot,
            i.ImageUrlSnapshot, i.Quantity, i.UnitPrice, i.LineSubtotal, i.Status.ToString())).ToList();

        return new OrderDetailDto(
            order.Id, order.OrderNumber, order.Status.ToString(), order.PaymentStatus.ToString(),
            order.Subtotal, order.DiscountAmount, order.TaxAmount, order.ShippingFee, order.Total,
            order.AppliedCouponCode, order.PlacedAt, order.DeliveredAt, items);
    }
}
