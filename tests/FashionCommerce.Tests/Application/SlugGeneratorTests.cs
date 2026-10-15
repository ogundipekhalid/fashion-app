using FashionCommerce.Application.Common.Utilities;
using FluentAssertions;
using Xunit;

namespace FashionCommerce.Tests.Application;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Blue Ankara Shirt", "blue-ankara-shirt")]
    [InlineData("Men's Casual Wear", "men-s-casual-wear")]
    [InlineData("  Extra   Spaces  ", "extra-spaces")]
    [InlineData("UPPERCASE NAME", "uppercase-name")]
    [InlineData("Special!!@@Characters##", "special-characters")]
    public void Generate_produces_expected_slug(string input, string expected)
    {
        SlugGenerator.Generate(input).Should().Be(expected);
    }

    [Fact]
    public void Generate_never_produces_leading_or_trailing_dashes()
    {
        var slug = SlugGenerator.Generate("!!!Wrapped In Symbols!!!");

        slug.Should().NotStartWith("-");
        slug.Should().NotEndWith("-");
    }

    [Fact]
    public void Generate_collapses_multiple_consecutive_separators_into_one_dash()
    {
        var slug = SlugGenerator.Generate("Too---Many___Dashes");

        slug.Should().NotContain("--");
    }
}
