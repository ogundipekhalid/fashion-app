using Asp.Versioning;
using FashionCommerce.Application.Features.Catalog.Commands.CreateCategory;
using FashionCommerce.Application.Features.Catalog.Queries.GetCategories;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
public class CategoriesController : ApiControllerBase
{
    /// <summary>Returns the full category tree. Public — no auth required.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
    {
        var categories = await Mediator.Send(new GetCategoriesQuery());
        return Ok(ApiResponse<object>.SuccessResponse(categories));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create(CreateCategoryCommand command) =>
        FromResult(await Mediator.Send(command), "Category created successfully.");
}
