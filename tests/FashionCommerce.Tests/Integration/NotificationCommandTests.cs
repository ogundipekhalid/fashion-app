using FashionCommerce.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using FashionCommerce.Application.Features.Notifications.Commands.MarkNotificationRead;
using FashionCommerce.Domain.Entities.Notifications;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class NotificationCommandTests
{
    [Fact]
    public async Task Marking_a_notification_read_sets_IsRead_and_ReadAt()
    {
        await using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var notification = new Notification { UserId = userId, UserRole = UserRole.Customer, Type = NotificationType.OrderPlaced, Title = "Order placed", Body = "Your order was placed." };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new MarkNotificationReadCommandHandler(context);
        var result = await handler.Handle(new MarkNotificationReadCommand(notification.Id, userId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        var reloaded = context.Notifications.Local.Single(n => n.Id == notification.Id);
        reloaded.IsRead.Should().BeTrue();
        reloaded.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Marking_someone_elses_notification_read_is_rejected()
    {
        await using var context = TestDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        var notification = new Notification { UserId = ownerId, UserRole = UserRole.Customer, Type = NotificationType.OrderPlaced, Title = "Order placed", Body = "Your order was placed." };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new MarkNotificationReadCommandHandler(context);

        var act = async () => await handler.Handle(new MarkNotificationReadCommand(notification.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<FashionCommerce.Application.Common.Exceptions.ForbiddenAccessException>();
    }

    [Fact]
    public async Task MarkAllRead_marks_every_unread_notification_for_that_user_and_role_only()
    {
        await using var context = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();

        context.Notifications.AddRange(
            new Notification { UserId = userId, UserRole = UserRole.Customer, Type = NotificationType.OrderPlaced, Title = "A", Body = "a" },
            new Notification { UserId = userId, UserRole = UserRole.Customer, Type = NotificationType.PaymentReceived, Title = "B", Body = "b" },
            new Notification { UserId = userId, UserRole = UserRole.Vendor, Type = NotificationType.PaymentReceived, Title = "C", Body = "c" }, // same user id, different role — should NOT be touched
            new Notification { UserId = Guid.NewGuid(), UserRole = UserRole.Customer, Type = NotificationType.OrderPlaced, Title = "D", Body = "d" } // different user — should NOT be touched
        );
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new MarkAllNotificationsReadCommandHandler(context);
        var result = await handler.Handle(new MarkAllNotificationsReadCommand(userId, UserRole.Customer), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().Be(2);

        context.Notifications.Local.Count(n => n.IsRead).Should().Be(2);
        context.Notifications.Local.Single(n => n.Title == "C").IsRead.Should().BeFalse();
        context.Notifications.Local.Single(n => n.Title == "D").IsRead.Should().BeFalse();
    }
}
