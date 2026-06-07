using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class TeamPaletteTests : TestClass
{
    public TeamPaletteTests(Node testScene) : base(testScene) { }

    [Test]
    public void EachOfTheFourTeams_HasADistinctColour()
    {
        var colours = new[] { TeamPalette.TintFor(0), TeamPalette.TintFor(1), TeamPalette.TintFor(2), TeamPalette.TintFor(3) };
        for (var i = 0; i < colours.Length; i++)
        {
            for (var j = i + 1; j < colours.Length; j++)
            {
                if (colours[i] == colours[j])
                {
                    throw new System.Exception($"Teams {i} and {j} must read apart; both were {colours[i]}.");
                }
            }
        }
    }

    [Test]
    public void TeamIndex_WrapsModuloTheFourColours()
    {
        // A fifth team reuses the first colour so any team index is renderable.
        if (TeamPalette.TintFor(4) != TeamPalette.TintFor(0) || TeamPalette.TintFor(-1) != TeamPalette.TintFor(3))
        {
            throw new System.Exception("Team colours must wrap modulo the palette size.");
        }
    }
}
