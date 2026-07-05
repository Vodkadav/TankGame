using System;
using System.IO;
using System.Linq;
using Xunit;

namespace TankGame.Tests.Architecture;

// The arcade ships on Godot's .NET-to-WASM runtime, which carries no ICU data: any
// culture-sensitive number formatting (an un-qualified $"{x:F2}", string.Format without an
// explicit culture, .ToString("F2")) throws there and aborts the calling scene's build — the
// "tap a button, nothing happens" class of web bug (#231, SettingsOverlay). These tests scan the
// production source so the next such call fails CI instead of the live arcade.
public class CultureSafetyTests
{
    [Fact]
    public void StringFormat_WithoutAnExplicitCulture_IsFlagged()
    {
        var source = @"label.Text = string.Format(TranslationServer.Translate(key), a, b);";

        Assert.NotEmpty(CultureSafety.FindViolations(source));
    }

    [Fact]
    public void StringFormat_LeadingWithCultureInfo_EvenAcrossLines_IsSafe()
    {
        var source = "label.Text = string.Format(\n    CultureInfo.InvariantCulture,\n    key, a);";

        Assert.Empty(CultureSafety.FindViolations(source));
    }

    [Fact]
    public void AnInterpolatedNumericFormatSpecifier_IsFlagged()
    {
        var source = @"var text = $""{multiplier:F2}x"";";

        Assert.NotEmpty(CultureSafety.FindViolations(source));
    }

    [Fact]
    public void AnInterpolatedHoleWithoutAFormatSpecifier_IsSafe()
    {
        var source = @"var text = $""{name} wins"";";

        Assert.Empty(CultureSafety.FindViolations(source));
    }

    [Fact]
    public void ACommentDescribingTheBugClass_IsNotAViolation()
    {
        var source = "// an un-qualified $\"{x:F2}\" resolves CurrentCulture and throws\nvar y = 1;";

        Assert.Empty(CultureSafety.FindViolations(source));
    }

    [Fact]
    public void ToStringWithAFormatString_AndNoCulture_IsFlagged()
    {
        var source = @"var text = multiplier.ToString(""F2"");";

        Assert.NotEmpty(CultureSafety.FindViolations(source));
    }

    [Fact]
    public void ToStringWithAFormatString_AndAnExplicitCulture_IsSafe()
    {
        var source = @"var text = multiplier.ToString(""F2"", CultureInfo.InvariantCulture);";

        Assert.Empty(CultureSafety.FindViolations(source));
    }

    [Fact]
    public void EveryProductionSourceFile_IsFreeOfCultureSensitiveFormatting()
    {
        var violations = ProductionSourceFiles()
            .SelectMany(file => CultureSafety.FindViolations(File.ReadAllText(file))
                .Select(v => $"{Path.GetFileName(file)}: {v}"))
            .OrderBy(v => v)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Culture-sensitive formatting crashes the ICU-less WASM arcade runtime (#231). "
            + "Pass CultureInfo.InvariantCulture explicitly:\n  - " + string.Join("\n  - ", violations));
    }

    private static string[] ProductionSourceFiles()
    {
        // The test runs from tests/Architecture/bin/...; the production source is client/src.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TankGame.csproj")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Directory.GetFiles(Path.Combine(dir!.FullName, "src"), "*.cs", SearchOption.AllDirectories);
    }
}
