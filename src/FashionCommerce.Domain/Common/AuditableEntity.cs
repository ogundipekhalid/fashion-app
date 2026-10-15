namespace FashionCommerce.Domain.Common;

/// <summary>
/// Marks an entity as needing Created/Modified audit stamps.
/// Persistence layer's SaveChangesInterceptor sets these automatically.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Marks an entity as supporting soft delete instead of physical delete.
/// A global EF Core query filter (configured in Persistence) excludes
/// rows where IsDeleted = true from all normal queries.
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

/// <summary>
/// Convenience base class combining BaseEntity + audit fields + soft delete
/// + optimistic concurrency (RowVersion). Most domain entities should
/// inherit from this rather than BaseEntity directly.
/// </summary>
public abstract class AuditableEntity : BaseEntity, IAuditableEntity, ISoftDelete
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>
    /// EF Core concurrency token (SQL Server rowversion/timestamp column).
    /// Prevents lost updates when two requests edit the same row concurrently.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
