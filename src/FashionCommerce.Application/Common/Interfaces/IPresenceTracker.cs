namespace FashionCommerce.Application.Common.Interfaces;

/// <summary>
/// Tracks which users currently have at least one live SignalR connection.
/// Multi-connection aware: a user with the app open in two tabs is still
/// "online" after closing one — only goes offline when their LAST
/// connection drops. Implemented in-memory in Infrastructure; a
/// multi-instance deployment would swap this for a Redis-backed
/// implementation without changing anything that calls it.
/// </summary>
public interface IPresenceTracker
{
    /// <summary>Registers a new connection for the user. Returns true if this was their first active connection (i.e. they just came online).</summary>
    bool UserConnected(Guid userId, string connectionId);

    /// <summary>Removes a connection. Returns true if the user has no remaining connections (i.e. they just went offline).</summary>
    bool UserDisconnected(Guid userId, string connectionId);

    bool IsOnline(Guid userId);

    /// <summary>Null if the user has never connected since the API process started, or is currently online.</summary>
    DateTime? GetLastSeenUtc(Guid userId);

    IReadOnlyCollection<Guid> GetOnlineUserIds();
}
