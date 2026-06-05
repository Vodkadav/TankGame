using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>Draws a level's bushes as low isometric leafy clumps — passable cover a tank can hide in
/// (the GameLogic <c>BushField</c> / <c>IConcealment</c> decides who is concealed; this only shows
/// where). One <see cref="Sprite2D"/> per bush cell, projected onto its tile and sitting just above
/// the ground (below every tank, so a revealed tank draws over the bush). Bushes never block movement
/// or shots.</summary>
public partial class BushOverlay : Node2D
{
    // Above the ground (ZIndex -10) but below every entity, so a tank reads as standing in the bush.
    private const int BushZ = -3;

    // The art's ground contact sits 33 px from its bottom (shared by the iso tiles); centre the
    // sprite and lift it so that point lands on the cell.
    private const float BaseDiamondFromBottom = 33f;

    /// <summary>Builds a clump for every bush cell. <paramref name="bushes"/> is indexed
    /// <c>[x, y]</c>; <paramref name="tileSize"/> is the world-space size of one cell.</summary>
    public void Bind(bool[,] bushes, float tileSize)
    {
        var width = bushes.GetLength(0);
        var height = bushes.GetLength(1);
        var texture = GD.Load<Texture2D>(AssetCatalogue.Active.BushTile);
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                if (bushes[x, y])
                {
                    AddChild(BuildClump(x, y, tileSize, texture));
                }
            }
        }
    }

    private static Sprite2D BuildClump(int x, int y, float tileSize, Texture2D texture)
    {
        var centre = IsoProjection.WorldToScreen(new NVector2((x + 0.5f) * tileSize, (y + 0.5f) * tileSize));
        return new Sprite2D
        {
            Name = $"Bush_{x}_{y}",
            Texture = texture,
            Position = new Vector2(centre.X, centre.Y),
            Offset = new Vector2(0f, BaseDiamondFromBottom - (texture.GetHeight() / 2f)),
            ZIndex = BushZ,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
    }
}
