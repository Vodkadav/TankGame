using Godot;

namespace TankGame.Presentation;

/// <summary>The editor's dotted line between a teleport pad pair (owner feedback 2026-06-11): a row
/// of small flat discs from one end to the other, gently fading in and out so the pairing reads as a
/// live connection without shouting over the map. All dots share ONE material held in a field and
/// mutated in place (the GC-safe pattern from the pad rings, see #181) — never re-fetched per frame.</summary>
public partial class TeleportLinkLine : Node3D
{
    private static readonly Color LinkColour = new(0.3f, 0.85f, 1f); // teleport cyan, matches the pads

    private const float DotSpacing = 28f;
    private const float DotRadius = 4.5f;
    private const float HoverY = 30f; // floats above the floor meshes, under the pad glyphs

    private Vector3 _from;
    private Vector3 _to;
    private StandardMaterial3D _material = null!;
    private float _pulse;

    /// <summary>How many dots the line laid out — exposed for tests.</summary>
    public int DotCount { get; private set; }

    /// <summary>Sets the two pad ends (world space, ground level). Call before adding to the tree.</summary>
    public void Configure(Vector3 from, Vector3 to)
    {
        _from = from;
        _to = to;
    }

    public override void _Ready()
    {
        _material = new StandardMaterial3D
        {
            AlbedoColor = new Color(LinkColour.R, LinkColour.G, LinkColour.B, 0.6f),
            EmissionEnabled = true,
            Emission = LinkColour,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        var mesh = new CylinderMesh
        {
            TopRadius = DotRadius,
            BottomRadius = DotRadius,
            Height = 0.5f,
            RadialSegments = 10,
        };

        var span = _to - _from;
        var dots = Mathf.Max(2, (int)(span.Length() / DotSpacing));
        for (var i = 1; i < dots; i++) // skip the endpoints — the pad markers live there
        {
            AddChild(new MeshInstance3D
            {
                Mesh = mesh,
                MaterialOverride = _material,
                Position = _from + (span * ((float)i / dots)) + new Vector3(0f, HoverY, 0f),
            });
            DotCount++;
        }
    }

    public override void _Process(double delta)
    {
        _pulse += (float)delta;
        var alpha = 0.2f + (0.55f * (0.5f + (0.5f * Mathf.Sin(_pulse * 2.5f)))); // fade in and out
        _material.AlbedoColor = new Color(LinkColour.R, LinkColour.G, LinkColour.B, alpha);
    }
}
