using Asp.Versioning;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Admin.Queries.GetRevenueTrend;
using FashionCommerce.Application.Features.Vendors.Queries.GetVendorDashboardSummary;
using FashionCommerce.Application.Features.Vendors.Queries.GetVendorSalesTrend;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

/// <summary>
/// The vendor-facing dashboard. Most of a vendor's day-to-day data already
/// lives behind other controllers they also call directly — order list
/// (OrdersController), wallet/payout history (PayoutsController), product
/// list (ProductsController) — this endpoint exists for the single
/// "at a glance" summary view rather than duplicating those.
/// </summary>
[ApiVersion("1.0")]
[Authorize(Roles = "Vendor")]
public class VendorController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public VendorController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var vendorId = _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
        var summary = await Mediator.Send(new GetVendorDashboardSummaryQuery(vendorId));
        return Ok(ApiResponse<object>.SuccessResponse(summary));
    }

    /// <summary>Sales trend based on the vendor's own earnings (post-commission), not gross order totals.</summary>
    [HttpGet("sales-trend")]
    public async Task<IActionResult> GetSalesTrend(
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] TrendGranularity granularity = TrendGranularity.Daily)
    {
        var vendorId = _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
        var result = await Mediator.Send(new GetVendorSalesTrendQuery
        {
            VendorId = vendorId,
            FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
            ToDate = toDate ?? DateTime.UtcNow,
            Granularity = granularity
        });
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }
}
