using System.Collections.Concurrent;
using FashionCommerce.Application.Common.Interfaces;

namespace FashionCommerce.Infrastructure.Common;

/// <summary>
/// Single-instance in-memory implementation — must be registered as a
/// Singleton in DI (see Infrastructure/DependencyInjection.cs) since its
/// whole purpose is to hold connection state across the lifetime of the
/// process. Last-seen timestamps reset on API restart, same as the online
/// set — acceptable for now, but worth knowing. Fine for one API instance;
/// horizontally scaling the API would need a shared store (Redis) instead
/// so presence is consistent across instances — that swap only touches
/// this class, since everything else depends on IPresenceTracker.
/// </summary>
public class InMemoryPresenceTracker : IPresenceTracker
{
    // userId -> set of active SignalR connection ids (one user can have multiple tabs/devices open).
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _onlineUsers = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _lastSeenUtc = new();
    private readonly object _lock = new();

    public bool UserConnected(Guid userId, string connectionId)
    {
        lock (_lock)
        {
            var isFirstConnection = !_onlineUsers.ContainsKey(userId);

            _onlineUsers.AddOrUpdate(
                userId,
                _ => new HashSet<string> { connectionId },
                (_, connections) => { connections.Add(connectionId); return connections; });

            return isFirstConnection;
        }
    }

    public bool UserDisconnected(Guid userId, string connectionId)
    {
        lock (_lock)
        {
            if (!_onlineUsers.TryGetValue(userId, out var connections))
                return false;

            connections.Remove(connectionId);

            if (connections.Count != 0) return false;

            _onlineUsers.TryRemove(userId, out _);
            _lastSeenUtc[userId] = DateTime.UtcNow;
            return true; // last connection dropped — user is now offline
        }
    }

    public bool IsOnline(Guid userId) => _onlineUsers.ContainsKey(userId);

    public DateTime? GetLastSeenUtc(Guid userId) =>
        IsOnline(userId) ? null : (_lastSeenUtc.TryGetValue(userId, out var seen) ? seen : null);

    public IReadOnlyCollection<Guid> GetOnlineUserIds() => _onlineUsers.Keys.ToList();
}
