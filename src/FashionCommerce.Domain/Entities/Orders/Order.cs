using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Entities.Payments;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Domain.Events;
using FashionCommerce.Domain.Exceptions;

namespace FashionCommerce.Domain.Entities.Orders;

/// <summary>
/// Order aggregate root. In a multi-vendor marketplace a single Order can
/// contain items from multiple vendors (see OrderItem.VendorId); Order
/// itself tracks the overall customer-facing lifecycle while each
/// OrderItem tracks its own vendor-fulfilment lifecycle independently.
/// </summary>
public class Order : AuditableEntity
{
    public string OrderNumber { get; set; } = default!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    public Guid ShippingAddressId { get; set; }
    public Guid BillingAddressId { get; set; }

    public OrderStatus Status { get; private set; } = OrderStatus.PendingPayment;
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Pending;

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }

    public string? AppliedCouponCode { get; set; }

    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.PendingPayment] = new[] { OrderStatus.Paid, OrderStatus.Failed, OrderStatus.Cancelled },
        [OrderStatus.Paid] = new[] { OrderStatus.Processing, OrderStatus.Cancelled },
        [OrderStatus.Processing] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = new[] { OrderStatus.OutForDelivery },
        [OrderStatus.OutForDelivery] = new[] { OrderStatus.Delivered },
        [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>(),
        [OrderStatus.Failed] = new[] { OrderStatus.PendingPayment }
    };

    public void TransitionTo(OrderStatus newStatus, string? note = null, Guid? changedBy = null)
    {
        if (Status == newStatus) return;

        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidOrderStateTransitionException(Status.ToString(), newStatus.ToString());

        var previous = Status;
        Status = newStatus;

        StatusHistory.Add(new OrderStatusHistory
        {
            OrderId = Id,
            FromStatus = previous,
            ToStatus = newStatus,
            Note = note,
            ChangedBy = changedBy,
            ChangedAt = DateTime.UtcNow
        });

        if (newStatus == OrderStatus.Delivered) DeliveredAt = DateTime.UtcNow;
        if (newStatus == OrderStatus.Cancelled) CancelledAt = DateTime.UtcNow;

        AddDomainEvent(new OrderStatusChangedEvent(Id, CustomerId, previous, newStatus));
    }

    public void MarkPaymentSuccessful()
    {
        PaymentStatus = PaymentStatus.Successful;
        TransitionTo(OrderStatus.Paid);
        AddDomainEvent(new OrderPlacedEvent(Id, CustomerId, Total));
    }

    public void MarkPaymentFailed()
    {
        PaymentStatus = PaymentStatus.Failed;
        TransitionTo(OrderStatus.Failed);
    }
}

public class OrderStatusHistory : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public OrderStatus FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public string? Note { get; set; }
    public Guid? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
