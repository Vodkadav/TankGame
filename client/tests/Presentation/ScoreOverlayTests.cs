using Godot;
using Chickensoft.GoDotTest;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// Forces each shipped locale and asserts the score overlay renders the localized
// "Score {team0} - {team1}" line and updates when a kill is recorded — the LP8 check.
public class ScoreOverlayTests : TestClass
{
    private string _originalLocale = default!;
    private ScoreOverlay _overlay = default!;
    private Label _label = default!;

    public ScoreOverlayTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalLocale = TranslationServer.GetLocale();
        TranslationLoader.EnsureLoaded();

        _overlay = new ScoreOverlay();
        TestScene.AddChild(_overlay); // runs _Ready, which builds the label
        _label = _overlay.FindChild("ScoreCounter", recursive: true, owned: false) as Label
            ?? throw new System.Exception("Overlay must show a 'ScoreCounter' Label.");
    }

    [Cleanup]
    public void Cleanup()
    {
        TranslationServer.SetLocale(_originalLocale);
        _overlay.QueueFree();
    }

    [Test]
    public void Score_English_StartsAtZeroAndCountsAKill()
        => AssertCounts("en", "Score 0 - 0", "Score 1 - 0");

    [Test]
    public void Score_Spanish_StartsAtZeroAndCountsAKill()
        => AssertCounts("es", "Puntuación 0 - 0", "Puntuación 1 - 0");

    [Test]
    public void Score_Danish_StartsAtZeroAndCountsAKill()
        => AssertCounts("dk", "Score 0 - 0", "Score 1 - 0");

    private void AssertCounts(string locale, string atZero, string atOne)
    {
        TranslationServer.SetLocale(locale);
        var board = new ScoreBoard();
        _overlay.Bind(board); // re-renders in this locale at 0 - 0
        AssertText(locale, atZero);

        board.RecordKill(0); // team 0 scores -> 1 - 0
        AssertText(locale, atOne);
    }

    private void AssertText(string locale, string expected)
    {
        if (_label.Text != expected)
        {
            throw new System.Exception(
                $"Score overlay in locale '{locale}' rendered '{_label.Text}', expected '{expected}'.");
        }
    }
}
