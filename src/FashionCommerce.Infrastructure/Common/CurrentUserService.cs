using System.Security.Claims;
using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace FashionCommerce.Infrastructure.Common;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? IdentityUserId
    {
        get
        {
            var sub = User?.FindFirstValue("sub") ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid? ProfileId
    {
        get
        {
            var profileId = User?.FindFirstValue("profile_id");
            return Guid.TryParse(profileId, out var id) ? id : null;
        }
    }

    public UserRole? Role
    {
        get
        {
            var role = User?.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(role, out var parsed) ? parsed : null;
        }
    }

    public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
