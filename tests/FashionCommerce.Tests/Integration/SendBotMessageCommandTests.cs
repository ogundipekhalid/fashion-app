using FashionCommerce.Application.Common.Services;
using FashionCommerce.Application.Features.Support.Commands.SendBotMessage;
using FashionCommerce.Domain.Entities.Support;
using FashionCommerce.Domain.Enums;
using FashionCommerce.Tests.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace FashionCommerce.Tests.Integration;

public class SendBotMessageCommandTests
{
    [Fact]
    public async Task A_bug_report_to_the_dev_contact_bot_automatically_opens_a_support_ticket()
    {
        await using var context = TestDbContextFactory.Create();
        var conversation = new BotConversation { UserId = Guid.NewGuid(), UserRole = UserRole.Customer, Channel = BotChannel.DeveloperContactBot };
        context.BotConversations.Add(conversation);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new SendBotMessageCommandHandler(context, new RuleBasedBotReplyService(), Mock.Of<global::FashionCommerce.Application.Common.Interfaces.IAiService>());

        var result = await handler.Handle(new SendBotMessageCommand
        {
            BotConversationId = conversation.Id,
            Content = "I found a bug — the checkout page crashes on submit"
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();

        var ticket = context.SupportTickets.Local.Should().ContainSingle().Subject;
        ticket.Category.Should().Be(SupportTicketCategory.BugReport);
        ticket.RequesterId.Should().Be(conversation.UserId);

        var reloadedConversation = context.BotConversations.Local.Single(c => c.Id == conversation.Id);
        reloadedConversation.EscalatedToSupportTicketId.Should().Be(ticket.Id);
        reloadedConversation.EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_general_question_to_the_support_bot_is_answered_without_opening_a_ticket()
    {
        await using var context = TestDbContextFactory.Create();
        var conversation = new BotConversation { UserId = Guid.NewGuid(), UserRole = UserRole.Customer, Channel = BotChannel.CustomerSupportBot };
        context.BotConversations.Add(conversation);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new SendBotMessageCommandHandler(context, new RuleBasedBotReplyService(), Mock.Of<global::FashionCommerce.Application.Common.Interfaces.IAiService>());

        var result = await handler.Handle(new SendBotMessageCommand
        {
            BotConversationId = conversation.Id,
            Content = "How do I get a refund on an order?"
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        context.SupportTickets.Local.Should().BeEmpty();

        var reloadedConversation = context.BotConversations.Local.Single(c => c.Id == conversation.Id);
        reloadedConversation.EscalatedToSupportTicketId.Should().BeNull();
        reloadedConversation.EndedAt.Should().BeNull(); // conversation stays open, not escalated
    }

    [Fact]
    public async Task Messages_cannot_be_sent_into_an_already_ended_bot_conversation()
    {
        await using var context = TestDbContextFactory.Create();
        var conversation = new BotConversation { UserId = Guid.NewGuid(), UserRole = UserRole.Customer, Channel = BotChannel.CustomerSupportBot };
        conversation.End();
        context.BotConversations.Add(conversation);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = new SendBotMessageCommandHandler(context, new RuleBasedBotReplyService(), Mock.Of<global::FashionCommerce.Application.Common.Interfaces.IAiService>());

        var result = await handler.Handle(new SendBotMessageCommand
        {
            BotConversationId = conversation.Id,
            Content = "Are you still there?"
        }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
    }
}
