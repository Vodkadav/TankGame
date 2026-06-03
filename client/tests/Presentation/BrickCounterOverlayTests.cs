using Godot;
using Chickensoft.GoDotTest;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using TankGame.Presentation;
using DomainCell = TankGame.Domain.CellMaterial;

namespace TankGame.Tests.Presentation;

// Forces each shipped locale and asserts the brick-counter dev overlay renders the
// localized "Bricks destroyed: N" line and increments when a brick breaks — the M2-T7 check.
public class BrickCounterOverlayTests : TestClass
{
    private string _originalLocale = default!;
    private BrickCounterOverlay _overlay = default!;
    private Label _label = default!;

    public BrickCounterOverlayTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalLocale = TranslationServer.GetLocale();
        TranslationLoader.EnsureLoaded();

        _overlay = new BrickCounterOverlay();
        TestScene.AddChild(_overlay); // runs _Ready, which builds the label
        _label = _overlay.FindChild("BrickCounter", recursive: true, owned: false) as Label
            ?? throw new System.Exception("Overlay must show a 'BrickCounter' Label.");
    }

    [Cleanup]
    public void Cleanup()
    {
        TranslationServer.SetLocale(_originalLocale);
        _overlay.QueueFree();
    }

    [Test]
    public void Counter_English_StartsAtZeroAndCountsADestroyedBrick()
        => AssertCounts("en", "Bricks destroyed: 0", "Bricks destroyed: 1");

    [Test]
    public void Counter_Spanish_StartsAtZeroAndCountsADestroyedBrick()
        => AssertCounts("es", "Ladrillos destruidos: 0", "Ladrillos destruidos: 1");

    [Test]
    public void Counter_Danish_StartsAtZeroAndCountsADestroyedBrick()
        => AssertCounts("dk", "Mursten ødelagt: 0", "Mursten ødelagt: 1");

    private void AssertCounts(string locale, string atZero, string atOne)
    {
        TranslationServer.SetLocale(locale);
        var grid = WallGrid.FromMaterials(new[,] { { DomainCell.Brick } });
        _overlay.Bind(grid); // re-renders in this locale at count 0
        AssertText(locale, atZero);

        grid.DamageCell(0, 0, WallGrid.DefaultBrickHp); // breaks the brick -> count 1
        AssertText(locale, atOne);
    }

    private void AssertText(string locale, string expected)
    {
        if (_label.Text != expected)
        {
            throw new System.Exception(
                $"Brick counter in locale '{locale}' rendered '{_label.Text}', expected '{expected}'.");
        }
    }
}
