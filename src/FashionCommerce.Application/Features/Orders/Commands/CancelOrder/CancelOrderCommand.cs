using FashionCommerce.Application.Common.Behaviors;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Orders.Commands.CancelOrder;

public record CancelOrderCommand : IRequest<Result>, IAuditableRequest
{
    public Guid OrderId { get; init; }
    public Guid RequestingCustomerId { get; init; }
    public string Reason { get; init; } = default!;

    public string Action => "Order.Cancelled";
    public string EntityName => "Order";
}

public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Result>
{
    private readonly IApplicationDbContext _context;

    public CancelOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .Include(o => o.Items).ThenInclude(i => i.ProductVariant)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order is null)
            throw new NotFoundException(nameof(Order), request.OrderId);

        if (order.CustomerId != request.RequestingCustomerId)
            throw new ForbiddenAccessException("This order does not belong to you.");

        try
        {
            order.TransitionTo(OrderStatus.Cancelled, request.Reason, request.RequestingCustomerId);
        }
        catch (InvalidOrderStateTransitionException)
        {
            return Result.Failure($"Order cannot be cancelled from its current status ({order.Status}). Once an order has shipped, request a return/refund instead.");
        }

        foreach (var item in order.Items)
        {
            item.ProductVariant.RestockOnCancellationOrRefund(item.Quantity);
            item.Status = OrderItemStatus.Cancelled;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
