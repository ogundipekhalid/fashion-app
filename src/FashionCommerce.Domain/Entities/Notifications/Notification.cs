using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Notifications;

public class Notification : AuditableEntity
{
    public Guid UserId { get; set; }
    public UserRole UserRole { get; set; }

    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;

    /// <summary>Optional deep-link payload, e.g. {"orderId": "..."} for client-side navigation.</summary>
    public string? MetadataJson { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? SentAt { get; set; }
    public bool DeliveryFailed { get; set; }

    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
