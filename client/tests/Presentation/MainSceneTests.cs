using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Chickensoft.GodotTestDriver;
using Godot;
using Shouldly;

namespace TankGame.Tests.Presentation;

public class MainSceneTests : TestClass
{
    private Fixture _fixture = default!;

    public MainSceneTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _fixture = new Fixture(TestScene.GetTree());
    }

    [Test]
    public async Task MainSceneLoads()
    {
        var scene = await _fixture.LoadAndAddScene<CanvasLayer>("res://Main.tscn");
        scene.ShouldNotBeNull();
    }

    [Test]
    public async Task LabelStartsWithTankGame()
    {
        var scene = await _fixture.LoadAndAddScene<CanvasLayer>("res://Main.tscn");
        var label = scene.FindChild("BootLabel", true, false) as Label;
        label.ShouldNotBeNull();
        label.Text.ShouldStartWith("TankGame");
    }

    [Cleanup]
    public async Task Cleanup() => await _fixture.Cleanup();
}
