using Asp.Versioning;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using FashionCommerce.Application.Features.Notifications.Commands.MarkNotificationRead;
using FashionCommerce.Application.Features.Notifications.Queries.GetMyNotifications;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class NotificationsController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] bool unreadOnly = false, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetMyNotificationsQuery
        {
            UserId = RequireUserId(),
            UserRole = _currentUser.Role!.Value,
            UnreadOnly = unreadOnly,
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

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var result = await Mediator.Send(new MarkNotificationReadCommand(id, RequireUserId()));
        return FromResult(result);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var result = await Mediator.Send(new MarkAllNotificationsReadCommand(RequireUserId(), _currentUser.Role!.Value));
        return FromResult(result, "All notifications marked as read.");
    }

    private Guid RequireUserId() => _currentUser.ProfileId ?? throw new AuthenticationFailedException("Not authenticated.");
}
