using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FashionCommerce.Application.Features.Auth.Commands.Logout;

public record LogoutCommand : IRequest<Result>
{
    public string RefreshToken { get; init; } = default!;

    /// <summary>If true, revokes every active refresh token for the user (logout of all devices).</summary>
    public bool AllDevices { get; init; }
}

public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public LogoutCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, cancellationToken);

        if (token is null)
            return Result.Failure("Invalid refresh token.");

        if (request.AllDevices)
        {
            var allActive = await _context.RefreshTokens
                .Where(t => t.IdentityUserId == token.IdentityUserId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var t in allActive)
                t.Revoke(_currentUser.IpAddress, "User logged out of all devices.");
        }
        else if (!token.IsRevoked)
        {
            token.Revoke(_currentUser.IpAddress, "User logged out.");
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
