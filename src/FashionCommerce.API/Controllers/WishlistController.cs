using Asp.Versioning;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Wishlist.Commands.AddToWishlist;
using FashionCommerce.Application.Features.Wishlist.Commands.RemoveFromWishlist;
using FashionCommerce.Application.Features.Wishlist.Queries.GetMyWishlist;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
[Authorize(Roles = "Customer")]
public class WishlistController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public WishlistController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyWishlist()
    {
        var items = await Mediator.Send(new GetMyWishlistQuery(RequireCustomerId()));
        return Ok(ApiResponse<object>.SuccessResponse(items));
    }

    [HttpPost]
    public async Task<IActionResult> Add(AddToWishlistCommand command)
    {
        var result = await Mediator.Send(command with { CustomerId = RequireCustomerId() });
        return FromResult(result, "Added to wishlist.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id)
    {
        var result = await Mediator.Send(new RemoveFromWishlistCommand { WishlistItemId = id, CustomerId = RequireCustomerId() });
        return FromResult(result, "Removed from wishlist.");
    }

    private Guid RequireCustomerId() => _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
}
