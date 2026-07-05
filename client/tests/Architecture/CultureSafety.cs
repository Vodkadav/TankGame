using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TankGame.Tests.Architecture;

/// <summary>Finds culture-sensitive number formatting in C# source — the calls that throw on the
/// arcade's ICU-less .NET-to-WASM runtime (#231). Regex heuristics over source text, kept simple:
/// they only need to catch the three call shapes that have actually caused the bug class.</summary>
public static class CultureSafety
{
    // string.Format whose first argument is not a CultureInfo (matches across line breaks —
    // the safe call sites put CultureInfo.InvariantCulture on its own line).
    private static readonly Regex FormatWithoutCulture =
        new(@"string\.Format\((?!\s*CultureInfo\.)", RegexOptions.Compiled);

    // An interpolated string hole with a numeric format specifier ($"{x:F2}") — always resolves
    // CultureInfo.CurrentCulture, which cannot be qualified inline.
    private static readonly Regex InterpolatedNumericSpecifier =
        new(@"\$@?""[^""]*\{[^{}""]*:(?:[FfNnPpDdXxEeGgCcRr]\d*|0[0#.,]*)[^{}""]*\}", RegexOptions.Compiled);

    // .ToString("F2") with a format string but no culture argument.
    private static readonly Regex ToStringWithoutCulture =
        new(@"\.ToString\(\s*""[^""]*""\s*\)", RegexOptions.Compiled);

    // Lines that are pure comments may legitimately quote the offending call shapes (they
    // document this very rule); blank them out — preserving line breaks so reported line
    // numbers stay true — before scanning.
    private static readonly Regex CommentLines =
        new(@"^\s*(//|\*|/\*).*$", RegexOptions.Compiled | RegexOptions.Multiline);

    public static IReadOnlyList<string> FindViolations(string source)
    {
        source = CommentLines.Replace(source, "");
        var violations = new List<string>();
        foreach (Match match in FormatWithoutCulture.Matches(source))
        {
            violations.Add(Describe(source, match.Index, "string.Format without an explicit culture"));
        }

        foreach (Match match in InterpolatedNumericSpecifier.Matches(source))
        {
            violations.Add(Describe(source, match.Index, $"interpolated numeric format specifier {match.Value}"));
        }

        foreach (Match match in ToStringWithoutCulture.Matches(source))
        {
            violations.Add(Describe(source, match.Index, $"format-string ToString without a culture {match.Value}"));
        }

        return violations;
    }

    private static string Describe(string source, int index, string what)
    {
        var line = 1;
        for (var i = 0; i < index; i++)
        {
            if (source[i] == '\n')
            {
                line++;
            }
        }

        return $"line {line}: {what}";
    }
}
