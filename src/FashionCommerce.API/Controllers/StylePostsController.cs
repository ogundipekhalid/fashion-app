using Asp.Versioning;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Styling.Commands.AddStylePostComment;
using FashionCommerce.Application.Features.Styling.Commands.CreateStylePost;
using FashionCommerce.Application.Features.Styling.Commands.LikeStylePost;
using FashionCommerce.Application.Features.Styling.Commands.RemoveStylePost;
using FashionCommerce.Application.Features.Styling.Commands.UnlikeStylePost;
using FashionCommerce.Application.Features.Styling.Queries.GetStylePostsForProduct;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
public class StylePostsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public StylePostsController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>The styling feed for a product's page — how real customers and the vendor have styled it. Public, but likes show as "liked" only for the authenticated requester.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetForProduct([FromQuery] Guid productId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetStylePostsForProductQuery
        {
            ProductId = productId,
            RequesterId = _currentUser.ProfileId,
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

    /// <summary>Customer or Vendor posts a styled photo. Images are expected to already be uploaded (see Storage module note).</summary>
    [HttpPost]
    [Authorize(Roles = "Customer,Vendor")]
    public async Task<IActionResult> Create(CreateStylePostCommand command)
    {
        var authorId = _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
        var authorType = _currentUser.Role!.Value.ToString();
        var result = await Mediator.Send(command with { AuthorId = authorId, AuthorType = authorType });
        return FromResult(result, "Style post published.");
    }

    [HttpPost("{id:guid}/like")]
    [Authorize]
    public async Task<IActionResult> Like(Guid id)
    {
        var userId = _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
        var result = await Mediator.Send(new LikeStylePostCommand { StylePostId = id, UserId = userId, UserRole = _currentUser.Role!.Value.ToString() });
        return FromResult(result);
    }

    [HttpDelete("{id:guid}/like")]
    [Authorize]
    public async Task<IActionResult> Unlike(Guid id)
    {
        var userId = _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
        var result = await Mediator.Send(new UnlikeStylePostCommand { StylePostId = id, UserId = userId });
        return FromResult(result);
    }

    [HttpPost("{id:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(Guid id, AddStylePostCommentCommand command)
    {
        var userId = _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
        var result = await Mediator.Send(command with { StylePostId = id, UserId = userId, UserRole = _currentUser.Role!.Value.ToString() });
        return FromResult(result, "Comment added.");
    }

    /// <summary>Author deletes their own post, or Admin removes it for moderation.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Remove(Guid id, [FromQuery] string? reason)
    {
        var isAdmin = _currentUser.Role is Domain.Enums.UserRole.Admin or Domain.Enums.UserRole.SuperAdmin;
        var result = await Mediator.Send(new RemoveStylePostCommand
        {
            StylePostId = id,
            RequestingAuthorId = isAdmin ? null : _currentUser.ProfileId,
            Reason = reason
        });
        return FromResult(result, "Style post removed.");
    }
}
