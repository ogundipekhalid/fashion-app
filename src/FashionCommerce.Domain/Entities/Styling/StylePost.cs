using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Domain.Exceptions;

namespace FashionCommerce.Domain.Entities.Styling;

/// <summary>
/// A styled photo post: a Customer or Vendor shows how a product looks
/// worn/styled, so other shoppers can see it beyond the vendor's plain
/// product shots. Always anchored to a primary Product, with the option to
/// tag additional products in the same look ("shop this look").
/// </summary>
public class StylePost : AuditableEntity
{
    /// <summary>The Customer.Id or Vendor.Id of the poster, interpreted via AuthorType.</summary>
    public Guid AuthorId { get; set; }
    public StylePostAuthorType AuthorType { get; set; }

    /// <summary>The main product this look is built around.</summary>
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public string? Caption { get; set; }
    public StylePostStatus Status { get; set; } = StylePostStatus.Published;
    public string? RemovalReason { get; set; }

    public int LikeCount { get; private set; }
    public int CommentCount { get; private set; }

    public ICollection<StylePostImage> Images { get; set; } = new List<StylePostImage>();
    public ICollection<StylePostProductTag> TaggedProducts { get; set; } = new List<StylePostProductTag>();
    public ICollection<StylePostLike> Likes { get; set; } = new List<StylePostLike>();
    public ICollection<StylePostComment> Comments { get; set; } = new List<StylePostComment>();

    public static StylePost Create(Guid authorId, StylePostAuthorType authorType, Guid productId, string? caption, IEnumerable<string> imageUrls)
    {
        var urls = imageUrls.ToList();
        if (urls.Count == 0)
            throw new DomainValidationException("A style post needs at least one photo.");

        var post = new StylePost
        {
            AuthorId = authorId,
            AuthorType = authorType,
            ProductId = productId,
            Caption = caption
        };

        var order = 0;
        foreach (var url in urls)
            post.Images.Add(new StylePostImage { StylePostId = post.Id, Url = url, DisplayOrder = order++ });

        return post;
    }

    public void IncrementLikeCount() => LikeCount++;
    public void DecrementLikeCount() => LikeCount = Math.Max(0, LikeCount - 1);
    public void IncrementCommentCount() => CommentCount++;
    public void DecrementCommentCount() => CommentCount = Math.Max(0, CommentCount - 1);

    public void RemoveByModeration(string reason)
    {
        Status = StylePostStatus.RemovedByAdmin;
        RemovalReason = reason;
    }
}

public class StylePostImage : BaseEntity
{
    public Guid StylePostId { get; set; }
    public StylePost StylePost { get; set; } = default!;

    public string Url { get; set; } = default!;
    public int DisplayOrder { get; set; }
}

/// <summary>Additional products tagged in the same look, beyond the primary Product (e.g. "shoes also shown").</summary>
public class StylePostProductTag : BaseEntity
{
    public Guid StylePostId { get; set; }
    public StylePost StylePost { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
}

public class StylePostLike : BaseEntity
{
    public Guid StylePostId { get; set; }
    public StylePost StylePost { get; set; } = default!;

    public Guid UserId { get; set; }
    public UserRole UserRole { get; set; }
    public DateTime LikedAt { get; set; } = DateTime.UtcNow;
}

public class StylePostComment : AuditableEntity
{
    public Guid StylePostId { get; set; }
    public StylePost StylePost { get; set; } = default!;

    public Guid UserId { get; set; }
    public UserRole UserRole { get; set; }
    public string Content { get; set; } = default!;

    public static StylePostComment Create(Guid stylePostId, Guid userId, UserRole userRole, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new DomainValidationException("Comment cannot be empty.");

        return new StylePostComment { StylePostId = stylePostId, UserId = userId, UserRole = userRole, Content = content.Trim() };
    }
}
