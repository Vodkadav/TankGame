using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>Thrown when <see cref="MapCodec.Decode"/> is handed text that is not a valid map document
/// (malformed JSON, a missing field, a ragged grid, or an unknown glyph).</summary>
public sealed class MapFormatException : Exception
{
    public MapFormatException(string message)
        : base(message)
    {
    }

    public MapFormatException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>Serialises a <see cref="MapDefinition"/> to and from JSON. The grid layers are written as
/// one string per row of single-character glyphs, which keeps the document human-readable and diffable
/// (and trivially turned into a share string later). Hand-rolled rather than reflection-based because
/// <see cref="System.Text.Json"/> cannot serialise a multidimensional <c>CellMaterial[,]</c>. Pure C#.
/// </summary>
public static class MapCodec
{
    private static readonly IReadOnlyDictionary<CellMaterial, char> MaterialGlyph =
        new Dictionary<CellMaterial, char>
        {
            [CellMaterial.Floor] = '.',
            [CellMaterial.Brick] = 'x',
            [CellMaterial.Steel] = '#',
            [CellMaterial.Crate] = 'c',
            [CellMaterial.Water] = '~',
            [CellMaterial.Bridge] = '=',
            [CellMaterial.Mountain] = '^',
            [CellMaterial.Building] = 'B',
        };

    private static readonly IReadOnlyDictionary<char, CellMaterial> GlyphMaterial = InvertGlyphs();

    private const char OverlayOn = 'b';
    private const char OverlayOff = '.';
    private const char RampOn = 'r';

    public static string Encode(MapDefinition map)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("name", map.Name);
            writer.WriteNumber("width", map.Width);
            writer.WriteNumber("height", map.Height);

            // Only when authored — the sandy default keeps the lean pre-theme document shape.
            if (map.GroundTheme != GroundTheme.Sand)
            {
                writer.WriteString("groundTheme", map.GroundTheme.ToString());
            }

            WriteRows(writer, "materials", map.Width, map.Height, (x, y) => MaterialGlyph[map.Materials[x, y]]);
            WriteRows(writer, "bushes", map.Width, map.Height, (x, y) => map.Bushes[x, y] ? OverlayOn : OverlayOff);
            WriteRows(writer, "sandbags", map.Width, map.Height, (x, y) => map.Sandbags[x, y] ? OverlayOn : OverlayOff);

            // Elevation only when authored — a flat map keeps the lean pre-elevation document shape.
            if (map.Layers is { } layers)
            {
                WriteRows(writer, "layers", map.Width, map.Height, (x, y) => (char)('0' + layers[x, y]));
            }

            if (map.Ramps is { } ramps)
            {
                WriteRows(writer, "ramps", map.Width, map.Height, (x, y) => ramps[x, y] ? RampOn : OverlayOff);
            }

            // Poses only when authored — an untouched map keeps the lean document shape.
            if (map.Transforms is { Count: > 0 } transforms)
            {
                writer.WriteStartArray("transforms");
                foreach (var ((x, y), t) in transforms)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("x", x);
                    writer.WriteNumber("y", y);
                    writer.WriteNumber("yaw", t.YawDeg);
                    writer.WriteNumber("pitch", t.PitchDeg);
                    writer.WriteNumber("roll", t.RollDeg);
                    writer.WriteNumber("scale", t.Scale);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            WriteCell(writer, "playerSpawn", map.PlayerSpawn.X, map.PlayerSpawn.Y);

            writer.WriteStartArray("enemySpawns");
            foreach (var (x, y) in map.EnemySpawns)
            {
                WriteCellArray(writer, x, y);
            }

            writer.WriteEndArray();

            writer.WriteStartArray("powerupSpawns");
            foreach (var spawn in map.PowerupSpawns)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", spawn.Kind.ToString());
                writer.WriteNumber("x", spawn.X);
                writer.WriteNumber("y", spawn.Y);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            // Decorations only when placed — a prop-free map keeps the lean document shape.
            if (map.Decorations.Count > 0)
            {
                writer.WriteStartArray("decorations");
                foreach (var decoration in map.Decorations)
                {
                    writer.WriteStartObject();
                    writer.WriteString("asset", decoration.AssetId);
                    writer.WriteNumber("x", decoration.X);
                    writer.WriteNumber("y", decoration.Y);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteStartArray("teleportPads");
            foreach (var pad in map.TeleportPads)
            {
                writer.WriteStartObject();
                writer.WriteNumber("ax", pad.AX);
                writer.WriteNumber("ay", pad.AY);
                writer.WriteNumber("bx", pad.BX);
                writer.WriteNumber("by", pad.BY);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static MapDefinition Decode(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new MapFormatException("map document is not valid JSON", ex);
        }

        using (doc)
        {
            try
            {
                var root = doc.RootElement;
                var name = root.GetProperty("name").GetString()
                    ?? throw new MapFormatException("map is missing 'name'");
                var width = root.GetProperty("width").GetInt32();
                var height = root.GetProperty("height").GetInt32();

                var materials = ReadMaterials(root, width, height);
                var bushes = ReadOverlay(root, "bushes", width, height);
                var sandbags = ReadOverlay(root, "sandbags", width, height);
                var playerSpawn = ReadCell(root.GetProperty("playerSpawn"));

                var enemySpawns = new List<(int X, int Y)>();
                foreach (var cell in root.GetProperty("enemySpawns").EnumerateArray())
                {
                    enemySpawns.Add(ReadCell(cell));
                }

                var powerupSpawns = new List<PowerupSpawn>();
                foreach (var spawn in root.GetProperty("powerupSpawns").EnumerateArray())
                {
                    var kindText = spawn.GetProperty("kind").GetString();
                    if (!Enum.TryParse<PowerupKind>(kindText, out var kind))
                    {
                        throw new MapFormatException($"unknown powerup kind '{kindText}'");
                    }

                    powerupSpawns.Add(new PowerupSpawn(kind, spawn.GetProperty("x").GetInt32(), spawn.GetProperty("y").GetInt32()));
                }

                var teleportPads = new List<TeleportPadLink>();
                if (root.TryGetProperty("teleportPads", out var padsElement))
                {
                    foreach (var pad in padsElement.EnumerateArray())
                    {
                        teleportPads.Add(new TeleportPadLink(
                            pad.GetProperty("ax").GetInt32(), pad.GetProperty("ay").GetInt32(),
                            pad.GetProperty("bx").GetInt32(), pad.GetProperty("by").GetInt32()));
                    }
                }

                var layers = ReadLayers(root, width, height);
                var ramps = root.TryGetProperty("ramps", out _)
                    ? ReadRampOverlay(root, width, height)
                    : null;

                var groundTheme = GroundTheme.Sand;
                if (root.TryGetProperty("groundTheme", out var themeElement))
                {
                    if (!Enum.TryParse(themeElement.GetString(), out groundTheme))
                    {
                        throw new MapFormatException($"unknown ground theme '{themeElement.GetString()}'");
                    }
                }

                var decorations = new List<Decoration>();
                if (root.TryGetProperty("decorations", out var decorationsElement))
                {
                    foreach (var entry in decorationsElement.EnumerateArray())
                    {
                        decorations.Add(new Decoration(
                            entry.GetProperty("asset").GetString()
                                ?? throw new MapFormatException("decoration is missing 'asset'"),
                            entry.GetProperty("x").GetInt32(),
                            entry.GetProperty("y").GetInt32()));
                    }
                }

                return new MapDefinition(
                    name, materials, bushes, sandbags, playerSpawn, enemySpawns, powerupSpawns,
                    teleportPads, layers, ramps, groundTheme, ReadTransforms(root, width, height),
                    decorations);
            }
            catch (KeyNotFoundException ex)
            {
                throw new MapFormatException("map document is missing a required field", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new MapFormatException("map document has a field of the wrong type", ex);
            }
        }
    }

    private static void WriteRows(
        Utf8JsonWriter writer, string property, int width, int height, Func<int, int, char> glyphAt)
    {
        writer.WriteStartArray(property);
        var row = new char[width];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                row[x] = glyphAt(x, y);
            }

            writer.WriteStringValue(new string(row));
        }

        writer.WriteEndArray();
    }

    private static void WriteCell(Utf8JsonWriter writer, string property, int x, int y)
    {
        writer.WriteStartArray(property);
        writer.WriteNumberValue(x);
        writer.WriteNumberValue(y);
        writer.WriteEndArray();
    }

    private static void WriteCellArray(Utf8JsonWriter writer, int x, int y)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(x);
        writer.WriteNumberValue(y);
        writer.WriteEndArray();
    }

    private static CellMaterial[,] ReadMaterials(JsonElement root, int width, int height)
    {
        var rows = ReadRowStrings(root, "materials", width, height);
        var materials = new CellMaterial[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!GlyphMaterial.TryGetValue(rows[y][x], out var material))
                {
                    throw new MapFormatException($"unknown terrain glyph '{rows[y][x]}' at ({x},{y})");
                }

                materials[x, y] = material;
            }
        }

        return materials;
    }

    // Elevation rows are optional: a document without them is a flat (pre-elevation) map.
    private static int[,]? ReadLayers(JsonElement root, int width, int height)
    {
        if (!root.TryGetProperty("layers", out _))
        {
            return null;
        }

        var rows = ReadRowStrings(root, "layers", width, height);
        var layers = new int[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var glyph = rows[y][x];
                if (glyph is < '0' or > '9')
                {
                    throw new MapFormatException($"unknown layer glyph '{glyph}' at ({x},{y})");
                }

                layers[x, y] = glyph - '0';
            }
        }

        return layers;
    }

    // Poses are optional: a document without them is an untouched map. Two shapes decode — the
    // current sparse "transforms" list, and the short-lived quarter-turn "orientations" rows
    // (#199), whose turns become the equivalent yaw so old saves keep their look.
    private static IReadOnlyDictionary<(int X, int Y), PropTransform>? ReadTransforms(
        JsonElement root, int width, int height)
    {
        if (root.TryGetProperty("transforms", out var list))
        {
            var transforms = new Dictionary<(int X, int Y), PropTransform>();
            foreach (var entry in list.EnumerateArray())
            {
                transforms[(entry.GetProperty("x").GetInt32(), entry.GetProperty("y").GetInt32())] =
                    new PropTransform(
                        entry.GetProperty("yaw").GetSingle(),
                        entry.GetProperty("pitch").GetSingle(),
                        entry.GetProperty("roll").GetSingle(),
                        entry.GetProperty("scale").GetSingle());
            }

            return transforms.Count > 0 ? transforms : null;
        }

        if (!root.TryGetProperty("orientations", out _))
        {
            return null;
        }

        var rows = ReadRowStrings(root, "orientations", width, height);
        var legacy = new Dictionary<(int X, int Y), PropTransform>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var glyph = rows[y][x];
                if (glyph is < '0' or > '3')
                {
                    throw new MapFormatException($"unknown orientation glyph '{glyph}' at ({x},{y})");
                }

                if (glyph != '0')
                {
                    // The quarter-turn renderer applied RotateY(-90° × turns) — reproduce as yaw.
                    legacy[(x, y)] = new PropTransform(-90f * (glyph - '0'), 0f, 0f, 1f);
                }
            }
        }

        return legacy.Count > 0 ? legacy : null;
    }

    private static bool[,] ReadRampOverlay(JsonElement root, int width, int height)
    {
        var rows = ReadRowStrings(root, "ramps", width, height);
        var ramps = new bool[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                ramps[x, y] = rows[y][x] == RampOn;
            }
        }

        return ramps;
    }

    private static bool[,] ReadOverlay(JsonElement root, string property, int width, int height)
    {
        var rows = ReadRowStrings(root, property, width, height);
        var overlay = new bool[width, height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                overlay[x, y] = rows[y][x] == OverlayOn;
            }
        }

        return overlay;
    }

    private static string[] ReadRowStrings(JsonElement root, string property, int width, int height)
    {
        var rows = new List<string>();
        foreach (var row in root.GetProperty(property).EnumerateArray())
        {
            rows.Add(row.GetString() ?? throw new MapFormatException($"{property} row is null"));
        }

        if (rows.Count != height)
        {
            throw new MapFormatException($"{property} has {rows.Count} rows, expected {height}");
        }

        foreach (var row in rows)
        {
            if (row.Length != width)
            {
                throw new MapFormatException($"{property} row has width {row.Length}, expected {width}");
            }
        }

        return rows.ToArray();
    }

    private static (int X, int Y) ReadCell(JsonElement cell)
    {
        if (cell.GetArrayLength() != 2)
        {
            throw new MapFormatException("a cell must be a [x, y] pair");
        }

        return (cell[0].GetInt32(), cell[1].GetInt32());
    }

    private static Dictionary<char, CellMaterial> InvertGlyphs()
    {
        var map = new Dictionary<char, CellMaterial>();
        foreach (var (material, glyph) in MaterialGlyph)
        {
            map[glyph] = material;
        }

        return map;
    }
}
