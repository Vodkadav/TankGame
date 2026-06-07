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

    private const float BarWidth = 46f;
    private const float BarHeight = 6f;
    private const float HealthBarY = 84f;
    private const float ShieldBarY = HealthBarY + BarHeight + 2f;

    private ITank? _tank;
    private Node3D _hull = null!;
    private Node3D _turret = null!;
    private StandardMaterial3D _bodyMaterial = null!;
    private int _team;
    private MeshInstance3D _healthBar = null!;
    private MeshInstance3D _shieldBar = null!;

    public override void _Ready()
    {
        var model = GD.Load<PackedScene>("res://src/Presentation/Tank/Tank3D.glb").Instantiate<Node3D>();
        model.Scale = new Vector3(ModelScale, ModelScale, ModelScale);
        model.Position = new Vector3(0f, ModelYOffset, 0f);
        AddChild(model);

        _hull = FindPart(model, "Base") ?? model;
        _turret = FindPart(model, "Turret") ?? _hull;
        BuildBodyMaterial(model);
        if (_bodyMaterial is not null)
        {
            _bodyMaterial.AlbedoColor = TeamPalette.TintFor(_team); // apply any tint set before the tree entry
        }

        BuildBars();
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

        UpdateBars();
    }

    // Billboarded health (and over-shield) bars floating above the tank, always facing the camera.
    private void BuildBars()
    {
        AddChild(Bar("BarBacking", new Color(0.1f, 0.1f, 0.1f, 0.7f), HealthBarY, BarWidth));
        _healthBar = Bar("HealthBar", new Color(0.2f, 0.8f, 0.2f), HealthBarY, BarWidth);
        AddChild(_healthBar);
        _shieldBar = Bar("ShieldBar", new Color(0.3f, 0.9f, 0.95f), ShieldBarY, BarWidth);
        _shieldBar.Visible = false;
        AddChild(_shieldBar);
    }

    private static MeshInstance3D Bar(string name, Color colour, float y, float width) => new()
    {
        Name = name,
        Mesh = new QuadMesh { Size = new Vector2(width, BarHeight) },
        Position = new Vector3(0f, y, 0f),
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = colour,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true,
        },
    };

    private void UpdateBars()
    {
        var hpRatio = _tank!.MaxHp > 0 ? Mathf.Clamp((float)_tank.Hp / _tank.MaxHp, 0f, 1f) : 0f;
        _healthBar.Scale = new Vector3(hpRatio, 1f, 1f);
        ((StandardMaterial3D)_healthBar.MaterialOverride).AlbedoColor = hpRatio > 0.5f ? new Color(0.2f, 0.8f, 0.2f)
            : hpRatio > 0.25f ? new Color(0.9f, 0.8f, 0.1f)
            : new Color(0.9f, 0.2f, 0.2f);

        _shieldBar.Visible = _tank.Shield > 0;
        if (_shieldBar.Visible)
        {
            var sRatio = _tank.MaxHp > 0 ? Mathf.Clamp((float)_tank.Shield / _tank.MaxHp, 0f, 1f) : 1f;
            _shieldBar.Scale = new Vector3(sRatio, 1f, 1f);
        }
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

    // Keep the model's own imported materials (the look the owner approved) and recolour ONLY the body
    // ('Green' in the source) per team. The recolour uses a per-instance duplicate of the imported
    // material, because GLB materials are shared between instances — without the copy, colouring one
    // tank would recolour every tank. The tracks/cannon/lights keep their original materials.
    private void BuildBodyMaterial(Node model)
    {
        foreach (var mi in MeshInstances(model))
        {
            for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                if (mi.Mesh.SurfaceGetMaterial(s) is { ResourceName: "Green" } body)
                {
                    _bodyMaterial ??= body.Duplicate() as StandardMaterial3D ?? new StandardMaterial3D();
                    mi.SetSurfaceOverrideMaterial(s, _bodyMaterial);
                }
            }
        }
    }

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
