using Godot;
using Chickensoft.GoDotTest;
using TankGame.Infrastructure;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// Forces each shipped locale and asserts the networked-match status strings (connecting /
// connected / opponent joined) resolve — the i18n half of M3-T9.
public class NetStatusLocaleTests : TestClass
{
    private string _originalLocale = default!;

    public NetStatusLocaleTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalLocale = TranslationServer.GetLocale();
        TranslationLoader.EnsureLoaded();
    }

    [Cleanup]
    public void Cleanup() => TranslationServer.SetLocale(_originalLocale);

    [Test]
    public void English_RendersTheNetStatusStrings()
        => AssertAll("en", "Connecting…", "Connected", "Player 2 joined");

    [Test]
    public void Spanish_RendersTheNetStatusStrings()
        => AssertAll("es", "Conectando…", "Conectado", "Jugador 2 se unió");

    [Test]
    public void Danish_RendersTheNetStatusStrings()
        => AssertAll("dk", "Forbinder…", "Forbundet", "Spiller 2 deltog");

    private static void AssertAll(string locale, string connecting, string connected, string joined)
    {
        TranslationServer.SetLocale(locale);
        AssertKey(locale, NetStatusOverlay.ConnectingKey, connecting);
        AssertKey(locale, NetStatusOverlay.ConnectedKey, connected);
        AssertKey(locale, NetStatusOverlay.Player2JoinedKey, joined);
    }

    private static void AssertKey(string locale, string key, string expected)
    {
        var actual = TranslationServer.Translate(key).ToString();
        if (actual != expected)
        {
            throw new System.Exception($"Key '{key}' in locale '{locale}' rendered '{actual}', expected '{expected}'.");
        }
    }
}
