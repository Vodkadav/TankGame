using Godot;
using Chickensoft.GoDotTest;
using TankGame.Infrastructure;

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
        foreach (var name in new[] { "Solo", "TeamVsTeam", "SelectMap", "Editor", "Exit" })
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
        foreach (var name in new[] { "Coop", "Versus", "JoinTest", "ThreeD", "OnePlayer" })
        {
            if (_title.FindChild(name, recursive: true, owned: false) is Button)
            {
                throw new System.Exception($"Title screen should no longer offer a '{name}' button.");
            }
        }
    }

    [Test]
    public void Title_EnablesTeamVsTeam_LeadingToTheLobby()
    {
        var team = _title.FindChild("TeamVsTeam", recursive: true, owned: false) as Button
            ?? throw new System.Exception("Missing 'TeamVsTeam' button.");
        if (team.Disabled)
        {
            throw new System.Exception("Team vs Team should be enabled now that the lobby flow exists (ADR-0019 step 2).");
        }
    }

    [Test]
    public void Title_RendersTheMenuLabelsInDanish()
    {
        TranslationServer.SetLocale("dk");
        AssertButton("Solo", "Solo");
        AssertButton("SelectMap", "Vælg bane");
        AssertButton("Editor", "Baneeditor");
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
