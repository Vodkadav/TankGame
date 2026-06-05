using Godot;

namespace TankGame.Presentation;

/// <summary>Draws a level's bushes as translucent green patches — passable cover a tank can
/// hide in (the GameLogic <c>BushField</c> / <c>IConcealment</c> decides who is concealed; this
/// only shows where). A pure mirror built entirely in code — one <see cref="Polygon2D"/> per
/// bush cell — so no art asset is needed. Bushes never block movement or shots.</summary>
public partial class BushOverlay : Node2D
{
    private static readonly Color BushColour = new(0.20f, 0.55f, 0.20f, 0.55f);
    private const float Inset = 4f;

    /// <summary>Builds a patch for every bush cell. <paramref name="bushes"/> is indexed
    /// <c>[x, y]</c>; <paramref name="tileSize"/> is the world-space size of one cell.</summary>
    public void Bind(bool[,] bushes, float tileSize)
    {
        Transform = IsoProjection.ScreenTransform; // shear the square patches into iso diamonds
        var width = bushes.GetLength(0);
        var height = bushes.GetLength(1);
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                if (bushes[x, y])
                {
                    AddChild(BuildPatch(x, y, tileSize));
                }
            }
        }
    }

    private static Polygon2D BuildPatch(int x, int y, float tileSize)
    {
        var min = Inset;
        var max = tileSize - Inset;
        return new Polygon2D
        {
            Name = $"Bush_{x}_{y}",
            Position = new Vector2(x * tileSize, y * tileSize),
            Color = BushColour,
            Polygon = new[]
            {
                new Vector2(min, min),
                new Vector2(max, min),
                new Vector2(max, max),
                new Vector2(min, max),
            },
        };
    }
}
