using FashionCommerce.Application.Common.Interfaces;
using FashionCommerce.Domain.Entities.Audit;
using MediatR;

namespace FashionCommerce.Application.Common.Behaviors;

/// <summary>
/// Marker interface: commands that mutate meaningful business state
/// implement this so AuditLoggingBehavior knows to record them. Queries
/// and read-only commands don't implement it and are skipped automatically.
/// </summary>
public interface IAuditableRequest
{
    string Action { get; }       // e.g. "Order.StatusChanged"
    string EntityName { get; }   // e.g. "Order"
}

/// <summary>
/// Writes an AuditLog row after any IAuditableRequest succeeds. Runs after
/// ValidationBehavior (so invalid requests are never logged as actions)
/// but wraps the actual handler call to capture the acting user.
/// </summary>
public class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AuditLoggingBehavior(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is IAuditableRequest auditable)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = _currentUser.ProfileId,
                UserRole = _currentUser.Role?.ToString(),
                Action = auditable.Action,
                EntityName = auditable.EntityName,
                EntityId = Guid.Empty, // handlers that need the real entity id should log it themselves via IApplicationDbContext for now
                IpAddress = _currentUser.IpAddress,
                UserAgent = _currentUser.UserAgent,
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        return response;
    }
}
