namespace FashionCommerce.Domain.Enums;

public enum ProductStatus
{
    Draft = 1,
    PendingApproval = 2,
    Active = 3,
    OutOfStock = 4,
    Rejected = 5,
    Archived = 6
}

public enum Season
{
    AllSeason = 1,
    Spring = 2,
    Summer = 3,
    Autumn = 4,
    Winter = 5
}

public enum DiscountType
{
    Percentage = 1,
    FixedAmount = 2
}

public enum ProductImageAngle
{
    Front = 1,
    Back = 2,
    Left = 3,
    Right = 4,
    Detail = 5,
    Lifestyle = 6
}

public enum ProductMediaGenerationSource
{
    VendorUploaded = 1,
    AiBackgroundRemoved = 2,
    AiGeneratedAngle = 3,
    AiColorVariant = 4,
    AiEnhanced = 5
}
