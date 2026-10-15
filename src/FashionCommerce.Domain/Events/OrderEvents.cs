using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Events;

public record OrderPlacedEvent(Guid OrderId, Guid CustomerId, decimal Total) : DomainEventBase;

public record OrderStatusChangedEvent(Guid OrderId, Guid CustomerId, OrderStatus From, OrderStatus To) : DomainEventBase;

public record OrderItemRefundedEvent(Guid OrderItemId, Guid OrderId, Guid VendorId, decimal Amount) : DomainEventBase;
