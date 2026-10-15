using FashionCommerce.Domain.Common;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Domain.Entities.Identity;

/// <summary>
/// Internal platform staff: SuperAdmin, Admin, or SupportAgent. Kept
/// separate from Customer/Vendor since it has a materially different
/// permission model (see AdminPermission).
/// </summary>
public class Admin : AuditableEntity
{
    public Guid IdentityUserId { get; set; }

    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public AdminRole Role { get; set; } = AdminRole.SupportAgent;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<AdminPermission> Permissions { get; set; } = new List<AdminPermission>();
}

/// <summary>
/// Fine-grained permission grant on top of the coarse AdminRole, so e.g. a
/// SupportAgent can be individually granted "ApproveRefunds" without being
/// promoted to full Admin.
/// </summary>
public class AdminPermission : BaseEntity
{
    public Guid AdminId { get; set; }
    public Admin Admin { get; set; } = default!;

    public string PermissionKey { get; set; } = default!; // e.g. "products.moderate", "refunds.approve"
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public Guid GrantedBy { get; set; }
}
