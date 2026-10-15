using FashionCommerce.Domain.Common;

namespace FashionCommerce.Domain.Events;

public record ProductApprovedEvent(Guid ProductId, Guid VendorId) : DomainEventBase;

public record ProductRejectedEvent(Guid ProductId, Guid VendorId, string Reason) : DomainEventBase;

public record ProductLowStockEvent(Guid ProductId, Guid ProductVariantId, Guid VendorId, int RemainingStock) : DomainEventBase;

public record ProductReviewAddedEvent(Guid ProductId, Guid ReviewId, Guid CustomerId, int Rating) : DomainEventBase;
