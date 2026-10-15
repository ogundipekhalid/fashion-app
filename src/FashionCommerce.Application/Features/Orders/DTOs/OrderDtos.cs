namespace FashionCommerce.Application.Features.Orders.DTOs;

public record OrderItemDto(
    Guid Id, Guid VendorId, string VendorName, string ProductName, string Color, string Size,
    string? ImageUrl, int Quantity, decimal UnitPrice, decimal LineSubtotal, string Status);

public record OrderSummaryDto(
    Guid Id, string OrderNumber, string Status, string PaymentStatus,
    decimal Total, int ItemCount, DateTime PlacedAt);

public record OrderDetailDto(
    Guid Id, string OrderNumber, string Status, string PaymentStatus,
    decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal ShippingFee, decimal Total,
    string? AppliedCouponCode, DateTime PlacedAt, DateTime? DeliveredAt,
    IReadOnlyList<OrderItemDto> Items);

public record PlaceOrderResultDto(Guid OrderId, string OrderNumber, decimal Total, string? PaymentAuthorizationUrl, string? PaymentReference);
