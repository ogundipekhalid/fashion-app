using FashionCommerce.Application.Common.Models;
using FashionCommerce.Domain.Enums;

namespace FashionCommerce.Application.Common.Interfaces;

/// <summary>
/// Abstracts ASP.NET Core Identity (UserManager/SignInManager) behind an
/// interface so Application handlers never reference
/// Microsoft.AspNetCore.Identity directly. Implemented in Infrastructure.
/// </summary>
public interface IIdentityService
{
    /// <summary>Creates the AspNetUsers row and assigns the given role. Returns the new IdentityUserId.</summary>
    Task<Result<Guid>> CreateUserAsync(string email, string password, UserRole role);

    Task<Result> AssignRoleAsync(Guid identityUserId, UserRole role);

    /// <summary>Validates credentials, respecting lockout state (Maximum Login Attempts / Account Lockout requirement).</summary>
    Task<CredentialCheckResult> CheckPasswordAsync(string email, string password);

    Task<Result> ConfirmEmailAsync(Guid identityUserId, string token);
    Task<string> GenerateEmailConfirmationTokenAsync(Guid identityUserId);

    Task<string> GeneratePasswordResetTokenAsync(Guid identityUserId);
    Task<Result> ResetPasswordAsync(Guid identityUserId, string token, string newPassword);

    Task<Result> ChangePasswordAsync(Guid identityUserId, string currentPassword, string newPassword);

    Task<Guid?> FindIdentityUserIdByEmailAsync(string email);
    Task<bool> EmailExistsAsync(string email);

    Task RecordFailedLoginAttemptAsync(Guid identityUserId);
    Task ResetFailedLoginAttemptsAsync(Guid identityUserId);
    Task<bool> IsLockedOutAsync(Guid identityUserId);
}

public class CredentialCheckResult
{
    public bool Succeeded { get; init; }
    public bool IsLockedOut { get; init; }
    public bool IsNotAllowed { get; init; } // e.g. email not yet confirmed
    public Guid? IdentityUserId { get; init; }
    public UserRole? Role { get; init; }

    public static CredentialCheckResult Success(Guid identityUserId, UserRole role) =>
        new() { Succeeded = true, IdentityUserId = identityUserId, Role = role };

    public static CredentialCheckResult Failed() => new() { Succeeded = false };
    public static CredentialCheckResult LockedOut() => new() { Succeeded = false, IsLockedOut = true };
    public static CredentialCheckResult NotAllowed() => new() { Succeeded = false, IsNotAllowed = true };
}
