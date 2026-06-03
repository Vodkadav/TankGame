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
    public void Title_OffersAButtonForEveryMode()
    {
        foreach (var name in new[] { "OnePlayer", "Coop", "Versus" })
        {
            if (_title.FindChild(name, recursive: true, owned: false) is not Button)
            {
                throw new System.Exception($"Title screen must offer a '{name}' mode button.");
            }
        }
    }

    [Test]
    public void Title_RendersTheModeLabelsInDanish()
    {
        TranslationServer.SetLocale("dk");
        AssertButton("OnePlayer", "1 spiller");
        AssertButton("Coop", "2 spillere — Sammen");
        AssertButton("Versus", "2 spillere — Mod hinanden");
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
