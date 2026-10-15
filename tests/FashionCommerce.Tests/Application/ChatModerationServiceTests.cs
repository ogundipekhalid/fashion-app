using FashionCommerce.Application.Common.Services;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Application;

public class ChatModerationServiceTests
{
    private readonly ChatModerationService _sut = new();

    [Fact]
    public void Clean_product_question_is_not_flagged()
    {
        var result = _sut.CheckMessage("Does this come in a size large?", Array.Empty<string>());

        result.IsSuspicious.Should().BeFalse();
    }

    [Theory]
    [InlineData("Call me on 08012345678")]
    [InlineData("My account number is 0123456789")]
    [InlineData("+234 801 234 5678 is my number")]
    public void Messages_containing_a_long_digit_sequence_are_flagged(string message)
    {
        var result = _sut.CheckMessage(message, Array.Empty<string>());

        result.IsSuspicious.Should().BeTrue();
    }

    [Fact]
    public void Short_digit_sequences_like_a_size_or_quantity_are_not_flagged()
    {
        var result = _sut.CheckMessage("I need size 42 in quantity 2", Array.Empty<string>());

        result.IsSuspicious.Should().BeFalse();
    }

    [Theory]
    [InlineData("zero eight one two three four five six seven")]
    [InlineData("Zero One Two Three Four Five Six Seven")]
    public void Spelled_out_digit_sequences_are_flagged_as_obfuscation(string message)
    {
        var result = _sut.CheckMessage(message, Array.Empty<string>());

        result.IsSuspicious.Should().BeTrue();
        result.Reason.Should().Contain("spell out a number");
    }

    [Theory]
    [InlineData("call me on whatsapp")]
    [InlineData("let's talk off-platform")]
    [InlineData("send to my account directly")]
    public void Contact_circumvention_keywords_are_flagged(string message)
    {
        var result = _sut.CheckMessage(message, Array.Empty<string>());

        result.IsSuspicious.Should().BeTrue();
    }

    [Fact]
    public void Single_digit_per_message_evasion_pattern_is_caught_across_history()
    {
        // Simulates someone spelling out "0801" by sending one digit per message
        // to dodge a single-message regex check.
        var history = new[] { "0", "8", "0" };

        var result = _sut.CheckMessage("1", history);

        result.IsSuspicious.Should().BeTrue();
        result.Reason.Should().Contain("one message at a time");
    }

    [Fact]
    public void A_single_short_message_with_no_history_is_not_flagged_by_the_pattern_check()
    {
        var result = _sut.CheckMessage("2", Array.Empty<string>());

        result.IsSuspicious.Should().BeFalse();
    }

    [Fact]
    public void Three_consecutive_short_numeric_messages_are_not_yet_enough_to_trigger_the_window()
    {
        // Window requires 4 consecutive short numeric tokens; 3 total (2 history + this one) should not trip it.
        var history = new[] { "0", "8" };

        var result = _sut.CheckMessage("0", history);

        result.IsSuspicious.Should().BeFalse();
    }

    [Fact]
    public void Empty_or_whitespace_message_is_always_clean()
    {
        _sut.CheckMessage("", Array.Empty<string>()).IsSuspicious.Should().BeFalse();
        _sut.CheckMessage("   ", Array.Empty<string>()).IsSuspicious.Should().BeFalse();
    }
}
