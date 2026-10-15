using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Persistence.Identity;
using Microsoft.AspNetCore.Identity;

namespace FashionCommerce.Infrastructure.Authentication;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationIdentityUser> _userManager;

    public IdentityService(UserManager<ApplicationIdentityUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result<Guid>> CreateUserAsync(string email, string password, UserRole role)
    {
        var user = new ApplicationIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
            return Result<Guid>.Failure(createResult.Errors.Select(e => e.Description));

        var roleResult = await _userManager.AddToRoleAsync(user, role.ToString());
        if (!roleResult.Succeeded)
            return Result<Guid>.Failure(roleResult.Errors.Select(e => e.Description));

        return Result<Guid>.Success(user.Id);
    }

    public async Task<Result> AssignRoleAsync(Guid identityUserId, UserRole role)
    {
        var user = await _userManager.FindByIdAsync(identityUserId.ToString());
        if (user is null) return Result.Failure("User not found.");

        var result = await _userManager.AddToRoleAsync(user, role.ToString());
        return result.Succeeded ? Result.Success() : Result.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<CredentialCheckResult> CheckPasswordAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return CredentialCheckResult.Failed();

        if (await _userManager.IsLockedOutAsync(user))
            return CredentialCheckResult.LockedOut();

        if (_userManager.Options.SignIn.RequireConfirmedEmail && !await _userManager.IsEmailConfirmedAsync(user))
            return CredentialCheckResult.NotAllowed();

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user); // increments AccessFailedCount, triggers lockout at the configured threshold
            return CredentialCheckResult.Failed();
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.Select(ParseRole).FirstOrDefault(r => r is not null);

        return primaryRole is null
            ? CredentialCheckResult.Failed()
            : CredentialCheckResult.Success(user.Id, primaryRole.Value);
    }

    public async Task<Result> ConfirmEmailAsync(Guid identityUserId, string token)
    {
        var user = await _userManager.FindByIdAsync(identityUserId.ToString());
        if (user is null) return Result.Failure("User not found.");

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded ? Result.Success() : Result.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<string> GenerateEmailConfirmationTokenAsync(Guid identityUserId)
    {
        var user = await _userManager.FindByIdAsync(identityUserId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        return await _userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    public async Task<string> GeneratePasswordResetTokenAsync(Guid identityUserId)
    {
        var user = await _userManager.FindByIdAsync(identityUserId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<Result> ResetPasswordAsync(Guid identityUserId, string token, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(identityUserId.ToString());
        if (user is null) return Result.Failure("User not found.");

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded ? Result.Success() : Result.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<Result> ChangePasswordAsync(Guid identityUserId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(identityUserId.ToString());
        if (user is null) return Result.Failure("User not found.");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return result.Succeeded ? Result.Success() : Result.Failure(result.Errors.Select(e => e.Description));
    }

    public async Task<Guid?> FindIdentityUserIdByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user?.Id;
    }

    public async Task<bool> EmailExistsAsync(string email) => await _userManager.FindByEmailAsync(email) is not null;

    public async Task RecordFailedLoginAttemptAsync(Guid identityUserId)
    {
        var user = await _userManager.FindByIdAsync(identityUserId.ToString());
        if (user is not null) await _userManager.AccessFailedAsync(user);
    }

    public async Task ResetFailedLoginAttemptsAsync(Guid identityUserId)
    {
        var user = await _userManager.FindByIdAsync(identityUserId.ToString());
        if (user is not null) await _userManager.ResetAccessFailedCountAsync(user);
    }

    public async Task<bool> IsLockedOutAsync(Guid identityUserId)
    {
        var user = await _userManager.FindByIdAsync(identityUserId.ToString());
        return user is not null && await _userManager.IsLockedOutAsync(user);
    }

    private static UserRole? ParseRole(string roleName) => Enum.TryParse<UserRole>(roleName, out var role) ? role : null;
}
