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
            new[] { new PowerupSpawn(PowerupKind.Shield, 2, 1) });
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
}
