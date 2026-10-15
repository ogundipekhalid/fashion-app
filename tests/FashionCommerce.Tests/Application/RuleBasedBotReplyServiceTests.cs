using FashionCommerce.Application.Common.Services;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Application;

public class RuleBasedBotReplyServiceTests
{
    private readonly RuleBasedBotReplyService _sut = new();

    [Theory]
    [InlineData("There's a bug when I try to checkout")]
    [InlineData("The app keeps crashing")]
    [InlineData("This feature is not working")]
    public void Bug_reports_are_detected_and_escalated(string message)
    {
        var reply = _sut.Reply(message, "DeveloperContactBot");

        reply.DetectedIntent.Should().Be("report_bug");
        reply.ShouldEscalateToTicket.Should().BeTrue();
    }

    [Fact]
    public void Requests_to_talk_to_a_developer_are_detected_and_escalated()
    {
        var reply = _sut.Reply("I want to talk to a developer", "DeveloperContactBot");

        reply.DetectedIntent.Should().Be("contact_developer");
        reply.ShouldEscalateToTicket.Should().BeTrue();
    }

    [Fact]
    public void Payment_issues_are_escalated()
    {
        var reply = _sut.Reply("I think my card declined incorrectly", "CustomerSupportBot");

        reply.ShouldEscalateToTicket.Should().BeTrue();
    }

    [Fact]
    public void Refund_questions_are_answered_without_escalating()
    {
        var reply = _sut.Reply("How do I get a refund?", "CustomerSupportBot");

        reply.DetectedIntent.Should().Be("refund_question");
        reply.ShouldEscalateToTicket.Should().BeFalse();
    }

    [Fact]
    public void Vendor_onboarding_questions_are_answered_without_escalating()
    {
        var reply = _sut.Reply("How do I become a vendor and start selling?", "CustomerSupportBot");

        reply.DetectedIntent.Should().Be("vendor_onboarding");
        reply.ShouldEscalateToTicket.Should().BeFalse();
    }

    [Fact]
    public void Unrecognized_messages_are_handled_honestly_with_low_confidence_and_no_forced_escalation()
    {
        var reply = _sut.Reply("asdkjfh qwepoiu random gibberish", "CustomerSupportBot");

        reply.DetectedIntent.Should().Be("unrecognized");
        reply.ConfidenceScore.Should().BeLessThan(0.5);
        reply.ShouldEscalateToTicket.Should().BeFalse(); // offers escalation but doesn't force it
    }

    [Fact]
    public void Empty_message_returns_a_clarifying_prompt_without_crashing()
    {
        var reply = _sut.Reply("", "CustomerSupportBot");

        reply.Content.Should().NotBeNullOrEmpty();
        reply.ShouldEscalateToTicket.Should().BeFalse();
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var reply = _sut.Reply("I FOUND A BUG IN THE APP", "DeveloperContactBot");

        reply.DetectedIntent.Should().Be("report_bug");
    }
}
