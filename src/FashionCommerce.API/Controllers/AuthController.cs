using Asp.Versioning;
using FashionCommerce.Application.Features.Auth.Commands.ChangePassword;
using FashionCommerce.Application.Features.Auth.Commands.ForgotPassword;
using FashionCommerce.Application.Features.Auth.Commands.Login;
using FashionCommerce.Application.Features.Auth.Commands.Logout;
using FashionCommerce.Application.Features.Auth.Commands.RefreshToken;
using FashionCommerce.Application.Features.Auth.Commands.RegisterCustomer;
using FashionCommerce.Application.Features.Auth.Commands.RegisterVendor;
using FashionCommerce.Application.Features.Auth.Commands.ResetPassword;
using FashionCommerce.Application.Features.Auth.Commands.VerifyEmail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
public class AuthController : ApiControllerBase
{
    /// <summary>Creates a new Customer account and sends an email verification link.</summary>
    [HttpPost("register/customer")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterCustomer(RegisterCustomerCommand command) =>
        FromResult(await Mediator.Send(command), "Registration successful. Please check your email to verify your account.");

    /// <summary>Creates a new Vendor account (pending verification) and sends an email verification link.</summary>
    [HttpPost("register/vendor")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterVendor(RegisterVendorCommand command) =>
        FromResult(await Mediator.Send(command), "Registration successful. Please check your email to verify your account.");

    /// <summary>Authenticates a Customer, Vendor, or Admin and issues an access + refresh token pair.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginCommand command) =>
        FromResult(await Mediator.Send(command), "Login successful.");

    /// <summary>Exchanges a valid refresh token for a new access + refresh token pair (rotation).</summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command) =>
        FromResult(await Mediator.Send(command));

    /// <summary>Revokes the given refresh token (or every token for the user, if AllDevices is true).</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(LogoutCommand command) =>
        FromResult(await Mediator.Send(command), "Logged out successfully.");

    /// <summary>Always returns success, regardless of whether the email exists, to avoid account enumeration.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordCommand command) =>
        FromResult(await Mediator.Send(command), "If an account with that email exists, a password reset link has been sent.");

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command) =>
        FromResult(await Mediator.Send(command), "Password has been reset successfully.");

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(VerifyEmailCommand command) =>
        FromResult(await Mediator.Send(command), "Email verified successfully.");

    /// <summary>Changes the currently authenticated user's password.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordCommand command) =>
        FromResult(await Mediator.Send(command), "Password changed successfully.");
}
