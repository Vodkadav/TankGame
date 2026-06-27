using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="IPowerup"/> as a bold cartoon icon that bobs over a vivid glowing pad, so
/// each pickup reads at a glance in the cartoon style the owner asked for (a white shield, golden bullets,
/// green speed arrows, a bomb for the airstrike, …). The icon is a flat billboarded <see cref="Sprite3D"/>
/// drawn from a true-alpha PNG (AI-generated — see <c>docs/credits/assets.md</c>); the pad underneath is
/// tinted to the powerup's colour so the colour-coding survives. Hidden while a respawning pickup is
/// dormant; the scene frees it on despawn.</summary>
public partial class Powerup3DView : Node3D
{
    private const float HoverHeight = 26f;
    private const float IconPixelSize = 0.046f; // ~icon source is 1024 px → roughly 45 world units across
    private const float PadRadius = 18f;
    private const float BobSpeed = 2.2f;
    private const float BobHeight = 5f;

    private IPowerup? _powerup;
    private Node3D _icon = null!;
    private float _bobTime;

    public void Bind(IPowerup powerup) => _powerup = powerup;

    // Built in _Ready (not Bind), because the scene binds the view before adding it to the tree.
    public override void _Ready()
    {
        if (_powerup is null)
        {
            return;
        }

        _icon = new Node3D { Name = "Icon", Position = new Vector3(0f, HoverHeight, 0f) };
        AddChild(_icon);
        _icon.AddChild(IconSprite(_powerup.Kind));

        AddChild(GlowPad(PowerupView.ColourFor(_powerup.Kind))); // coloured pad so it reads as a pickup
        UpdateFromModel();
    }

    public override void _Process(double delta)
    {
        if (_icon is null)
        {
            return;
        }

        _bobTime += (float)delta;
        _icon.Position = new Vector3(0f, HoverHeight + (Mathf.Sin(_bobTime * BobSpeed) * BobHeight), 0f);
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

    // A flat cartoon icon that always faces the camera. Unshaded so the bold art keeps its own colours
    // instead of being darkened by arena lighting; RenderPriority lifts it cleanly over the glowing pad.
    private static Sprite3D IconSprite(PowerupKind kind) => new()
    {
        Name = "IconSprite",
        Texture = GD.Load<Texture2D>("res://src/Presentation/Arena/icons/" + IconFile(kind)),
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        Shaded = false,
        PixelSize = IconPixelSize,
        RenderPriority = 1,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
    };

    private static MeshInstance3D GlowPad(Color colour) => new()
    {
        Name = "Glow",
        Mesh = new CylinderMesh { TopRadius = PadRadius, BottomRadius = PadRadius, Height = 1.5f },
        Position = new Vector3(0f, 3f, 0f),
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(colour.R, colour.G, colour.B, 0.7f),
            EmissionEnabled = true,
            Emission = colour,
            EmissionEnergyMultiplier = 2.0f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        },
    };

    // The cartoon sprite that depicts each powerup (its shape + colour read the meaning).
    private static string IconFile(PowerupKind kind) => kind switch
    {
        PowerupKind.SpeedBoost => "speed.png",       // green speed arrows
        PowerupKind.RapidFire => "rapidfire.png",    // muzzle-flash burst
        PowerupKind.BouncingAmmo => "bounce.png",    // ricochet round
        PowerupKind.SpreadAmmo => "spread.png",      // fan of golden bullets
        PowerupKind.PiercingAmmo => "pierce.png",    // armour-piercing round
        PowerupKind.Repair => "repair.png",          // wrench
        PowerupKind.Shield => "shield.png",          // shield
        PowerupKind.Missile => "missile.png",        // rocket
        PowerupKind.Telephone => "telephone.png",    // bomb (airstrike)
        _ => "repair.png",
    };
}
