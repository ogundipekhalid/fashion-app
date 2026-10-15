using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Application.Features.Auth.DTOs;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RefreshTokenEntity = FashionCommerce.Domain.Entities.Identity.RefreshToken;

namespace FashionCommerce.Application.Features.Auth.Commands.Login;

public record LoginCommand : IRequest<Result<AuthResponseDto>>
{
    public string Email { get; init; } = default!;
    public string Password { get; init; } = default!;

    /// <summary>"Remember Me" — when true, the refresh token is issued with a longer expiry.</summary>
    public bool RememberMe { get; init; }

    public string? DeviceInfo { get; init; }
}

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    private const int DefaultRefreshTokenDays = 7;
    private const int RememberMeRefreshTokenDays = 30;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IIdentityService identityService,
        IJwtTokenService jwtTokenService,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _context = context;
        _identityService = identityService;
        _jwtTokenService = jwtTokenService;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<Result<AuthResponseDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var check = await _identityService.CheckPasswordAsync(request.Email, request.Password);

        if (check.IsLockedOut)
            return Result<AuthResponseDto>.Failure("Account is temporarily locked due to too many failed login attempts. Try again later.");

        if (check.IsNotAllowed)
            return Result<AuthResponseDto>.Failure("Please verify your email address before logging in.");

        if (!check.Succeeded || check.IdentityUserId is null || check.Role is null)
            return Result<AuthResponseDto>.Failure("Invalid email or password.");

        var (profileId, displayEmail) = await ResolveProfileAsync(check.IdentityUserId.Value, check.Role.Value, cancellationToken);
        if (profileId is null)
            return Result<AuthResponseDto>.Failure("Account profile could not be found.");

        var (accessToken, jti, accessExpiresAt) = _jwtTokenService.GenerateAccessToken(
            check.IdentityUserId.Value, profileId.Value, displayEmail, check.Role.Value);

        var refreshTokenValue = _jwtTokenService.GenerateRefreshTokenValue();
        var refreshExpiryDays = request.RememberMe ? RememberMeRefreshTokenDays : DefaultRefreshTokenDays;

        _context.RefreshTokens.Add(new RefreshTokenEntity
        {
            IdentityUserId = check.IdentityUserId.Value,
            Token = refreshTokenValue,
            JwtId = jti,
            ExpiresAt = _dateTime.UtcNow.AddDays(refreshExpiryDays),
            CreatedByIp = _currentUser.IpAddress,
            DeviceInfo = request.DeviceInfo ?? _currentUser.UserAgent
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<AuthResponseDto>.Success(new AuthResponseDto
        {
            ProfileId = profileId.Value,
            Email = displayEmail,
            Role = check.Role.Value.ToString(),
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessExpiresAt,
            RefreshToken = refreshTokenValue,
            RefreshTokenExpiresAt = _dateTime.UtcNow.AddDays(refreshExpiryDays)
        });
    }

    private async Task<(Guid? ProfileId, string Email)> ResolveProfileAsync(Guid identityUserId, UserRole role, CancellationToken ct)
    {
        switch (role)
        {
            case UserRole.Customer:
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.IdentityUserId == identityUserId, ct);
                return (customer?.Id, customer?.Email ?? string.Empty);
            case UserRole.Vendor:
                var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.IdentityUserId == identityUserId, ct);
                return (vendor?.Id, vendor?.ContactEmail ?? string.Empty);
            case UserRole.Admin:
            case UserRole.SuperAdmin:
            case UserRole.SupportAgent:
                var admin = await _context.Admins.FirstOrDefaultAsync(a => a.IdentityUserId == identityUserId, ct);
                return (admin?.Id, admin?.Email ?? string.Empty);
            default:
                return (null, string.Empty);
        }
    }
}
