using FashionCommerce.Domain.Common;

namespace FashionCommerce.Domain.Entities.Identity;

/// <summary>
/// Supports refresh token rotation + revocation + device/session tracking.
/// Lives in Domain (not Infrastructure) because "a token can be rotated
/// exactly once, revoking replaces it with a successor, a revoked token's
/// descendants are all revoked too" is a business rule, not plumbing.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid IdentityUserId { get; set; }

    public string Token { get; set; } = default!;
    public string JwtId { get; set; } = default!; // ties this refresh token to the access token that was issued alongside it

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public string? CreatedByIp { get; set; }
    public string? DeviceInfo { get; set; } // user agent / device name, for "Session Tracking" / "Device Tracking"

    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? ReasonRevoked { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;

    public void Revoke(string? byIp, string reason, string? replacedByToken = null)
    {
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = byIp;
        ReasonRevoked = reason;
        ReplacedByToken = replacedByToken;
    }
}
