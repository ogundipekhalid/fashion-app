using Asp.Versioning;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Payouts.Commands.ProcessPayout;
using FashionCommerce.Application.Features.Payouts.Commands.RejectPayout;
using FashionCommerce.Application.Features.Payouts.Commands.ReleaseEligibleFunds;
using FashionCommerce.Application.Features.Payouts.Commands.RequestPayout;
using FashionCommerce.Application.Features.Payouts.Queries.GetPayoutHistory;
using FashionCommerce.Application.Features.Payouts.Queries.GetVendorWallet;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class PayoutsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public PayoutsController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet("wallet")]
    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> GetMyWallet()
    {
        var wallet = await Mediator.Send(new GetVendorWalletQuery(RequireVendorId()));
        return Ok(ApiResponse<object>.SuccessResponse(wallet));
    }

    [HttpGet("history")]
    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> GetHistory([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var history = await Mediator.Send(new GetPayoutHistoryQuery(RequireVendorId(), pageNumber, pageSize));
        return Ok(new PaginatedApiResponse<object>
        {
            Success = true,
            Data = history.Items.Cast<object>(),
            PageNumber = history.PageNumber,
            TotalPages = history.TotalPages,
            TotalCount = history.TotalCount,
            HasPreviousPage = history.HasPreviousPage,
            HasNextPage = history.HasNextPage
        });
    }

    [HttpPost("request")]
    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> RequestPayout(RequestPayoutCommand command)
    {
        var result = await Mediator.Send(command with { VendorId = RequireVendorId() });
        return FromResult(result, "Payout requested.");
    }

    [HttpPost("{id:guid}/process")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Process(Guid id, ProcessPayoutCommand command)
    {
        var adminId = _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
        var result = await Mediator.Send(command with { PayoutRequestId = id, ProcessedByAdminId = adminId });
        return FromResult(result, "Payout marked as paid.");
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Reject(Guid id, RejectPayoutCommand command)
    {
        var result = await Mediator.Send(command with { PayoutRequestId = id });
        return FromResult(result, "Payout rejected.");
    }

    /// <summary>
    /// Stand-in for a scheduled job (see README — Hangfire recurring job not
    /// yet wired up). Admin can trigger it manually for now.
    /// </summary>
    [HttpPost("release-eligible-funds")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> ReleaseEligibleFunds(ReleaseEligibleFundsCommand command)
    {
        var result = await Mediator.Send(command);
        return FromResult(result, "Eligible funds released.");
    }

    private Guid RequireVendorId() => _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
}
