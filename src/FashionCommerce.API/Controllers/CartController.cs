using Asp.Versioning;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Cart.Commands.AddToCart;
using FashionCommerce.Application.Features.Cart.Commands.ApplyCoupon;
using FashionCommerce.Application.Features.Cart.Commands.MergeGuestCart;
using FashionCommerce.Application.Features.Cart.Commands.RemoveCoupon;
using FashionCommerce.Application.Features.Cart.Commands.RemoveFromCart;
using FashionCommerce.Application.Features.Cart.Commands.UpdateCartItemQuantity;
using FashionCommerce.Application.Features.Cart.Queries.GetCart;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

/// <summary>
/// Guest checkout is supported throughout: an anonymous client generates its
/// own GuestSessionId (any opaque string, typically a GUID stored in a
/// cookie/localStorage) and sends it via the X-Guest-Session-Id header.
/// Authenticated Customer requests ignore the header entirely and use the
/// JWT-derived CustomerId instead.
/// </summary>
[ApiVersion("1.0")]
[AllowAnonymous]
public class CartController : ApiControllerBase
{
    private const string GuestSessionHeader = "X-Guest-Session-Id";
    private readonly ICurrentUserService _currentUser;

    public CartController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var (customerId, guestSessionId) = ResolveIdentity();
        var cart = await Mediator.Send(new GetCartQuery(customerId, guestSessionId));
        return Ok(ApiResponse<object>.SuccessResponse(cart));
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem(AddToCartCommand command)
    {
        var (customerId, guestSessionId) = ResolveIdentity();
        var result = await Mediator.Send(command with { CustomerId = customerId, GuestSessionId = guestSessionId });
        return FromResult(result, "Item added to cart.");
    }

    [HttpPut("items/{id:guid}")]
    public async Task<IActionResult> UpdateItemQuantity(Guid id, UpdateCartItemQuantityCommand command)
    {
        var (customerId, guestSessionId) = ResolveIdentity();
        var result = await Mediator.Send(command with { CartItemId = id, RequestingCustomerId = customerId, RequestingGuestSessionId = guestSessionId });
        return FromResult(result, "Quantity updated.");
    }

    [HttpDelete("items/{id:guid}")]
    public async Task<IActionResult> RemoveItem(Guid id)
    {
        var (customerId, guestSessionId) = ResolveIdentity();
        var result = await Mediator.Send(new RemoveFromCartCommand { CartItemId = id, RequestingCustomerId = customerId, RequestingGuestSessionId = guestSessionId });
        return FromResult(result, "Item removed.");
    }

    [HttpPost("coupon")]
    public async Task<IActionResult> ApplyCoupon(ApplyCouponCommand command)
    {
        var result = await Mediator.Send(command);
        return FromResult(result, "Coupon applied.");
    }

    [HttpDelete("coupon/{cartId:guid}")]
    public async Task<IActionResult> RemoveCoupon(Guid cartId)
    {
        var result = await Mediator.Send(new RemoveCouponCommand { CartId = cartId });
        return FromResult(result, "Coupon removed.");
    }

    /// <summary>Call right after login if the client was holding a guest cart, so it merges into the now-authenticated customer's cart.</summary>
    [HttpPost("merge")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> MergeGuestCart([FromBody] string guestSessionId)
    {
        var customerId = _currentUser.ProfileId ?? throw new Application.Common.Exceptions.AuthenticationFailedException("Not authenticated.");
        var result = await Mediator.Send(new MergeGuestCartCommand { CustomerId = customerId, GuestSessionId = guestSessionId });
        return FromResult(result, "Cart merged.");
    }

    private (Guid? CustomerId, string? GuestSessionId) ResolveIdentity()
    {
        if (_currentUser.IsAuthenticated && _currentUser.Role == Domain.Enums.UserRole.Customer)
            return (_currentUser.ProfileId, null);

        var guestSessionId = Request.Headers[GuestSessionHeader].FirstOrDefault();
        return (null, guestSessionId);
    }
}
