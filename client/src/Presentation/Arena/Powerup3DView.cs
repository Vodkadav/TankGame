using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="IPowerup"/> as a real floating 3D emblem that shows what it is — a heart
/// for repair, a shield, a rocket for the missile, a bomb for the airstrike, etc. (ADR-0017; the owner
/// asked for emblems, not plain orbs). The emblem is a small Kenney CC0 model auto-fitted and spinning
/// over a glowing disc tinted to the powerup's colour, so it reads as a pickup. Hidden while a respawning
/// pickup is dormant; the scene frees it on despawn.</summary>
public partial class Powerup3DView : Node3D
{
    private const float HoverHeight = 34f;
    private const float EmblemSpan = 30f;
    private const float SpinSpeed = 1.6f;
    private const float DiscRadius = 18f;

    private IPowerup? _powerup;
    private Node3D _emblem = null!;

    public void Bind(IPowerup powerup) => _powerup = powerup;

    // Built in _Ready (not Bind), because ModelFit needs the model inside the tree to measure it, and the
    // scene binds the view before adding it to the tree.
    public override void _Ready()
    {
        if (_powerup is null)
        {
            return;
        }

        _emblem = new Node3D { Name = "Emblem", Position = new Vector3(0f, HoverHeight, 0f) };
        AddChild(_emblem);
        var model = GD.Load<PackedScene>(EmblemPath(_powerup.Kind)).Instantiate<Node3D>();
        _emblem.AddChild(model);
        ModelFit.Apply(model, EmblemSpan, seatOnGround: false); // centred so it spins in place
        ModelFit.Tint(model, EmblemColour(_powerup.Kind)); // the bare kit .glb are untextured (white)

        AddChild(GlowDisc(PowerupView.ColourFor(_powerup.Kind))); // coloured pad so it reads as a pickup
        AddChild(DebugLabel.Make(_powerup.Kind.ToString(), HoverHeight + 26f));
        UpdateFromModel();
    }

    public override void _Process(double delta)
    {
        if (_emblem is null)
        {
            return;
        }

        _emblem.RotateY((float)delta * SpinSpeed);
        UpdateFromModel();
    }

    public void UpdateFromModel()
    {
        if (_powerup is not null)
        {
            Visible = _powerup.IsAvailable;
            Position = GroundProjection.ToWorld(_powerup.Position); // it moves if it drops
        }
    }

    private static MeshInstance3D GlowDisc(Color colour) => new()
    {
        Name = "Glow",
        Mesh = new CylinderMesh { TopRadius = DiscRadius, BottomRadius = DiscRadius, Height = 1.5f },
        Position = new Vector3(0f, 3f, 0f),
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(colour.R, colour.G, colour.B, 0.55f),
            EmissionEnabled = true,
            Emission = colour,
            EmissionEnergyMultiplier = 0.8f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        },
    };

    // A natural colour per emblem so the bare (untextured) kit models read clearly rather than white.
    private static Color EmblemColour(PowerupKind kind) => kind switch
    {
        PowerupKind.SpeedBoost => new Color(0.30f, 0.80f, 0.95f),   // cyan
        PowerupKind.RapidFire => new Color(1.00f, 0.60f, 0.15f),    // orange
        PowerupKind.BouncingAmmo => new Color(0.70f, 0.35f, 0.95f), // purple
        PowerupKind.SpreadAmmo => new Color(0.35f, 0.72f, 0.32f),   // green
        PowerupKind.PiercingAmmo => new Color(1.00f, 0.85f, 0.20f), // yellow
        PowerupKind.Repair => new Color(0.95f, 0.25f, 0.25f),       // red heart
        PowerupKind.Shield => new Color(0.50f, 0.66f, 0.88f),       // steel blue
        PowerupKind.Missile => new Color(1.00f, 0.40f, 0.20f),      // red-orange
        PowerupKind.Telephone => new Color(0.28f, 0.28f, 0.32f),    // dark bomb
        _ => Colors.White,
    };

    // The Kenney CC0 model that depicts each powerup (its shape reads the meaning).
    private static string EmblemPath(PowerupKind kind) => "res://src/Presentation/Arena/emblems/" + kind switch
    {
        PowerupKind.SpeedBoost => "EmblemSpeed.glb",     // double chevrons
        PowerupKind.RapidFire => "EmblemRapid.glb",      // blaster
        PowerupKind.BouncingAmmo => "EmblemBounce.glb",  // spring
        PowerupKind.SpreadAmmo => "EmblemSpread.glb",    // grenade (scatter)
        PowerupKind.PiercingAmmo => "EmblemPierce.glb",  // arrow
        PowerupKind.Repair => "EmblemRepair.glb",        // heart
        PowerupKind.Shield => "EmblemShield.glb",        // shield
        PowerupKind.Missile => "EmblemMissile.glb",      // rocket
        PowerupKind.Telephone => "EmblemPhone.glb",      // bomb (airstrike)
        _ => "EmblemRepair.glb",
    };
}
