using Asp.Versioning;
using FashionCommerce.Application.Features.Admin.Commands.ToggleVendorActive;
using FashionCommerce.Application.Features.Admin.Commands.UpdatePlatformSettings;
using FashionCommerce.Application.Features.Admin.Commands.VerifyVendor;
using FashionCommerce.Application.Features.Admin.Queries.GetAuditLogs;
using FashionCommerce.Application.Features.Admin.Queries.GetCustomers;
using FashionCommerce.Application.Features.Admin.Queries.GetDashboardSummary;
using FashionCommerce.Application.Features.Admin.Queries.GetPlatformSettings;
using FashionCommerce.Application.Features.Admin.Queries.GetRevenueTrend;
using FashionCommerce.Application.Features.Admin.Queries.GetTaxReport;
using FashionCommerce.Application.Features.Admin.Queries.GetVendors;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AdminController : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var summary = await Mediator.Send(new GetDashboardSummaryQuery());
        return Ok(ApiResponse<object>.SuccessResponse(summary));
    }

    [HttpGet("vendors")]
    public async Task<IActionResult> GetVendors([FromQuery] string? verificationStatusFilter, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetVendorsQuery
        {
            VerificationStatusFilter = verificationStatusFilter,
            SearchTerm = searchTerm,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        return Ok(ToPaginatedResponse(result));
    }

    [HttpPost("vendors/{id:guid}/verify")]
    public async Task<IActionResult> VerifyVendor(Guid id, VerifyVendorCommand command)
    {
        var result = await Mediator.Send(command with { VendorId = id });
        return FromResult(result, "Vendor verification decision recorded.");
    }

    [HttpPost("vendors/{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleVendorActive(Guid id, ToggleVendorActiveCommand command)
    {
        var result = await Mediator.Send(command with { VendorId = id });
        return FromResult(result, command.IsActive ? "Vendor reactivated." : "Vendor suspended.");
    }

    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers([FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetCustomersQuery { SearchTerm = searchTerm, PageNumber = pageNumber, PageSize = pageSize });
        return Ok(ToPaginatedResponse(result));
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityNameFilter, [FromQuery] Guid? userIdFilter,
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        var result = await Mediator.Send(new GetAuditLogsQuery
        {
            EntityNameFilter = entityNameFilter,
            UserIdFilter = userIdFilter,
            FromDate = fromDate,
            ToDate = toDate,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        return Ok(ToPaginatedResponse(result));
    }

    [HttpGet("analytics/revenue-trend")]
    public async Task<IActionResult> GetRevenueTrend(
        [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] TrendGranularity granularity = TrendGranularity.Daily)
    {
        var result = await Mediator.Send(new GetRevenueTrendQuery
        {
            FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
            ToDate = toDate ?? DateTime.UtcNow,
            Granularity = granularity
        });
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("analytics/tax-report")]
    public async Task<IActionResult> GetTaxReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var result = await Mediator.Send(new GetTaxReportQuery
        {
            FromDate = fromDate ?? DateTime.UtcNow.AddMonths(-1),
            ToDate = toDate ?? DateTime.UtcNow
        });
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var result = await Mediator.Send(new GetPlatformSettingsQuery());
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPut("settings")]
    [Authorize(Roles = "SuperAdmin")] // platform-wide config changes are SuperAdmin-only, stricter than the rest of AdminController
    public async Task<IActionResult> UpdateSettings(UpdatePlatformSettingsCommand command)
    {
        var result = await Mediator.Send(command);
        return FromResult(result, "Platform settings updated.");
    }

    private static PaginatedApiResponse<object> ToPaginatedResponse<T>(Application.Common.Models.PaginatedList<T> list) => new()
    {
        Success = true,
        Data = list.Items.Cast<object>(),
        PageNumber = list.PageNumber,
        TotalPages = list.TotalPages,
        TotalCount = list.TotalCount,
        HasPreviousPage = list.HasPreviousPage,
        HasNextPage = list.HasNextPage
    };
}
