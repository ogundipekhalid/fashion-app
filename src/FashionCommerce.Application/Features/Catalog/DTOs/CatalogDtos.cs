namespace FashionCommerce.Application.Features.Catalog.DTOs;

public record ProductVariantDto(
    Guid Id, string Color, string? ColorHexCode, string Size, string Sku,
    decimal EffectivePrice, int StockQuantity, bool IsInStock, bool IsLowStock);

public record ProductImageDto(Guid Id, string Url, string? AltText, string Angle, bool IsPrimary, int DisplayOrder);

public record ProductSummaryDto(
    Guid Id, string Name, string Slug, decimal CurrentPrice, decimal? BasePrice,
    decimal AverageRating, int ReviewCount, string? PrimaryImageUrl, string VendorName, string Status);

public record ProductDetailDto(
    Guid Id, string Name, string Slug, string Description,
    Guid VendorId, string VendorName,
    Guid CategoryId, string CategoryName,
    string? BrandName,
    decimal BasePrice, decimal? SalePrice, decimal CurrentPrice,
    string? Material, string Season, string? TargetGender,
    decimal AverageRating, int ReviewCount, int TotalSold,
    string Status,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<string> Tags);

public record CategoryDto(Guid Id, string Name, string Slug, string? ImageUrl, Guid? ParentCategoryId, List<CategoryDto> SubCategories);

public record BrandDto(Guid Id, string Name, string Slug, string? LogoUrl);

public record ProductReviewDto(Guid Id, string CustomerName, int Rating, string? Title, string? Comment, bool IsVerifiedPurchase, DateTime CreatedAt);
