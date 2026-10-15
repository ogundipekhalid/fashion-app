using System.Text;
using System.Text.RegularExpressions;

namespace FashionCommerce.Application.Common.Utilities;

/// <summary>Simple, dependency-free slug generator. Shared by Category/Brand/Collection/Product creation commands.</summary>
public static partial class SlugGenerator
{
    public static string Generate(string input)
    {
        var normalized = input.Trim().ToLowerInvariant();
        var withoutSpecialChars = NonAlphanumericRegex().Replace(normalized, "-");
        var collapsedDashes = MultipleDashesRegex().Replace(withoutSpecialChars, "-");
        return collapsedDashes.Trim('-');
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleDashesRegex();
}
