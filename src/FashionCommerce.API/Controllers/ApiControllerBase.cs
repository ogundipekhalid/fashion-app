using FashionCommerce.Application.Common.Models;
using FashionCommerce.Shared.Wrappers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>Maps a non-generic Result to 200 OK / 400 Bad Request in the standard envelope.</summary>
    protected IActionResult FromResult(Result result, string? successMessage = null) =>
        result.Succeeded
            ? Ok(ApiResponse.SuccessResponse(successMessage))
            : BadRequest(ApiResponse.FailureResponse(result.Errors.FirstOrDefault() ?? "Request failed.", result.Errors));

    /// <summary>Maps a Result&lt;T&gt; to 200 OK / 400 Bad Request in the standard envelope.</summary>
    protected IActionResult FromResult<T>(Result<T> result, string? successMessage = null) =>
        result.Succeeded
            ? Ok(ApiResponse<T>.SuccessResponse(result.Value!, successMessage))
            : BadRequest(ApiResponse<T>.FailureResponse(result.Errors.FirstOrDefault() ?? "Request failed.", result.Errors));
}
