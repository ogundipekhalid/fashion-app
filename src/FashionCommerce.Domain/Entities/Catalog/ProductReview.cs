using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Exceptions;

namespace FashionCommerce.Domain.Entities.Catalog;

public class ProductReview : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;

    /// <summary>Ties the review to the order it was purchased in, to enforce "verified purchase only".</summary>
    public Guid? OrderItemId { get; set; }

    public int Rating { get; set; } // 1-5
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public bool IsVerifiedPurchase { get; set; }

    /// <summary>AI-generated one-line summary, populated by the AI Review Summarizer feature.</summary>
    public string? AiSummary { get; set; }

    public ICollection<ProductReviewImage> Images { get; set; } = new List<ProductReviewImage>();

    public static ProductReview Create(Guid productId, Guid customerId, int rating, string? title, string? comment, Guid? orderItemId)
    {
        if (rating is < 1 or > 5)
            throw new DomainValidationException("Rating must be between 1 and 5.");

        return new ProductReview
        {
            ProductId = productId,
            CustomerId = customerId,
            Rating = rating,
            Title = title,
            Comment = comment,
            OrderItemId = orderItemId,
            IsVerifiedPurchase = orderItemId.HasValue
        };
    }
}

public class ProductReviewImage : BaseEntity
{
    public Guid ProductReviewId { get; set; }
    public ProductReview ProductReview { get; set; } = default!;
    public string Url { get; set; } = default!;
}

/// <summary>Free-text/AI-generated searchable tags, distinct from Category/Brand taxonomy.</summary>
public class ProductTag : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public string Tag { get; set; } = default!;
    public bool IsAiGenerated { get; set; }
}
