using Asp.Versioning;
using FashionCommerce.Application.Features.Catalog.Commands.CreateBrand;
using FashionCommerce.Application.Features.Catalog.Queries.GetBrands;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
public class BrandsController : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
    {
        var brands = await Mediator.Send(new GetBrandsQuery());
        return Ok(ApiResponse<object>.SuccessResponse(brands));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create(CreateBrandCommand command) =>
        FromResult(await Mediator.Send(command), "Brand created successfully.");
}
