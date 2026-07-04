using System;
using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class RespawnHudTests : TestClass
{
    private RespawnHud _hud = default!;

    public RespawnHudTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _hud = new RespawnHud();
        TestScene.AddChild(_hud); // runs _Ready, which builds the icon row
    }

    [Cleanup]
    public void Cleanup() => _hud.QueueFree();

    [Test]
    public void Hud_BuildsFiveTankIcons_AllLitToStart()
    {
        for (var i = 0; i < RespawnHud.MaxIcons; i++)
        {
            var icon = Icon(i);
            if (icon.IsSpent)
            {
                throw new Exception($"Icon {i} should start lit (a respawn in hand), not spent.");
            }
        }
    }

    [Test]
    public void Show_LightsRespawnsRemaining_AndDimsTheSpentOnes()
    {
        _hud.Show(3);

        for (var i = 0; i < RespawnHud.MaxIcons; i++)
        {
            var expectedSpent = i >= 3;
            if (Icon(i).IsSpent != expectedSpent)
            {
                throw new Exception($"With 3 respawns left, icon {i} spent should be {expectedSpent}; was {Icon(i).IsSpent}.");
            }
        }
    }

    [Test]
    public void Show_ClampsOutOfRangeCounts()
    {
        _hud.Show(0);
        for (var i = 0; i < RespawnHud.MaxIcons; i++)
        {
            if (!Icon(i).IsSpent)
            {
                throw new Exception($"With 0 respawns, every icon should be spent; icon {i} was lit.");
            }
        }

        _hud.Show(99); // more than there are → all lit, no crash
        for (var i = 0; i < RespawnHud.MaxIcons; i++)
        {
            if (Icon(i).IsSpent)
            {
                throw new Exception($"Show(99) should light every icon; icon {i} was spent.");
            }
        }
    }

    private TankIcon Icon(int i) => _hud.GetNode<TankIcon>($"Icons/TankIcon{i}");
}
