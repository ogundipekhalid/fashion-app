using Asp.Versioning;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Orders.Commands.CancelOrder;
using FashionCommerce.Application.Features.Orders.Commands.PlaceOrder;
using FashionCommerce.Application.Features.Orders.Commands.UpdateOrderItemStatus;
using FashionCommerce.Application.Features.Orders.Queries.GetMyOrders;
using FashionCommerce.Application.Features.Orders.Queries.GetOrderById;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class OrdersController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public OrdersController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>Checkout: converts the customer's cart into an Order and initializes payment with the chosen provider.</summary>
    [HttpPost]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> PlaceOrder(PlaceOrderCommand command)
    {
        var customerId = RequireProfileId();
        var result = await Mediator.Send(command with { CustomerId = customerId });
        return FromResult(result, "Order placed. Complete payment to confirm.");
    }

    /// <summary>Order history — Customers see their own orders, Vendors see orders containing at least one of their items.</summary>
    [HttpGet]
    [Authorize(Roles = "Customer,Vendor")]
    public async Task<IActionResult> GetMyOrders([FromQuery] string? statusFilter, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var role = _currentUser.Role!.Value;
        var result = await Mediator.Send(new GetMyOrdersQuery
        {
            RequesterProfileId = RequireProfileId(),
            RequesterRole = role,
            StatusFilter = statusFilter,
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
        var order = await Mediator.Send(new GetOrderByIdQuery(id, RequireProfileId(), role));
        return Ok(ApiResponse<object>.SuccessResponse(order));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Cancel(Guid id, CancelOrderCommand command)
    {
        var result = await Mediator.Send(command with { OrderId = id, RequestingCustomerId = RequireProfileId() });
        return FromResult(result, "Order cancelled.");
    }

    /// <summary>Vendor progresses one of their own line items through fulfilment (Confirmed → Processing → Shipped → Delivered).</summary>
    [HttpPatch("items/{itemId:guid}/status")]
    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> UpdateItemStatus(Guid itemId, UpdateOrderItemStatusCommand command)
    {
        var result = await Mediator.Send(command with { OrderItemId = itemId, RequestingVendorId = RequireProfileId() });
        return FromResult(result, "Item status updated.");
    }

    private Guid RequireProfileId() => _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
}
