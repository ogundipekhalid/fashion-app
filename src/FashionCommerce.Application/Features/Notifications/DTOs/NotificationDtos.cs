namespace FashionCommerce.Application.Features.Notifications.DTOs;

public record NotificationDto(
    Guid Id, string Type, string Title, string Body,
    string? MetadataJson, bool IsRead, DateTime? SentAt);
