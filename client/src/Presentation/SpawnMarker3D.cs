using Godot;

namespace TankGame.Presentation;

/// <summary>The editor's spawn-point gizmo (owner follow-up 2026-06-11): a big red disc with two
/// white rings and the marker number — unmissable while authoring, never shown in play. Every part
/// renders with depth testing off so a marker stays visible over walls and plateaus from any camera
/// angle. Materials are shared statics (one of each for every marker, the Tank3DView shader
/// pattern) and the meshes are plain primitives, so a refresh churns no per-marker resources.</summary>
public partial class SpawnMarker3D : Node3D
{
    private const float DiscRadius = 26f;
    private const float HoverY = 6f; // floats just over the floor so it never z-fights the ground

    private static StandardMaterial3D? _redMat;
    private static StandardMaterial3D? _whiteMat;

    private int _number = 1;

    /// <summary>The marker's number (1 = the format's player slot). Call before adding to the tree.</summary>
    public void Configure(int number) => _number = number;

    public override void _Ready()
    {
        _redMat ??= MarkerMaterial(new Color(0.9f, 0.12f, 0.12f));
        _whiteMat ??= MarkerMaterial(new Color(0.96f, 0.96f, 0.94f));

        AddChild(Disc("Disc", new CylinderMesh
        {
            TopRadius = DiscRadius,
            BottomRadius = DiscRadius,
            Height = 1.4f,
            RadialSegments = 24,
        }, _redMat));
        AddChild(Disc("OuterRing", Ring(DiscRadius * 0.72f, 2.6f), _whiteMat));
        AddChild(Disc("InnerRing", Ring(DiscRadius * 0.38f, 2.2f), _whiteMat));

        AddChild(new Label3D
        {
            Name = "Number",
            Text = _number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            PixelSize = 0.28f, // Label3D's default renders sub-unit text — invisible at 64-unit cells
            FontSize = 110,
            Modulate = new Color(1f, 1f, 1f),
            OutlineModulate = new Color(0.45f, 0.04f, 0.04f),
            OutlineSize = 14,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Position = new Vector3(0f, HoverY + 30f, 0f),
        });
    }

    private static StandardMaterial3D MarkerMaterial(Color colour) => new()
    {
        AlbedoColor = colour,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = true, // the marker must read over walls and plateaus from any angle
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
