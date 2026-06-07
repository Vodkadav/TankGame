using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="ITank"/> as a live 3D model composited into the 2D isometric world.
/// A per-tank <see cref="SubViewport"/> holds the cartoon tank scene (<c>Tank3D.glb</c>) under a fixed
/// orthographic iso camera; its render texture is drawn by a <see cref="Sprite2D"/> at the tank's iso
/// screen position and depth-sorted with <c>ZIndex</c>, so it sits in the world exactly like the old
/// sprite did — but the hull and the independently-aiming turret are real 3D nodes rotated each frame,
/// so there is no directional-frame snapping. Per-part materials let the body take the team colour while
/// the tracks, wheels and cannon stay black and the lights stay yellow; a grow/cull-front next-pass adds
/// the bold cartoon outline. The view holds no game rules — it mirrors the model the world ticks.</summary>
public partial class TankView : Node2D
{
    // 3D render target + framing. Eyeball-gated on the owner's playtest.
    private const int ViewportSize = 256;     // px square the tank's 3D scene renders into
    private const float CameraElevDeg = 30f;  // iso camera pitch — matches the tile art angle
    private const float CameraYawDeg = 45f;    // iso camera yaw
    private const float CameraSize = 3.0f;     // orthographic frustum height (metres) — sets the tank's on-screen size
    private const float SpriteScale = 0.62f;   // scale the 256px render down to roughly the old tank footprint
    // Forward calibration: the model's nose maps to game-angle 0 at this offset; the rendered turn winds
    // opposite the game angle (as the iso sprites did), so the model angle is subtracted. One-line tweak.
    private const float HeadingOffsetDeg = 180f;

    private const float BarWidth = 44f;
    private const float BarHeight = 6f;
    private const float BarOffsetY = -58f;
    private const float ShieldOffsetY = BarOffsetY - BarHeight - 2f;

    private ITank? _tank;
    private SubViewport _viewport = null!;
    private Sprite2D _sprite = null!;
    private Node3D _hull = null!;
    private Node3D _turret = null!;
    private StandardMaterial3D _bodyMaterial = null!;
    private ColorRect _healthBar = null!;
    private ColorRect _shieldBar = null!;
    private int _team; // remembered so a tint set before _Ready (BuildTankView tints pre-AddChild) still lands

    public override void _Ready()
    {
        _viewport = new SubViewport
        {
            Size = new Vector2I(ViewportSize, ViewportSize),
            TransparentBg = true,
            OwnWorld3D = true, // each tank gets its own 3D world so models do not pile up in one scene
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        AddChild(_viewport);

        var model = GD.Load<PackedScene>("res://src/Presentation/Tank/Tank3D.glb").Instantiate<Node3D>();
        _viewport.AddChild(model);
        _hull = FindPart(model, "Base") ?? model;
        _turret = FindPart(model, "Turret") ?? _hull;

        BuildMaterials(model);
        BuildCameraAndLight();

        _sprite = new Sprite2D { Texture = _viewport.GetTexture(), Scale = new Vector2(SpriteScale, SpriteScale) };
        _sprite.Modulate = _stealthed ? new Color(0.45f, 0.45f, 0.45f) : Colors.White;
        AddChild(_sprite);

        _bodyMaterial.AlbedoColor = TeamPalette.TintFor(_team); // apply any tint set before the tree entry

        BuildHealthBar();
    }

    public void Bind(ITank tank) => _tank = tank;

    /// <summary>When true the tank is hidden from view — lurking in grass with no enemy near (the scene
    /// mirrors the AI's blindness so cover genuinely hides it). Set each frame by the scene.</summary>
    public bool Concealed { get; set; }

    private bool _stealthed;

    /// <summary>Paints the body to its team colour (green/red/blue/yellow for teams 0-3) by recolouring
    /// the one shared body material; the black tracks/cannon and yellow lights are untouched. Safe to call
    /// before the node enters the tree — the colour is remembered and applied when the material is built.</summary>
    public void ApplyTeamTint(int team)
    {
        _team = team;
        if (_bodyMaterial is not null)
        {
            _bodyMaterial.AlbedoColor = TeamPalette.TintFor(team);
        }
    }

    /// <summary>When true the tank is hiding in a bush — darken the composited sprite to signal stealth
    /// cover (leaves the team colour underneath). Set each frame by the scene for the player's tank.</summary>
    public bool Stealthed
    {
        get => _stealthed;
        set
        {
            _stealthed = value;
            if (_sprite is not null)
            {
                _sprite.Modulate = value ? new Color(0.45f, 0.45f, 0.45f) : Colors.White;
            }
        }
    }

    public override void _Process(double delta) => UpdateFromModel();

    /// <summary>Mirrors the bound tank's state onto the node, the 3D model and the health bar. Public so
    /// tests can assert the mirror without relying on frame timing.</summary>
    public void UpdateFromModel()
    {
        if (_tank is null)
        {
            return;
        }

        // A downed tank (0 hp, awaiting respawn) or a concealed one (unseen in grass) is hidden.
        Visible = _tank.Hp > 0 && !Concealed;
        if (!Visible)
        {
            return;
        }

        var screen = IsoProjection.WorldToScreen(_tank.Position);
        Position = new Vector2(screen.X, screen.Y);
        ZIndex = IsoProjection.DepthOf(_tank.Position); // nearer (greater x+y) draws over farther

        var offset = Mathf.DegToRad(HeadingOffsetDeg);
        var hullYaw = offset - _tank.Rotation;       // rendered turn winds opposite the game angle
        var turretYaw = offset - _tank.TurretRotation;
        _hull.Rotation = new Vector3(0f, hullYaw, 0f);
        _turret.Rotation = new Vector3(0f, turretYaw - hullYaw, 0f); // child of the hull → world yaw = turretYaw

        UpdateHealthBar();
        UpdateShieldBar();
    }

    // Find the first descendant whose name contains the part name (GLB import may suffix names).
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

    // Override every surface with a flat cartoon material by its source colour, each carrying the shared
    // black outline next-pass. The body ('Green' in the source) shares one recolourable material.
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
            GrowAmount = 0.03f,
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

    private void BuildCameraAndLight()
    {
        var elev = Mathf.DegToRad(CameraElevDeg);
        var yaw = Mathf.DegToRad(CameraYawDeg);
        var target = new Vector3(0f, 0.35f, 0f);
        var dir = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(elev), Mathf.Sin(elev), Mathf.Cos(yaw) * Mathf.Cos(elev));
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = CameraSize,
            Position = target + (dir * 10f),
        };
        _viewport.AddChild(camera);
        camera.LookAt(target, Vector3.Up);

        var sun = new DirectionalLight3D { LightEnergy = 1.6f };
        sun.RotationDegrees = new Vector3(-55f, 35f, 0f);
        _viewport.AddChild(sun);

        var env = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0f, 0f, 0f, 0f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.85f, 0.85f, 0.9f),
                AmbientLightEnergy = 1.3f,
            },
        };
        _viewport.AddChild(env);
    }

    private void UpdateHealthBar()
    {
        var ratio = _tank!.MaxHp > 0 ? Mathf.Clamp((float)_tank.Hp / _tank.MaxHp, 0f, 1f) : 0f;
        _healthBar.Size = new Vector2(BarWidth * ratio, BarHeight);
        _healthBar.Color = ratio > 0.5f ? new Color(0.2f, 0.8f, 0.2f)
            : ratio > 0.25f ? new Color(0.9f, 0.8f, 0.1f)
            : new Color(0.9f, 0.2f, 0.2f);
    }

    private void UpdateShieldBar()
    {
        _shieldBar.Visible = _tank!.Shield > 0;
        if (!_shieldBar.Visible)
        {
            return;
        }

        var ratio = _tank.MaxHp > 0 ? Mathf.Clamp((float)_tank.Shield / _tank.MaxHp, 0f, 1f) : 1f;
        _shieldBar.Size = new Vector2(BarWidth * ratio, BarHeight);
    }

    private void BuildHealthBar()
    {
        var backing = new ColorRect
        {
            Name = "HealthBarBacking",
            Color = new Color(0.1f, 0.1f, 0.1f, 0.6f),
            Size = new Vector2(BarWidth, BarHeight),
            Position = new Vector2(-BarWidth / 2f, BarOffsetY),
        };
        AddChild(backing);

        _healthBar = new ColorRect
        {
            Name = "HealthBar",
            Size = new Vector2(BarWidth, BarHeight),
            Position = new Vector2(-BarWidth / 2f, BarOffsetY),
        };
        AddChild(_healthBar);

        _shieldBar = new ColorRect
        {
            Name = "ShieldBar",
            Color = new Color(0.3f, 0.9f, 0.95f),
            Size = new Vector2(BarWidth, BarHeight),
            Position = new Vector2(-BarWidth / 2f, ShieldOffsetY),
            Visible = false,
        };
        AddChild(_shieldBar);
    }
}
