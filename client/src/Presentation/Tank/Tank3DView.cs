using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="ITank"/> as a real 3D model in the 3D world (ADR-0017): the
/// <c>Tank3D.glb</c> placed at the tank's ground position, its hull and independently-aiming turret
/// nodes rotated each frame. No SubViewport, no depth-sort bookkeeping — the 3D camera and depth buffer
/// handle projection and ordering. Per-part materials give the team-colour body, yellow lights and black
/// tracks/cannon; a grow/cull-front next-pass draws the cartoon outline. A pure mirror of the model.</summary>
public partial class Tank3DView : Node3D
{
    // Eyeball-gated on playtest. The GLB is ~1.7 units; the game field uses 64-unit cells, so the model
    // is scaled up to roughly fill a cell. HeadingOffset calibrates the model's nose to game-angle 0.
    private const float ModelScale = 32f;
    private const float ModelYOffset = 0f;
    private const float HeadingOffsetDeg = 0f;

    private ITank? _tank;
    private Node3D _hull = null!;
    private Node3D _turret = null!;
    private StandardMaterial3D _bodyMaterial = null!;
    private int _team;

    public override void _Ready()
    {
        var model = GD.Load<PackedScene>("res://src/Presentation/Tank/Tank3D.glb").Instantiate<Node3D>();
        model.Scale = new Vector3(ModelScale, ModelScale, ModelScale);
        model.Position = new Vector3(0f, ModelYOffset, 0f);
        AddChild(model);

        _hull = FindPart(model, "Base") ?? model;
        _turret = FindPart(model, "Turret") ?? _hull;
        BuildMaterials(model);
        _bodyMaterial.AlbedoColor = TeamPalette.TintFor(_team); // apply any tint set before the tree entry
    }

    public void Bind(ITank tank) => _tank = tank;

    /// <summary>Paints the body to its team colour; safe to call before the node enters the tree.</summary>
    public void ApplyTeamTint(int team)
    {
        _team = team;
        if (_bodyMaterial is not null)
        {
            _bodyMaterial.AlbedoColor = TeamPalette.TintFor(team);
        }
    }

    public override void _Process(double delta) => UpdateFromModel();

    /// <summary>Mirrors the tank's position and facings onto the 3D node. Public so tests can assert it.</summary>
    public void UpdateFromModel()
    {
        if (_tank is null)
        {
            return;
        }

        Visible = _tank.Hp > 0; // downed/awaiting respawn → hidden
        if (!Visible)
        {
            return;
        }

        Position = GroundProjection.ToWorld(_tank.Position);

        var offset = Mathf.DegToRad(HeadingOffsetDeg);
        var hullYaw = offset - _tank.Rotation;          // subtract: the model turn matches the mouse (see #141)
        var turretYaw = offset - _tank.TurretRotation;
        _hull.Rotation = new Vector3(0f, hullYaw, 0f);
        _turret.Rotation = new Vector3(0f, turretYaw - hullYaw, 0f); // child of the hull → world yaw = turretYaw
    }

    private static Node3D? FindPart(Node node, string contains)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n3 && child.Name.ToString().Contains(contains))
            {
                return n3;
            }

            if (FindPart(child, contains) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // Override every surface by its source colour, each carrying the shared black outline next-pass; the
    // body ('Green' in the source) shares one recolourable material set per team.
    private void BuildMaterials(Node model)
    {
        var black = Cartoon(new Color(0.05f, 0.05f, 0.07f));
        var lights = Cartoon(new Color(0.98f, 0.82f, 0.20f));
        var white = Cartoon(new Color(0.85f, 0.85f, 0.88f));
        _bodyMaterial = Cartoon(TeamPalette.TintFor(0));
        var outline = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.06f, 0.05f, 0.08f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Front,
            Grow = true,
            GrowAmount = 0.6f, // in model-local units; the model is scaled up ~32x
        };
        foreach (var mat in new[] { black, lights, white, _bodyMaterial })
        {
            mat.NextPass = outline;
        }

        foreach (var mi in MeshInstances(model))
        {
            for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                var srcName = mi.Mesh.SurfaceGetMaterial(s)?.ResourceName ?? string.Empty;
                StandardMaterial3D chosen = srcName switch
                {
                    "Green" => _bodyMaterial,
                    "Lights" => lights,
                    "White" => white,
                    _ => black,
                };
                mi.SetSurfaceOverrideMaterial(s, chosen);
            }
        }
    }

    private static StandardMaterial3D Cartoon(Color albedo) => new()
    {
        AlbedoColor = albedo,
        Roughness = 1f,
        Metallic = 0f,
        SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
    };

    private static System.Collections.Generic.IEnumerable<MeshInstance3D> MeshInstances(Node node)
    {
        if (node is MeshInstance3D mi && mi.Mesh is not null)
        {
            yield return mi;
        }

        foreach (var child in node.GetChildren())
        {
            foreach (var found in MeshInstances(child))
            {
                yield return found;
            }
        }
    }
}
