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
    // The GLB imports nose-along +Z; game-angle 0 should face world +X, so a +90° offset aligns them.
    private const float HeadingOffsetDeg = 90f;

    private const float BarWidth = 46f;
    private const float BarHeight = 6f;
    private const float HealthBarY = 84f;
    private const float ShieldBarY = HealthBarY + BarHeight + 2f;

    private ITank? _tank;
    private Node3D _hull = null!;
    private Node3D _turret = null!;
    private StandardMaterial3D _bodyMaterial = null!;
    private int _team;
    private MeshInstance3D _shieldBar = null!;
    private ShaderMaterial _healthFill = null!;
    private ShaderMaterial _shieldFill = null!;
    private CpuParticles3D _smoke = null!;
    private bool _wasAlive = true;

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
        BuildSmoke();
    }

    public void Bind(ITank tank) => _tank = tank;

    // Dark smoke that trails a badly-wounded tank (below Tank.LowHealthFraction of max HP). Off until the
    // tank drops under the threshold; a clear "this tank is nearly dead" tell to match the crawl speed.
    private void BuildSmoke()
    {
        var mat = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
        };
        var puff = new SphereMesh { Radius = 7f, Height = 14f, RadialSegments = 6, Rings = 3, Material = mat };
        var ramp = new Gradient
        {
            Offsets = new[] { 0f, 1f },
            Colors = new[] { new Color(0.18f, 0.18f, 0.18f, 0.8f), new Color(0.06f, 0.06f, 0.06f, 0f) },
        };
        _smoke = new CpuParticles3D
        {
            Name = "DamageSmoke",
            Emitting = false,
            Amount = 14,
            Lifetime = 0.9,
            Position = new Vector3(0f, 18f, 0f),
            Mesh = puff,
            Direction = new Vector3(0f, 1f, 0f),
            Spread = 28f,
            Gravity = new Vector3(0f, 22f, 0f),
            InitialVelocityMin = 16f,
            InitialVelocityMax = 40f,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.5f,
            ColorRamp = ramp,
        };
        AddChild(_smoke);
    }

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

        var alive = _tank.Hp > 0;
        if (_wasAlive && !alive)
        {
            SpawnDeathExplosion(); // a fireball where it died, so the kill reads
        }

        _wasAlive = alive;
        Visible = alive; // downed/awaiting respawn → hidden
        if (!alive)
        {
            _smoke.Emitting = false;
            return;
        }

        _smoke.Emitting = _tank.Hp <= GameLogic.Tank.LowHealthFraction * _tank.MaxHp; // wounded → trails smoke

        Position = GroundProjection.ToWorld(_tank.Position);

        var offset = Mathf.DegToRad(HeadingOffsetDeg);
        var hullYaw = offset - _tank.Rotation;          // subtract: the model turn matches the mouse (see #141)
        var turretYaw = offset - _tank.TurretRotation;
        _hull.Rotation = new Vector3(0f, hullYaw, 0f);
        _turret.Rotation = new Vector3(0f, turretYaw - hullYaw, 0f); // child of the hull → world yaw = turretYaw

        UpdateBars();
    }

    // A billboarded progress-bar: a border, a dark background, and a foreground that fills from the LEFT
    // to the percent of life left (a shader, so it is properly left-anchored, not centre-scaled). The
    // vertex billboard keeps it camera-facing; depth test off draws it over everything.
    private const string BarShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_test_disabled;
uniform float ratio : hint_range(0.0, 1.0) = 1.0;
uniform vec3 fg : source_color = vec3(0.45, 0.95, 0.5);
uniform vec3 bg : source_color = vec3(0.16, 0.16, 0.19);
uniform vec3 border : source_color = vec3(0.02, 0.02, 0.03);
uniform float aspect = 7.6;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    float by = 0.16;
    float bx = by / aspect;
    bool inB = UV.x < bx || UV.x > 1.0 - bx || UV.y < by || UV.y > 1.0 - by;
    vec3 c = border;
    if (!inB) {
        float fx = (UV.x - bx) / (1.0 - 2.0 * bx);
        c = fx < ratio ? fg : bg;
    }
    ALBEDO = c;
}";

    private static Shader _barShader = null!;

    private void BuildBars()
    {
        _barShader ??= new Shader { Code = BarShaderCode };
        _healthFill = BarMaterial(new Vector3(0.45f, 0.95f, 0.5f));
        AddChild(BarQuad(_healthFill, HealthBarY));
        _shieldFill = BarMaterial(new Vector3(0.40f, 0.85f, 0.98f));
        _shieldBar = BarQuad(_shieldFill, ShieldBarY);
        _shieldBar.Visible = false;
        AddChild(_shieldBar);
    }

    private static ShaderMaterial BarMaterial(Vector3 fg)
    {
        var mat = new ShaderMaterial { Shader = _barShader };
        mat.SetShaderParameter("fg", fg);
        mat.SetShaderParameter("aspect", BarWidth / BarHeight);
        mat.SetShaderParameter("ratio", 1f);
        return mat;
    }

    private static MeshInstance3D BarQuad(ShaderMaterial mat, float y) => new()
    {
        Mesh = new QuadMesh { Size = new Vector2(BarWidth, BarHeight) },
        Position = new Vector3(0f, y, 0f),
        MaterialOverride = mat,
    };

    private void UpdateBars()
    {
        var hp = _tank!.MaxHp > 0 ? Mathf.Clamp((float)_tank.Hp / _tank.MaxHp, 0f, 1f) : 0f;
        _healthFill.SetShaderParameter("ratio", hp);
        _healthFill.SetShaderParameter("fg", hp > 0.5f ? new Vector3(0.45f, 0.95f, 0.5f)
            : hp > 0.25f ? new Vector3(0.98f, 0.85f, 0.35f)
            : new Vector3(0.98f, 0.42f, 0.42f));

        _shieldBar.Visible = _tank.Shield > 0;
        if (_shieldBar.Visible)
        {
            var s = _tank.MaxHp > 0 ? Mathf.Clamp((float)_tank.Shield / _tank.MaxHp, 0f, 1f) : 1f;
            _shieldFill.SetShaderParameter("ratio", s);
        }
    }

    private void SpawnDeathExplosion()
    {
        var boom = new Explosion3D();
        boom.Init(48f);
        boom.Position = Position; // the tank's last world position
        GetParent()?.AddChild(boom);
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
