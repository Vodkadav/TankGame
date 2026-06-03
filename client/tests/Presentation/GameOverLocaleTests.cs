using Godot;
using Chickensoft.GoDotTest;
using TankGame.Infrastructure;

namespace TankGame.Tests.Presentation;

// Forces each shipped locale and asserts the round-end HUD strings (win / lose / draw /
// restart) resolve — the i18n half of C6's round flow.
public class GameOverLocaleTests : TestClass
{
    private string _originalLocale = default!;

    public GameOverLocaleTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalLocale = TranslationServer.GetLocale();
        TranslationLoader.EnsureLoaded();
    }

    [Cleanup]
    public void Cleanup() => TranslationServer.SetLocale(_originalLocale);

    [Test]
    public void English_RendersTheRoundEndStrings()
        => AssertAll("en", "You win!", "You lose!", "Draw!", "Play again", "Player 1 wins!", "Player 2 wins!");

    [Test]
    public void Spanish_RendersTheRoundEndStrings()
        => AssertAll("es", "¡Ganaste!", "¡Perdiste!", "¡Empate!", "Jugar de nuevo", "¡Jugador 1 gana!", "¡Jugador 2 gana!");

    [Test]
    public void Danish_RendersTheRoundEndStrings()
        => AssertAll("dk", "Du vandt!", "Du tabte!", "Uafgjort!", "Spil igen", "Spiller 1 vinder!", "Spiller 2 vinder!");

    private static void AssertAll(string locale, string win, string lose, string draw, string restart, string p1, string p2)
    {
        TranslationServer.SetLocale(locale);
        AssertKey(locale, "hud.you_win", win);
        AssertKey(locale, "hud.you_lose", lose);
        AssertKey(locale, "hud.draw", draw);
        AssertKey(locale, "hud.restart", restart);
        AssertKey(locale, "hud.p1_wins", p1);
        AssertKey(locale, "hud.p2_wins", p2);
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
