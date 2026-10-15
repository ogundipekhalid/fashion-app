namespace FashionCommerce.Domain.Common;

/// <summary>
/// Marker interface for a domain event. The Application layer will define
/// MediatR INotificationHandlers that react to these (e.g. send an email
/// when OrderPlacedEvent fires). Domain stays framework-agnostic: it just
/// raises plain objects, it never depends on MediatR itself.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

public abstract record DomainEventBase : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>
/// Implemented by any entity that can raise domain events (currently just
/// BaseEntity, but kept as a separate interface for testability).
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
