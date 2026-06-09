using Godot;

namespace TankGame.Presentation;

/// <summary>A glowing teleport pad ring on the ground (teleport pads T1). It pulses brightly while the pad
/// is ready, dims while it is on cooldown after a warp (recovering toward bright as the cooldown drains),
/// and pops a flash the instant it fires — so a player can read at a glance whether driving onto it will
/// warp them. A pure view: the deterministic <c>Teleporter</c> owns the state and the scene pushes each
/// pad's ready/cooldown into <see cref="SetState"/> every frame. The emission material is held in a field
/// and mutated in place (the Tank3DView pattern) rather than re-fetched each frame, so no transient managed
/// binding to it lingers to the engine's C# shutdown.</summary>
public partial class TeleportPad3DView : Node3D
{
    private static readonly Color PadColour = new(0.30f, 0.85f, 1f); // teleport cyan

    private float _radius = 40f;
    private StandardMaterial3D _material = null!;
    private MeshInstance3D _ring = null!;
    private float _pulse;
    private bool _ready = true;
    private float _cooldownFraction;
    private float _flash;

    /// <summary>Places the ring on the ground and sizes it to the pad's trigger radius. Called before the
    /// node enters the tree; the ring mesh is built in <see cref="_Ready"/>.</summary>
    public void Configure(Vector3 groundPosition, float radius)
    {
        Position = groundPosition;
        _radius = radius;
    }

    public override void _Ready()
    {
        _material = new StandardMaterial3D
        {
            AlbedoColor = new Color(PadColour.R, PadColour.G, PadColour.B, 0.85f),
            EmissionEnabled = true,
            Emission = PadColour,
            EmissionEnergyMultiplier = 1.5f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        _ring = new MeshInstance3D
        {
            Name = "Ring",
            Mesh = new TorusMesh { InnerRadius = _radius * 0.72f, OuterRadius = _radius, Rings = 24, RingSegments = 16 },
            Position = new Vector3(0f, 5f, 0f),
            MaterialOverride = _material,
        };
        AddChild(_ring);
    }

    /// <summary>The scene pushes the pad's live state each frame. A ready→cooling transition is a warp, so
    /// it triggers the flash.</summary>
    public void SetState(bool ready, float cooldownFraction)
    {
        if (_ready && !ready)
        {
            _flash = 1f;
        }

        _ready = ready;
        _cooldownFraction = cooldownFraction;
    }

    public override void _Process(double delta)
    {
        if (_material is null)
        {
            return;
        }

        _pulse += (float)delta;
        _flash = Mathf.Max(0f, _flash - ((float)delta * 2.5f));

        // Ready: a gentle bright pulse. Cooling: dimmed, brightening as the cooldown drains back toward 0.
        var energy = _ready
            ? 1.4f + (Mathf.Sin(_pulse * 3f) * 0.5f)
            : 0.35f + ((1f - _cooldownFraction) * 0.9f);
        _material.EmissionEnergyMultiplier = energy + (_flash * 3.5f);

        _ring.RotateY((float)delta * 0.8f); // slow spin reads as "active"
    }
}
