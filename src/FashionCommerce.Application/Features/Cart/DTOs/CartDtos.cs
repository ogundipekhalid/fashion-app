namespace FashionCommerce.Application.Features.Cart.DTOs;

public record CartItemDto(
    Guid Id, Guid ProductVariantId, Guid ProductId, string ProductName,
    string Color, string Size, string? ImageUrl,
    decimal UnitPrice, int Quantity, decimal LineTotal,
    bool IsInStock, int AvailableStock);

public record CartDto(
    Guid Id, IReadOnlyList<CartItemDto> Items,
    decimal Subtotal, string? AppliedCouponCode, decimal DiscountAmount, decimal EstimatedTotal);
