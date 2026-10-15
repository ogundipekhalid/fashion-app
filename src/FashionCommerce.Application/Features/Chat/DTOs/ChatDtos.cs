namespace FashionCommerce.Application.Features.Chat.DTOs;

public record ConversationSummaryDto(
    Guid Id, string Type, string Status,
    Guid ProductId, string ProductName, string? ProductImageUrl,
    Guid VendorId, string VendorName,
    Guid CustomerId, string CustomerName,
    string? LastMessagePreview, DateTime? LastMessageAt, int UnreadCount);

public record MessageDto(
    Guid Id, Guid SenderId, string SenderType, string Type,
    string? Content, string? AttachmentUrl, bool IsRead, DateTime SentAt);
