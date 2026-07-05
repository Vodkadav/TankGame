using Godot;
using Chickensoft.GoDotTest;
using TankGame.Infrastructure;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class TitleSceneTests : TestClass
{
    private string _originalLocale = default!;
    private Node _title = default!;

    public TitleSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _originalLocale = TranslationServer.GetLocale();
        TranslationLoader.EnsureLoaded();
        _title = GD.Load<PackedScene>("res://src/Presentation/Title.tscn").Instantiate();
        TestScene.AddChild(_title); // runs _Ready, which builds the menu
    }

    [Cleanup]
    public void Cleanup()
    {
        TranslationServer.SetLocale(_originalLocale);
        _title.QueueFree();
    }

    [Test]
    public void Title_OffersTheSlimmedMenu()
    {
        foreach (var name in new[] { "Solo", "Multiplayer", "Settings", "Exit" })
        {
            if (_title.FindChild(name, recursive: true, owned: false) is not Button)
            {
                throw new System.Exception($"Title screen must offer a '{name}' button.");
            }
        }
    }

    [Test]
    public void Title_DropsTheOldModeAndNetButtons()
    {
        foreach (var name in new[] { "Coop", "Versus", "JoinTest", "ThreeD", "OnePlayer", "TeamVsTeam", "SelectMap", "Editor" })
        {
            if (_title.FindChild(name, recursive: true, owned: false) is Button)
            {
                throw new System.Exception($"Title screen should no longer offer a '{name}' button.");
            }
        }
    }

    [Test]
    public void Title_EnablesMultiplayer_LeadingToTheLobbyBrowser()
    {
        var multiplayer = _title.FindChild("Multiplayer", recursive: true, owned: false) as Button
            ?? throw new System.Exception("Missing 'Multiplayer' button.");
        if (multiplayer.Disabled)
        {
            throw new System.Exception("Multiplayer should be enabled — it opens the lobby browser (plan Phase 2).");
        }
    }

    // Every way into a game asks for the player's name first (owner feedback 2026-06-11): pressing a
    // play mode opens the prompt instead of starting, and confirming stores the name for the match.
    [Test]
    public void PressingSolo_OpensTheNamePrompt_AndConfirmingStoresTheName()
    {
        var previous = GameSetup.PlayerName;
        try
        {
            var prompt = _title.FindChild("NamePrompt", recursive: true, owned: false) as Control
                ?? throw new System.Exception("The title must hold a 'NamePrompt' panel.");
            if (prompt.Visible)
            {
                throw new System.Exception("The name prompt should stay hidden until a play mode is chosen.");
            }

            Press("Solo");
            if (!prompt.Visible)
            {
                throw new System.Exception("Choosing Solo must ask for the player's name first.");
            }

            var entry = _title.FindChild("NameEntry", recursive: true, owned: false) as LineEdit
                ?? throw new System.Exception("The name prompt must offer a 'NameEntry' field.");
            entry.Text = "  General Fluff  ";
            Press("NameOk");

            if (GameSetup.PlayerName != "General Fluff")
            {
                throw new System.Exception($"Confirming must store the trimmed name; got '{GameSetup.PlayerName}'.");
            }
        }
        finally
        {
            GameSetup.PlayerName = previous;
        }
    }

    private void Press(string buttonName)
    {
        var button = _title.FindChild(buttonName, recursive: true, owned: false) as Button
            ?? throw new System.Exception($"Missing '{buttonName}' button.");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    // The last button's label is platform-aware (title.back_to_arcade on web, title.exit elsewhere);
    // the test host is desktop, so it must render the Danish "Exit".
    [Test]
    public void Title_RendersTheMenuLabelsInDanish()
    {
        TranslationServer.SetLocale("dk");
        AssertButton("Solo", "Solo");
        AssertButton("Settings", "Indstillinger");
        AssertButton("Exit", "Afslut");
    }

    private void AssertButton(string name, string expected)
    {
        var button = _title.FindChild(name, recursive: true, owned: false) as Button
            ?? throw new System.Exception($"Missing '{name}' button.");
        var actual = button.Tr(button.Text).ToString();
        if (actual != expected)
        {
            throw new System.Exception($"Button '{name}' rendered '{actual}', expected '{expected}'.");
        }
    }
}
