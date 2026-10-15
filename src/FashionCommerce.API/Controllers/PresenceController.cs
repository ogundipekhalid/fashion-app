using Asp.Versioning;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Shared.Wrappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionCommerce.API.Controllers;

public record PresenceDto(Guid UserId, bool IsOnline, DateTime? LastSeenUtc);

/// <summary>
/// REST fallback for presence — e.g. a product page's initial server
/// render needs "is this vendor online" before the client's SignalR
/// connection has even been established. Live updates after that point
/// come from ChatHub's UserOnline/UserOffline broadcasts instead of polling
/// this endpoint.
/// </summary>
[ApiVersion("1.0")]
[Authorize]
public class PresenceController : ApiControllerBase
{
    private readonly IPresenceTracker _presenceTracker;

    public PresenceController(IPresenceTracker presenceTracker)
    {
        _presenceTracker = presenceTracker;
    }

    [HttpGet("{userId:guid}")]
    public IActionResult GetStatus(Guid userId)
    {
        var isOnline = _presenceTracker.IsOnline(userId);
        var dto = new PresenceDto(userId, isOnline, isOnline ? null : _presenceTracker.GetLastSeenUtc(userId));
        return Ok(ApiResponse<PresenceDto>.SuccessResponse(dto));
    }

    /// <summary>Batch check — e.g. a conversation list showing many vendors/customers at once.</summary>
    [HttpPost("batch")]
    public IActionResult GetStatusBatch([FromBody] List<Guid> userIds)
    {
        var results = userIds.Select(id =>
        {
            var isOnline = _presenceTracker.IsOnline(id);
            return new PresenceDto(id, isOnline, isOnline ? null : _presenceTracker.GetLastSeenUtc(id));
        }).ToList();

        return Ok(ApiResponse<List<PresenceDto>>.SuccessResponse(results));
    }
}
