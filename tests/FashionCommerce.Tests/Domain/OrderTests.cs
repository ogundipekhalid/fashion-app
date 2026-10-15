using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Entities.Orders;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Domain;

public class OrderTests
{
    private static Order CreateOrder() => new()
    {
        OrderNumber = "ORD-TEST-0001",
        CustomerId = Guid.NewGuid(),
        ShippingAddressId = Guid.NewGuid(),
        BillingAddressId = Guid.NewGuid(),
        Subtotal = 100m,
        Total = 100m
    };

    [Fact]
    public void New_order_starts_as_PendingPayment()
    {
        var order = CreateOrder();
        order.Status.Should().Be(OrderStatus.PendingPayment);
    }

    [Fact]
    public void MarkPaymentSuccessful_transitions_to_Paid_and_raises_OrderPlacedEvent()
    {
        var order = CreateOrder();

        order.MarkPaymentSuccessful();

        order.Status.Should().Be(OrderStatus.Paid);
        order.PaymentStatus.Should().Be(PaymentStatus.Successful);
        order.DomainEvents.Should().Contain(e => e.GetType().Name == "OrderPlacedEvent");
    }

    [Fact]
    public void MarkPaymentFailed_transitions_to_Failed()
    {
        var order = CreateOrder();

        order.MarkPaymentFailed();

        order.Status.Should().Be(OrderStatus.Failed);
        order.PaymentStatus.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void Cannot_skip_directly_from_PendingPayment_to_Shipped()
    {
        var order = CreateOrder();

        var act = () => order.TransitionTo(OrderStatus.Shipped);

        act.Should().Throw<InvalidOrderStateTransitionException>();
    }

    [Fact]
    public void Cannot_transition_out_of_a_terminal_Delivered_status()
    {
        var order = CreateOrder();
        order.TransitionTo(OrderStatus.Paid);
        order.TransitionTo(OrderStatus.Processing);
        order.TransitionTo(OrderStatus.Shipped);
        order.TransitionTo(OrderStatus.OutForDelivery);
        order.TransitionTo(OrderStatus.Delivered);

        var act = () => order.TransitionTo(OrderStatus.Cancelled);

        act.Should().Throw<InvalidOrderStateTransitionException>();
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Full_happy_path_transition_sequence_succeeds()
    {
        var order = CreateOrder();

        order.TransitionTo(OrderStatus.Paid);
        order.TransitionTo(OrderStatus.Processing);
        order.TransitionTo(OrderStatus.Shipped);
        order.TransitionTo(OrderStatus.OutForDelivery);
        order.TransitionTo(OrderStatus.Delivered);

        order.Status.Should().Be(OrderStatus.Delivered);
        order.DeliveredAt.Should().NotBeNull();
        order.StatusHistory.Should().HaveCount(5);
    }

    [Fact]
    public void Transitioning_to_the_same_status_is_a_no_op_and_does_not_log_history()
    {
        var order = CreateOrder();

        order.TransitionTo(OrderStatus.PendingPayment);

        order.StatusHistory.Should().BeEmpty();
    }

    [Fact]
    public void Cancelling_sets_CancelledAt()
    {
        var order = CreateOrder();

        order.TransitionTo(OrderStatus.Cancelled, "Customer changed their mind");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelledAt.Should().NotBeNull();
        order.StatusHistory.Single().Note.Should().Be("Customer changed their mind");
    }
}
