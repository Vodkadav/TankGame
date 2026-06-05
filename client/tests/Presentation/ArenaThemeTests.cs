using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class ArenaThemeTests : TestClass
{
    public ArenaThemeTests(Node testScene) : base(testScene) { }

    [Test]
    public void Default_IsTheSandyTheme()
    {
        if (ArenaTheme.Default != ArenaTheme.Sandy)
        {
            throw new System.Exception("The default arena theme should be Sandy.");
        }
    }

    [Test]
    public void Themes_HaveDistinctGrounds()
    {
        if (ArenaTheme.Sandy.Ground == ArenaTheme.Slate.Ground)
        {
            throw new System.Exception("Each theme needs its own ground palette so the seam is swappable.");
        }
    }

    [Test]
    public void EveryTheme_HasAnOpaqueRealGround()
    {
        foreach (var theme in new[] { ArenaTheme.Sandy, ArenaTheme.Slate })
        {
            if (theme.Ground.A < 0.99f || theme.Ground == Colors.Black)
            {
                throw new System.Exception($"A theme's ground must be a real, opaque colour; was {theme.Ground}.");
            }
        }
    }
}
