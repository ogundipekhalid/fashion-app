using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Auth.DTOs;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RefreshTokenEntity = FashionCommerce.Domain.Entities.Identity.RefreshToken;

namespace FashionCommerce.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Exchanges a valid refresh token for a new access + refresh token pair.
/// Implements ROTATION: the old refresh token is revoked and replaced by
/// the new one. If a token that's already revoked is presented again
/// (token reuse — a sign of theft), the ENTIRE token family for that user
/// is revoked as a precaution.
/// </summary>
public record RefreshTokenCommand : IRequest<Result<AuthResponseDto>>
{
    public string RefreshToken { get; init; } = default!;
}

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public RefreshTokenCommandHandler(
        IApplicationDbContext context,
        IJwtTokenService jwtTokenService,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<AuthResponseDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var existing = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, cancellationToken);

        if (existing is null)
            return Result<AuthResponseDto>.Failure("Invalid refresh token.");

        if (existing.IsRevoked)
        {
            // Reuse of a revoked token: revoke every other active token for this user (family revocation).
            var family = await _context.RefreshTokens
                .Where(t => t.IdentityUserId == existing.IdentityUserId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in family)
                token.Revoke(_currentUser.IpAddress, "Revoked as a precaution: an already-used refresh token was replayed.");

            await _context.SaveChangesAsync(cancellationToken);
            return Result<AuthResponseDto>.Failure("This session is no longer valid. Please log in again.");
        }

        if (existing.IsExpired)
            return Result<AuthResponseDto>.Failure("Refresh token has expired. Please log in again.");

        var identityUserId = existing.IdentityUserId;
        var (profileId, role, email) = await ResolveIdentityAsync(identityUserId, cancellationToken);
        if (profileId is null || role is null)
            return Result<AuthResponseDto>.Failure("Account profile could not be found.");

        var (accessToken, jti, accessExpiresAt) = _jwtTokenService.GenerateAccessToken(identityUserId, profileId.Value, email, role.Value);
        var newRefreshTokenValue = _jwtTokenService.GenerateRefreshTokenValue();

        var newRefreshToken = new RefreshTokenEntity
        {
            IdentityUserId = identityUserId,
            Token = newRefreshTokenValue,
            JwtId = jti,
            ExpiresAt = existing.ExpiresAt, // preserve original "remember me" window rather than resetting it indefinitely
            CreatedByIp = _currentUser.IpAddress,
            DeviceInfo = existing.DeviceInfo
        };

        existing.Revoke(_currentUser.IpAddress, "Rotated on refresh.", newRefreshTokenValue);

        _context.RefreshTokens.Add(newRefreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<AuthResponseDto>.Success(new AuthResponseDto
        {
            ProfileId = profileId.Value,
            Email = email,
            Role = role.Value.ToString(),
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessExpiresAt,
            RefreshToken = newRefreshTokenValue,
            RefreshTokenExpiresAt = newRefreshToken.ExpiresAt
        });
    }

    private async Task<(Guid? ProfileId, UserRole? Role, string Email)> ResolveIdentityAsync(Guid identityUserId, CancellationToken ct)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.IdentityUserId == identityUserId, ct);
        if (customer is not null) return (customer.Id, UserRole.Customer, customer.Email);

        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.IdentityUserId == identityUserId, ct);
        if (vendor is not null) return (vendor.Id, UserRole.Vendor, vendor.ContactEmail);

        var admin = await _context.Admins.FirstOrDefaultAsync(a => a.IdentityUserId == identityUserId, ct);
        if (admin is not null) return (admin.Id, admin.Role == AdminRole.SuperAdmin ? UserRole.SuperAdmin : UserRole.Admin, admin.Email);

        return (null, null, string.Empty);
    }
}
