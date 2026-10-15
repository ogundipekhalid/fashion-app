using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Application.Common.Interfaces;

public interface IJwtTokenService
{
    /// <summary>Generates a short-lived JWT access token. Returns the token string plus its unique jti claim (used to link the paired refresh token).</summary>
    (string AccessToken, string Jti, DateTime ExpiresAt) GenerateAccessToken(Guid identityUserId, Guid profileId, string email, UserRole role);

    /// <summary>Generates a cryptographically random opaque refresh token string (not a JWT itself).</summary>
    string GenerateRefreshTokenValue();
}
