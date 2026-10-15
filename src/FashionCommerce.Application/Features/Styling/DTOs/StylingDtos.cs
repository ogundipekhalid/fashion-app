namespace FashionCommerce.Application.Features.Styling.DTOs;

public record StylePostCommentDto(Guid Id, Guid UserId, string UserRole, string AuthorDisplayName, string Content, DateTime CreatedAt);

public record TaggedProductDto(Guid ProductId, string Name);

public record StylePostDto(
    Guid Id, Guid AuthorId, string AuthorType, string AuthorDisplayName, string? AuthorImageUrl,
    Guid ProductId, string ProductName,
    string? Caption,
    IReadOnlyList<string> ImageUrls,
    IReadOnlyList<TaggedProductDto> TaggedProducts,
    int LikeCount, bool LikedByRequester, int CommentCount,
    DateTime CreatedAt);
