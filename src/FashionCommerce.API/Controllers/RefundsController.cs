using Asp.Versioning;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Refunds.Commands.AdminDecideRefund;
using FashionCommerce.Application.Features.Refunds.Commands.ProcessRefund;
using FashionCommerce.Application.Features.Refunds.Commands.RequestRefund;
using FashionCommerce.Application.Features.Refunds.Commands.VendorRespondToRefund;
using FashionCommerce.Application.Features.Refunds.Queries.GetMyRefunds;
using FashionCommerce.Application.Features.Refunds.Queries.GetRefundById;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class RefundsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public RefundsController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Request(RequestRefundCommand command)
    {
        var result = await Mediator.Send(command with { RequestingCustomerId = RequireProfileId() });
        return FromResult(result, "Refund requested.");
    }

    [HttpGet]
    [Authorize(Roles = "Customer,Vendor")]
    public async Task<IActionResult> GetMine([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetMyRefundsQuery
        {
            RequesterProfileId = RequireProfileId(),
            RequesterRole = _currentUser.Role!.Value,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

        return Ok(new PaginatedApiResponse<object>
        {
            Success = true,
            Data = result.Items.Cast<object>(),
            PageNumber = result.PageNumber,
            TotalPages = result.TotalPages,
            TotalCount = result.TotalCount,
            HasPreviousPage = result.HasPreviousPage,
            HasNextPage = result.HasNextPage
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var role = _currentUser.Role ?? throw new AuthenticationFailedException("Not authenticated.");
        var refund = await Mediator.Send(new GetRefundByIdQuery(id, RequireProfileId(), role));
        return Ok(ApiResponse<object>.SuccessResponse(refund));
    }

    [HttpPost("{id:guid}/vendor-response")]
    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> VendorRespond(Guid id, VendorRespondToRefundCommand command)
    {
        var result = await Mediator.Send(command with { RefundRequestId = id, RequestingVendorId = RequireProfileId() });
        return FromResult(result, "Response recorded.");
    }

    [HttpPost("{id:guid}/admin-decision")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> AdminDecide(Guid id, AdminDecideRefundCommand command)
    {
        var result = await Mediator.Send(command with { RefundRequestId = id });
        return FromResult(result, "Decision recorded.");
    }

    /// <summary>Finalizes an approved refund — calls the payment provider and applies the vendor wallet clawback.</summary>
    [HttpPost("{id:guid}/process")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Process(Guid id)
    {
        var result = await Mediator.Send(new ProcessRefundCommand(id));
        return FromResult(result, "Refund processed.");
    }

    private Guid RequireProfileId() => _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
}
