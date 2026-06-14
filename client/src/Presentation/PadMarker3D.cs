using Godot;

namespace TankGame.Presentation;

/// <summary>The editor's teleport-pad gizmo (owner feedback 2026-06-11): the spawn-marker recipe in
/// teleport cyan — a disc with two white rings and the link's number ("?" while a pad still awaits
/// its partner) — every part depth-test-off, so pads float visibly above the ground tileset and
/// terrain from any camera angle. Editor-only; play renders the glowing pad rings instead.</summary>
public partial class PadMarker3D : Node3D
{
    private static readonly Color PadColour = new(0.16f, 0.62f, 0.78f); // teleport cyan, ink-dark enough for white rings

    private const float DiscRadius = 24f;
    private const float HoverY = 6f;

    private static StandardMaterial3D? _cyanMat;
    private static StandardMaterial3D? _whiteMat;

    private string _label = "?";

    /// <summary>The link number both ends of a pair wear (or "?" for the pending half). Call before
    /// adding to the tree.</summary>
    public void Configure(string label) => _label = label;

    public override void _Ready()
    {
        _cyanMat ??= MarkerMaterial(PadColour);
        _whiteMat ??= MarkerMaterial(new Color(0.96f, 0.96f, 0.94f));

        AddChild(Disc("Disc", new CylinderMesh
        {
            TopRadius = DiscRadius,
            BottomRadius = DiscRadius,
            Height = 1.4f,
            RadialSegments = 24,
        }, _cyanMat));
        AddChild(Disc("OuterRing", Ring(DiscRadius * 0.72f, 2.4f), _whiteMat));
        AddChild(Disc("InnerRing", Ring(DiscRadius * 0.38f, 2.0f), _whiteMat));

        AddChild(new Label3D
        {
            Name = "Number",
            Text = _label,
            PixelSize = 0.30f, // Label3D's default renders sub-unit text — invisible at 64-unit cells
            FontSize = 96,
            Modulate = new Color(1f, 1f, 1f),
            OutlineModulate = new Color(0.03f, 0.18f, 0.24f),
            OutlineSize = 14,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Position = new Vector3(0f, HoverY + 28f, 0f),
        });
    }

    private static StandardMaterial3D MarkerMaterial(Color colour) => new()
    {
        AlbedoColor = colour,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true,
    };

    private static TorusMesh Ring(float radius, float thickness) => new()
    {
        InnerRadius = radius - thickness,
        OuterRadius = radius,
        Rings = 24,
        RingSegments = 10,
    };

    private static MeshInstance3D Disc(string name, Mesh mesh, StandardMaterial3D material) => new()
    {
        Name = name,
        Mesh = mesh,
        MaterialOverride = material,
        Position = new Vector3(0f, HoverY, 0f),
    };
}
