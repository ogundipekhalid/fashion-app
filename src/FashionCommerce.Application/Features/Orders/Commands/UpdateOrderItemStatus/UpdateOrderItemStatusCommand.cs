using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Orders.Commands.UpdateOrderItemStatus;

/// <summary>
/// A Vendor's per-item fulfilment status is independent of Order.Status —
/// in a multi-vendor order, each vendor ships their own line items on their
/// own timeline. Order.Status (the customer-facing overall lifecycle) is
/// advanced separately once all items reach a consistent state — that
/// aggregation logic belongs to a later background job, not this handler.
/// </summary>
public record UpdateOrderItemStatusCommand : IRequest<Result>, IAuditableRequest
{
    public Guid OrderItemId { get; init; }
    public Guid RequestingVendorId { get; init; }
    public string NewStatus { get; init; } = default!;

    public string Action => "OrderItem.StatusUpdated";
    public string EntityName => "OrderItem";
}

public class UpdateOrderItemStatusCommandValidator : AbstractValidator<UpdateOrderItemStatusCommand>
{
    public UpdateOrderItemStatusCommandValidator()
    {
        RuleFor(x => x.OrderItemId).NotEmpty();
        RuleFor(x => x.NewStatus).Must(s => Enum.TryParse<OrderItemStatus>(s, out _)).WithMessage("Invalid order item status.");
    }
}

public class UpdateOrderItemStatusCommandHandler : IRequestHandler<UpdateOrderItemStatusCommand, Result>
{
    private static readonly Dictionary<OrderItemStatus, OrderItemStatus[]> AllowedTransitions = new()
    {
        [OrderItemStatus.Pending] = new[] { OrderItemStatus.Confirmed, OrderItemStatus.Cancelled },
        [OrderItemStatus.Confirmed] = new[] { OrderItemStatus.Processing, OrderItemStatus.Cancelled },
        [OrderItemStatus.Processing] = new[] { OrderItemStatus.Shipped, OrderItemStatus.Cancelled },
        [OrderItemStatus.Shipped] = new[] { OrderItemStatus.Delivered },
        [OrderItemStatus.Delivered] = Array.Empty<OrderItemStatus>()
    };

    private readonly IApplicationDbContext _context;

    public UpdateOrderItemStatusCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(UpdateOrderItemStatusCommand request, CancellationToken cancellationToken)
    {
        var item = await _context.OrderItems.FirstOrDefaultAsync(i => i.Id == request.OrderItemId, cancellationToken);
        if (item is null)
            throw new NotFoundException(nameof(OrderItem), request.OrderItemId);

        if (item.VendorId != request.RequestingVendorId)
            throw new ForbiddenAccessException("This order item does not belong to you.");

        var newStatus = Enum.Parse<OrderItemStatus>(request.NewStatus);

        if (!AllowedTransitions.TryGetValue(item.Status, out var allowed) || !allowed.Contains(newStatus))
            return Result.Failure($"Cannot move item from '{item.Status}' to '{newStatus}'.");

        item.Status = newStatus;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
