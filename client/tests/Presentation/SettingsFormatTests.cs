using System.Globalization;
using System.Threading;
using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// The Settings readouts must format numbers culture-invariantly. The arcade's ICU-less .NET-to-WASM
// runtime throws on ambient-culture number formatting ($"{x:F2}"), which was silently aborting
// SettingsOverlay.Build() on the web — "tap Settings, nothing happens" (owner report 2026-07-04).
public class SettingsFormatTests : TestClass
{
    public SettingsFormatTests(Node testScene) : base(testScene) { }

    [Test]
    public void Brightness_UsesInvariantDecimalPoint_UnderCommaDecimalCulture()
    {
        var prior = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("da-DK"); // comma decimal separator
        try
        {
            AssertEqual("1.00×", SettingsFormat.Brightness(1.0f));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = prior;
        }
    }

    [Test]
    public void Db_FormatsSignedInteger() => AssertEqual("-12 dB", SettingsFormat.Db(-12));

    private static void AssertEqual(string expected, string actual)
    {
        if (actual != expected)
        {
            throw new System.Exception($"Expected '{expected}', got '{actual}'.");
        }
    }
}
