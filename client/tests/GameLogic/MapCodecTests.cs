using System;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class MapCodecTests
{
    private static MapDefinition SampleMap()
    {
        var map = MapDefinition.CreateBlank("Sample", 6, 5);
        map.Materials[2, 2] = CellMaterial.Brick;
        map.Materials[3, 2] = CellMaterial.Water;
        map.Materials[4, 1] = CellMaterial.Mountain;
        map.Bushes[1, 3] = true;
        map.Sandbags[2, 3] = true;
        return new MapDefinition(
            map.Name, map.Materials, map.Bushes, map.Sandbags,
            (1, 1),
            new (int X, int Y)[] { (4, 3) },
            new[] { new PowerupSpawn(PowerupKind.Shield, 2, 1) },
            new[] { new TeleportPadLink(1, 1, 4, 3) });
    }

    [Fact]
    public void EncodeThenDecode_RoundTripsEveryField()
    {
        var original = SampleMap();

        var restored = MapCodec.Decode(MapCodec.Encode(original));

        Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Width, restored.Width);
        Assert.Equal(original.Height, restored.Height);
        Assert.Equal(original.PlayerSpawn, restored.PlayerSpawn);
        Assert.Equal(original.EnemySpawns, restored.EnemySpawns);
        Assert.Equal(original.PowerupSpawns, restored.PowerupSpawns);
        Assert.Equal(original.TeleportPads, restored.TeleportPads);

        for (var y = 0; y < original.Height; y++)
        {
            for (var x = 0; x < original.Width; x++)
            {
                Assert.Equal(original.Materials[x, y], restored.Materials[x, y]);
                Assert.Equal(original.Bushes[x, y], restored.Bushes[x, y]);
                Assert.Equal(original.Sandbags[x, y], restored.Sandbags[x, y]);
            }
        }
    }

    [Fact]
    public void Decode_ThrowsOnUnknownTerrainGlyph()
    {
        var json = MapCodec.Encode(MapDefinition.CreateBlank("x", 4, 4))
            .Replace('.', '?');

        Assert.Throws<MapFormatException>(() => MapCodec.Decode(json));
    }

    [Fact]
    public void Decode_ThrowsOnMalformedJson()
    {
        Assert.Throws<MapFormatException>(() => MapCodec.Decode("not json at all"));
    }

    [Fact]
    public void GroundTheme_RoundTrips_AndAMissingField_MeansSand()
    {
        var themed = new MapDefinition(
            "Mars Base", MapDefinition.CreateBlank("x", 4, 4).Materials,
            new bool[4, 4], new bool[4, 4], (1, 1),
            new (int X, int Y)[] { (2, 2) }, System.Array.Empty<PowerupSpawn>(),
            groundTheme: GroundTheme.Mars);

        Assert.Equal(GroundTheme.Mars, MapCodec.Decode(MapCodec.Encode(themed)).GroundTheme);

        // A pre-theme document has no "groundTheme" key — it must decode as the sandy default.
        var sandy = MapCodec.Decode(MapCodec.Encode(MapDefinition.CreateBlank("Plain", 4, 4)));
        Assert.Equal(GroundTheme.Sand, sandy.GroundTheme);
        Assert.DoesNotContain("groundTheme", MapCodec.Encode(MapDefinition.CreateBlank("Plain", 4, 4)));
    }

    [Fact]
    public void Transforms_RoundTrip_AndAMissingField_MeansUntouched()
    {
        var blank = MapDefinition.CreateBlank("Posey", 4, 4);
        var posed = new MapDefinition(
            blank.Name, blank.Materials, blank.Bushes, blank.Sandbags, (1, 1),
            new (int X, int Y)[] { (2, 2) }, System.Array.Empty<PowerupSpawn>(),
            transforms: new System.Collections.Generic.Dictionary<(int X, int Y), PropTransform>
            {
                [(1, 2)] = new(YawDeg: 37.5f, PitchDeg: -10f, RollDeg: 90f, Scale: 1.75f),
            });

        var restored = MapCodec.Decode(MapCodec.Encode(posed));
        Assert.NotNull(restored.Transforms);
        Assert.Equal(new PropTransform(37.5f, -10f, 90f, 1.75f), restored.Transforms![(1, 2)]);

        var plain = MapCodec.Decode(MapCodec.Encode(MapDefinition.CreateBlank("Plain", 4, 4)));
        Assert.Null(plain.Transforms);
        Assert.DoesNotContain("transforms", MapCodec.Encode(MapDefinition.CreateBlank("Plain", 4, 4)));
    }

    [Fact]
    public void Decode_ConvertsLegacyQuarterTurnOrientations_ToYawTransforms()
    {
        // A #199-era document stored quarter turns as glyph rows; it must decode to the equivalent
        // free-rotation pose (the renderer applied -90° per turn) so old saves keep their look.
        var legacy = "{\"name\":\"Legacy\",\"width\":3,\"height\":3," +
            "\"materials\":[\"###\",\"#x#\",\"###\"],\"bushes\":[\"...\",\"...\",\"...\"]," +
            "\"sandbags\":[\"...\",\"...\",\"...\"],\"orientations\":[\"000\",\"030\",\"000\"]," +
            "\"playerSpawn\":[1,1],\"enemySpawns\":[[1,1]],\"powerupSpawns\":[]}";

        var restored = MapCodec.Decode(legacy);

        Assert.NotNull(restored.Transforms);
        Assert.Equal(new PropTransform(-270f, 0f, 0f, 1f), restored.Transforms![(1, 1)]);
    }

    [Fact]
    public void Decode_TreatsAMissingTeleportPadsField_AsNoPads()
    {
        // A pre-teleport document has no "teleportPads" key — it must still decode (backward-compatible).
        var legacy = "{\"name\":\"Legacy\",\"width\":3,\"height\":3," +
            "\"materials\":[\"###\",\"#.#\",\"###\"],\"bushes\":[\"...\",\"...\",\"...\"]," +
            "\"sandbags\":[\"...\",\"...\",\"...\"],\"playerSpawn\":[1,1]," +
            "\"enemySpawns\":[[1,1]],\"powerupSpawns\":[]}";

        var restored = MapCodec.Decode(legacy);

        Assert.Empty(restored.TeleportPads);
    }

    // ── Elevation (ADR-0020 Wave B step 5) ──

    private static MapDefinition ElevatedMap()
    {
        var map = MapDefinition.CreateBlank("Cliffy", 6, 5);
        var layers = new int[6, 5];
        layers[3, 2] = 1;
        layers[4, 2] = 2;
        var ramps = new bool[6, 5];
        ramps[2, 2] = true;
        return new MapDefinition(
            map.Name, map.Materials, map.Bushes, map.Sandbags,
            (1, 1), new (int X, int Y)[] { (4, 3) }, System.Array.Empty<PowerupSpawn>(),
            layers: layers, ramps: ramps);
    }

    [Fact]
    public void EncodeThenDecode_RoundTripsLayersAndRamps()
    {
        var original = ElevatedMap();

        var restored = MapCodec.Decode(MapCodec.Encode(original));

        Assert.NotNull(restored.Layers);
        Assert.NotNull(restored.Ramps);
        for (var y = 0; y < original.Height; y++)
        {
            for (var x = 0; x < original.Width; x++)
            {
                Assert.Equal(original.Layers![x, y], restored.Layers![x, y]);
                Assert.Equal(original.Ramps![x, y], restored.Ramps![x, y]);
            }
        }
    }

    [Fact]
    public void Encode_OmitsElevation_OnAFlatMap_AndDecodeRestoresFlat()
    {
        // A flat map stays the lean pre-elevation document — and any pre-elevation document
        // (no "layers"/"ramps" keys) keeps decoding as flat (backward-compatible).
        var json = MapCodec.Encode(MapDefinition.CreateBlank("Flat", 4, 4));

        Assert.DoesNotContain("\"layers\"", json);
        Assert.DoesNotContain("\"ramps\"", json);

        var restored = MapCodec.Decode(json);
        Assert.Null(restored.Layers);
        Assert.Null(restored.Ramps);
    }

    [Fact]
    public void Decode_ThrowsOnANonDigitLayerGlyph()
    {
        var json = MapCodec.Encode(ElevatedMap()).Replace('2', '?');

        Assert.Throws<MapFormatException>(() => MapCodec.Decode(json));
    }
}
