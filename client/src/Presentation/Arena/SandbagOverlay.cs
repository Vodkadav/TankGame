using Godot;

namespace TankGame.Presentation;

/// <summary>Draws a level's sandbags as opaque khaki patches — passable terrain that slows a tank
/// crossing it (the GameLogic <c>SandbagField</c> / <c>ITerrain</c> applies the slow; this only shows
/// where). A pure mirror built in code — one <see cref="Polygon2D"/> per sandbag cell — so no art
/// asset is needed. Sandbags never block movement or shots.</summary>
public partial class SandbagOverlay : Node2D
{
    private static readonly Color SandbagColour = new(0.62f, 0.52f, 0.30f, 0.9f);
    private const float Inset = 2f;

    /// <summary>Builds a patch for every sandbag cell. <paramref name="sandbags"/> is indexed
    /// <c>[x, y]</c>; <paramref name="tileSize"/> is the world-space size of one cell.</summary>
    public void Bind(bool[,] sandbags, float tileSize)
    {
        Transform = IsoProjection.ScreenTransform; // shear the square patches into iso diamonds
        var width = sandbags.GetLength(0);
        var height = sandbags.GetLength(1);
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                if (sandbags[x, y])
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
            Name = $"Sandbag_{x}_{y}",
            Position = new Vector2(x * tileSize, y * tileSize),
            Color = SandbagColour,
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
