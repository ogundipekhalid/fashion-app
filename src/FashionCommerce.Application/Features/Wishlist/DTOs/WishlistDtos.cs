namespace FashionCommerce.Application.Features.Wishlist.DTOs;

public record WishlistItemDto(
    Guid Id, Guid ProductId, string ProductName, string Slug,
    Guid? ProductVariantId, string? Color, string? Size,
    decimal CurrentPrice, string? ImageUrl, bool IsInStock, DateTime AddedAt);
