using Godot;
using TankGame.GameLogic;

namespace TankGame.Presentation;

/// <summary>Maps each <see cref="GroundTheme"/> to its ground material: the same noise-mottled
/// patchwork technique the launch sand used, with per-theme colours (and a finer noise for the
/// parking lot, so its patches read as small pebble speckles). One source of truth, so the play
/// arena and the editor's WYSIWYG ground agree.</summary>
public static class GroundThemes
{
    /// <summary>The themed ground material for a field of the given size (the UV scale tiles the
    /// noise every ~4 cells, matching the launch look).</summary>
    public static StandardMaterial3D Material(GroundTheme theme, int widthCells, int heightCells)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = theme == GroundTheme.ParkingLot ? 0.35f : 0.05f, // fine grain = pebble dots
        };
        var ramp = new Gradient
        {
            Offsets = new[] { 0f, 0.34f, 0.67f, 1f },
            Colors = ColoursFor(theme),
        };
        var texture = new NoiseTexture2D { Noise = noise, Width = 256, Height = 256, Seamless = true, ColorRamp = ramp };

        return new StandardMaterial3D
        {
            AlbedoTexture = texture,
            Uv1Scale = new Vector3(widthCells / 4f, heightCells / 4f, 1f),
            Roughness = 1f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
        };
    }

    private static Color[] ColoursFor(GroundTheme theme) => theme switch
    {
        GroundTheme.Jungle => new[]
        {
            new Color(0.10f, 0.30f, 0.12f), // deep undergrowth
            new Color(0.18f, 0.42f, 0.16f), // leaf green
            new Color(0.30f, 0.52f, 0.22f), // sunlit moss
            new Color(0.14f, 0.36f, 0.20f), // damp fern
        },
        GroundTheme.Mars => new[]
        {
            new Color(0.22f, 0.05f, 0.04f), // near-black rust
            new Color(0.36f, 0.09f, 0.06f), // dark red regolith
            new Color(0.48f, 0.14f, 0.08f), // iron oxide
            new Color(0.30f, 0.08f, 0.06f), // shadowed dune
        },
        GroundTheme.ParkingLot => new[]
        {
            new Color(0.30f, 0.30f, 0.31f), // darker pebble speckle
            new Color(0.46f, 0.46f, 0.47f), // asphalt
            new Color(0.52f, 0.52f, 0.53f), // worn asphalt
            new Color(0.40f, 0.40f, 0.41f), // patch repair
        },
        _ => new[] // Sand — the launch patchwork
        {
            new Color(0.40f, 0.47f, 0.30f), // green
            new Color(0.52f, 0.40f, 0.26f), // brown
            new Color(0.78f, 0.67f, 0.42f), // yellow sand
            new Color(0.54f, 0.54f, 0.52f), // grey
        },
    };
}
