using Asp.Versioning;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Catalog.Commands.AddProductReview;
using FashionCommerce.Application.Features.Catalog.Commands.ApproveProduct;
using FashionCommerce.Application.Features.Catalog.Commands.CreateProduct;
using FashionCommerce.Application.Features.Catalog.Commands.RejectProduct;
using FashionCommerce.Application.Features.Catalog.Commands.UpdateProduct;
using FashionCommerce.Application.Features.Catalog.Queries.GetProductById;
using FashionCommerce.Application.Features.Catalog.Queries.GetProductsList;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
public class ProductsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public ProductsController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>Paginated, filterable product listing. Public — defaults to Active products only.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetList([FromQuery] GetProductsListQuery query)
    {
        // Non-Active visibility is only ever granted to the owning Vendor or Admin — never to an anonymous/customer caller,
        // regardless of what IncludeNonActive was set to in the query string.
        var isAuthorizedForNonActive = _currentUser.Role is Domain.Enums.UserRole.Vendor or Domain.Enums.UserRole.Admin or Domain.Enums.UserRole.SuperAdmin;
        var safeQuery = query with { IncludeNonActive = query.IncludeNonActive && isAuthorizedForNonActive };

        var result = await Mediator.Send(safeQuery);
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

    /// <summary>Full product detail for the product detail page. Public.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await Mediator.Send(new GetProductByIdQuery(id));
        return Ok(ApiResponse<object>.SuccessResponse(product));
    }

    /// <summary>Vendor lists a new product. Goes into PendingApproval status until moderated.</summary>
    [HttpPost]
    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> Create(CreateProductCommand command)
    {
        var vendorId = _currentUser.ProfileId ?? throw new FashionCommerce.Application.Common.Exceptions.AuthenticationFailedException("Not authenticated.");
        var result = await Mediator.Send(command with { VendorId = vendorId });
        return FromResult(result, "Product submitted for approval.");
    }

    /// <summary>Vendor updates their own product.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Vendor")]
    public async Task<IActionResult> Update(Guid id, UpdateProductCommand command)
    {
        var vendorId = _currentUser.ProfileId ?? throw new FashionCommerce.Application.Common.Exceptions.AuthenticationFailedException("Not authenticated.");
        var result = await Mediator.Send(command with { ProductId = id, RequestingVendorId = vendorId });
        return FromResult(result, "Product updated successfully.");
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var result = await Mediator.Send(new ApproveProductCommand { ProductId = id });
        return FromResult(result, "Product approved.");
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Reject(Guid id, RejectProductCommand command)
    {
        var result = await Mediator.Send(command with { ProductId = id });
        return FromResult(result, "Product rejected.");
    }

    /// <summary>Customer adds a review. If OrderItemId references a Delivered order for this product, the review is marked Verified Purchase.</summary>
    [HttpPost("{id:guid}/reviews")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> AddReview(Guid id, AddProductReviewCommand command)
    {
        var customerId = _currentUser.ProfileId ?? throw new FashionCommerce.Application.Common.Exceptions.AuthenticationFailedException("Not authenticated.");
        var result = await Mediator.Send(command with { ProductId = id, CustomerId = customerId });
        return FromResult(result, "Review submitted.");
    }
}
