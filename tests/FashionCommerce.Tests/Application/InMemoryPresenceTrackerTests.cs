using FashionCommerce.Infrastructure.Common;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Application;

public class InMemoryPresenceTrackerTests
{
    private readonly InMemoryPresenceTracker _sut = new();

    [Fact]
    public void First_connection_for_a_user_reports_them_as_now_online()
    {
        var userId = Guid.NewGuid();

        var wasFirstConnection = _sut.UserConnected(userId, "conn-1");

        wasFirstConnection.Should().BeTrue();
        _sut.IsOnline(userId).Should().BeTrue();
    }

    [Fact]
    public void A_second_connection_for_the_same_user_is_not_reported_as_a_fresh_online_event()
    {
        var userId = Guid.NewGuid();
        _sut.UserConnected(userId, "conn-1");

        var wasFirstConnection = _sut.UserConnected(userId, "conn-2"); // second tab/device

        wasFirstConnection.Should().BeFalse();
        _sut.IsOnline(userId).Should().BeTrue();
    }

    [Fact]
    public void User_stays_online_after_closing_one_of_two_connections()
    {
        var userId = Guid.NewGuid();
        _sut.UserConnected(userId, "conn-1");
        _sut.UserConnected(userId, "conn-2");

        var wentOffline = _sut.UserDisconnected(userId, "conn-1");

        wentOffline.Should().BeFalse();
        _sut.IsOnline(userId).Should().BeTrue();
    }

    [Fact]
    public void User_goes_offline_only_after_their_last_connection_drops()
    {
        var userId = Guid.NewGuid();
        _sut.UserConnected(userId, "conn-1");
        _sut.UserConnected(userId, "conn-2");
        _sut.UserDisconnected(userId, "conn-1");

        var wentOffline = _sut.UserDisconnected(userId, "conn-2");

        wentOffline.Should().BeTrue();
        _sut.IsOnline(userId).Should().BeFalse();
    }

    [Fact]
    public void GetLastSeenUtc_is_null_while_online()
    {
        var userId = Guid.NewGuid();
        _sut.UserConnected(userId, "conn-1");

        _sut.GetLastSeenUtc(userId).Should().BeNull();
    }

    [Fact]
    public void GetLastSeenUtc_is_set_after_going_offline()
    {
        var userId = Guid.NewGuid();
        _sut.UserConnected(userId, "conn-1");
        _sut.UserDisconnected(userId, "conn-1");

        _sut.GetLastSeenUtc(userId).Should().NotBeNull();
        _sut.GetLastSeenUtc(userId).Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Disconnecting_a_user_who_was_never_connected_is_a_safe_no_op()
    {
        var act = () => _sut.UserDisconnected(Guid.NewGuid(), "unknown-connection");

        act.Should().NotThrow();
    }

    [Fact]
    public void GetOnlineUserIds_reflects_only_currently_connected_users()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        _sut.UserConnected(userA, "conn-a");
        _sut.UserConnected(userB, "conn-b");
        _sut.UserDisconnected(userB, "conn-b");

        _sut.GetOnlineUserIds().Should().Contain(userA);
        _sut.GetOnlineUserIds().Should().NotContain(userB);
    }
}
