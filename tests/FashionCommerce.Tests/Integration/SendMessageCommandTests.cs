using FashionCommerce.Application.Common.Services;
using FashionCommerce.Application.Features.Chat.Commands.SendMessage;
using FashionCommerce.Domain.Entities.Catalog;
using FashionCommerce.Domain.Entities.Chat;
using FashionCommerce.Domain.Entities.Identity;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class SendMessageCommandTests
{
    [Fact]
    public async Task A_clean_message_is_delivered_and_the_conversation_stays_open()
    {
        await using var context = TestDbContextFactory.Create();
        var conversation = await SeedOpenConversationAsync(context);
        var handler = new SendMessageCommandHandler(context, new ChatModerationService());

        var result = await handler.Handle(new SendMessageCommand
        {
            ConversationId = conversation.Id,
            SenderId = conversation.CustomerId,
            SenderType = "Customer",
            Content = "Does this come in a size large?"
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        context.Messages.Local.Should().ContainSingle(m => !m.IsBlocked);

        var reloadedConversation = context.Conversations.Local.Single(c => c.Id == conversation.Id);
        reloadedConversation.Status.Should().Be(ConversationStatus.AwaitingVendorReply);
    }

    [Fact]
    public async Task A_message_containing_a_phone_number_is_blocked_and_the_conversation_is_auto_flagged()
    {
        await using var context = TestDbContextFactory.Create();
        var conversation = await SeedOpenConversationAsync(context);
        var handler = new SendMessageCommandHandler(context, new ChatModerationService());

        var result = await handler.Handle(new SendMessageCommand
        {
            ConversationId = conversation.Id,
            SenderId = conversation.CustomerId,
            SenderType = "Customer",
            Content = "Call me on 08012345678 instead"
        }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();

        var blockedMessage = context.Messages.Local.Should().ContainSingle().Subject;
        blockedMessage.IsBlocked.Should().BeTrue();

        var reloadedConversation = context.Conversations.Local.Single(c => c.Id == conversation.Id);
        reloadedConversation.Status.Should().Be(ConversationStatus.Blocked);
        reloadedConversation.IsAutoFlagged.Should().BeTrue();

        // An admin notification should have been raised alongside the block.
        context.Notifications.Local.Should().ContainSingle(n => n.Type == NotificationType.SupportTicketUpdated);
    }

    [Fact]
    public async Task Messages_cannot_be_sent_into_an_already_blocked_conversation()
    {
        await using var context = TestDbContextFactory.Create();
        var conversation = await SeedOpenConversationAsync(context);
        conversation.FlagAndBlock("Previously flagged for testing.");
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new SendMessageCommandHandler(context, new ChatModerationService());

        var result = await handler.Handle(new SendMessageCommand
        {
            ConversationId = conversation.Id,
            SenderId = conversation.CustomerId,
            SenderType = "Customer",
            Content = "Hello, are you still there?"
        }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        context.Messages.Local.Should().BeEmpty(); // never even reaches moderation — blocked at the door
    }

    private static async Task<Conversation> SeedOpenConversationAsync(TestApplicationDbContext context)
    {
        var customer = new Customer { IdentityUserId = Guid.NewGuid(), FirstName = "Test", LastName = "Customer", Email = "customer@test.dev" };
        var vendor = new Vendor { IdentityUserId = Guid.NewGuid(), BusinessName = "Test Vendor", ContactEmail = "vendor@test.dev" };
        var category = new Category { Name = "Shirts", Slug = $"shirts-{Guid.NewGuid():N}" };
        var product = new Product
        {
            VendorId = vendor.Id, Vendor = vendor, CategoryId = category.Id, Category = category,
            Name = "Test Shirt", Slug = $"shirt-{Guid.NewGuid():N}", Description = "desc",
            Sku = $"SKU-{Guid.NewGuid():N}", BasePrice = 20m
        };

        var conversation = Conversation.Start(customer.Id, vendor.Id, product.Id, ConversationType.ProductEnquiry);

        context.Customers.Add(customer);
        context.Vendors.Add(vendor);
        context.Categories.Add(category);
        context.Products.Add(product);
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(CancellationToken.None);

        return conversation;
    }
}
