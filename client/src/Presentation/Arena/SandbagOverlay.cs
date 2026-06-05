using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>Draws a level's sandbags as low isometric bag clusters — passable terrain that slows a
/// tank crossing it (the GameLogic <c>SandbagField</c> / <c>ITerrain</c> applies the slow; this only
/// shows where). One <see cref="Sprite2D"/> per sandbag cell, projected onto its tile and sitting just
/// above the ground (below every tank, so a tank drives over them). Sandbags never block movement or
/// shots.</summary>
public partial class SandbagOverlay : Node2D
{
    // Above the ground (ZIndex -10) but below every entity, so a tank reads as crossing the bags.
    private const int SandbagZ = -3;

    // The art's ground contact sits 33 px from its bottom (shared by the iso tiles); centre the
    // sprite and lift it so that point lands on the cell.
    private const float BaseDiamondFromBottom = 33f;

    /// <summary>Builds a cluster for every sandbag cell. <paramref name="sandbags"/> is indexed
    /// <c>[x, y]</c>; <paramref name="tileSize"/> is the world-space size of one cell.</summary>
    public void Bind(bool[,] sandbags, float tileSize)
    {
        var width = sandbags.GetLength(0);
        var height = sandbags.GetLength(1);
        var texture = GD.Load<Texture2D>(AssetCatalogue.Active.SandbagTile);
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                if (sandbags[x, y])
                {
                    AddChild(BuildCluster(x, y, tileSize, texture));
                }
            }
        }
    }

    private static Sprite2D BuildCluster(int x, int y, float tileSize, Texture2D texture)
    {
        var centre = IsoProjection.WorldToScreen(new NVector2((x + 0.5f) * tileSize, (y + 0.5f) * tileSize));
        return new Sprite2D
        {
            Name = $"Sandbag_{x}_{y}",
            Texture = texture,
            Position = new Vector2(centre.X, centre.Y),
            Offset = new Vector2(0f, BaseDiamondFromBottom - (texture.GetHeight() / 2f)),
            ZIndex = SandbagZ,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
    }
}
