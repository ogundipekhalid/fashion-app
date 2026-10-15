namespace FashionCommerce.Application.Features.Auth.DTOs;

/// <summary>Returned by Register/Login/RefreshToken — everything the client needs to start an authenticated session.</summary>
public record AuthResponseDto
{
    public Guid ProfileId { get; init; }
    public string Email { get; init; } = default!;
    public string Role { get; init; } = default!;
    public string AccessToken { get; init; } = default!;
    public DateTime AccessTokenExpiresAt { get; init; }
    public string RefreshToken { get; init; } = default!;
    public DateTime RefreshTokenExpiresAt { get; init; }
}
