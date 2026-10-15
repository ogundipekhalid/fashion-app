using FashionCommerce.Domain.Common;

namespace FashionCommerce.Domain.Entities.Audit;

/// <summary>
/// Generic, append-only audit trail row. Written by an Application-layer
/// MediatR pipeline behavior (see Application/Common/Behaviors) so every
/// command that mutates state is logged without each handler doing it
/// manually.
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? UserRole { get; set; }

    public string Action { get; set; } = default!; // e.g. "Order.StatusChanged", "Product.Approved"
    public string EntityName { get; set; } = default!;
    public Guid EntityId { get; set; }

    /// <summary>JSON diff of changed properties: {"field": {"old": ..., "new": ...}}.</summary>
    public string? ChangesJson { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
