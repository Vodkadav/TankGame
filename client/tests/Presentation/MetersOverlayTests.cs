using Godot;
using Chickensoft.GoDotTest;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// Forces each shipped locale and asserts the meters overlay renders the localized
// "Damage … K/D …" line and updates when a hit is recorded — the S9 check.
public class MetersOverlayTests : TestClass
{
    private string _originalLocale = default!;
    private MetersOverlay _overlay = default!;
    private Label _label = default!;

    public MetersOverlayTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalLocale = TranslationServer.GetLocale();
        TranslationLoader.EnsureLoaded();

        _overlay = new MetersOverlay();
        TestScene.AddChild(_overlay); // runs _Ready, which builds the label
        _label = _overlay.FindChild("MetersReadout", recursive: true, owned: false) as Label
            ?? throw new System.Exception("Overlay must show a 'MetersReadout' Label.");
    }

    [Cleanup]
    public void Cleanup()
    {
        TranslationServer.SetLocale(_originalLocale);
        _overlay.QueueFree();
    }

    [Test]
    public void Meters_English_StartsAtZeroAndCountsADamagingKill()
        => AssertCounts("en", "Damage 0 - 0   K/D 0/0 - 0/0", "Damage 0 - 3   K/D 0/1 - 1/0");

    [Test]
    public void Meters_Spanish_StartsAtZeroAndCountsADamagingKill()
        => AssertCounts("es", "Daño 0 - 0   K/D 0/0 - 0/0", "Daño 0 - 3   K/D 0/1 - 1/0");

    [Test]
    public void Meters_Danish_StartsAtZeroAndCountsADamagingKill()
        => AssertCounts("dk", "Skade 0 - 0   K/D 0/0 - 0/0", "Skade 0 - 3   K/D 0/1 - 1/0");

    private void AssertCounts(string locale, string atZero, string afterHit)
    {
        TranslationServer.SetLocale(locale);
        var board = new MeterBoard();
        _overlay.Bind(board); // re-renders in this locale at all-zero
        AssertText(locale, atZero);

        board.Record(shooterTeam: 1, victimTeam: 0, amount: 3, killed: true); // team 1 deals 3 and kills
        AssertText(locale, afterHit);
    }

    private void AssertText(string locale, string expected)
    {
        if (_label.Text != expected)
        {
            throw new System.Exception(
                $"Meters overlay in locale '{locale}' rendered '{_label.Text}', expected '{expected}'.");
        }
    }
}
