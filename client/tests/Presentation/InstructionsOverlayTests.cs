using Godot;
using Chickensoft.GoDotTest;
using TankGame.Infrastructure;

namespace TankGame.Tests.Presentation;

// Forces each shipped locale and asserts the Arena's instructions overlay renders
// the localized "how to play" line — the M1-T8 acceptance check.
public class InstructionsOverlayTests : TestClass
{
    private string _originalLocale = default!;
    private Node _arena = default!;
    private Label _label = default!;

    public InstructionsOverlayTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalLocale = TranslationServer.GetLocale();
        TranslationLoader.EnsureLoaded();

        _arena = GD.Load<PackedScene>("res://src/Presentation/Arena/Arena.tscn").Instantiate();
        TestScene.AddChild(_arena); // runs ArenaScene._Ready, which builds the overlay

        _label = _arena.FindChild("Instructions", recursive: true, owned: false) as Label
            ?? throw new System.Exception("Arena must show an 'Instructions' overlay Label.");
    }

    [Cleanup]
    public void Cleanup()
    {
        TranslationServer.SetLocale(_originalLocale);
        _arena.QueueFree();
    }

    [Test]
    public void Instructions_English_RendersHowToPlay()
        => AssertRendered("en", "WASD to move, mouse to aim, click to fire");

    [Test]
    public void Instructions_Spanish_RendersHowToPlay()
        => AssertRendered("es", "WASD para mover, ratón para apuntar, clic para disparar");

    [Test]
    public void Instructions_Danish_RendersHowToPlay()
        => AssertRendered("dk", "WASD for at bevæge dig, mus for at sigte, klik for at skyde");

    private void AssertRendered(string locale, string expected)
    {
        TranslationServer.SetLocale(locale);
        var actual = _label.Tr(_label.Text);

        if (actual != expected)
        {
            throw new System.Exception(
                $"Instructions overlay in locale '{locale}' rendered " +
                $"'{actual}', expected '{expected}'.");
        }
    }
}
