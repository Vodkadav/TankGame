using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="ITank"/>: a pure mirror that copies the model's position
/// onto the node, the chassis facing onto the Body sprite, the aim onto the Turret sprite,
/// and the hit points onto a health bar above the tank. The world owns the tick (advancing
/// the tank); this view holds no game rules.</summary>
public partial class TankView : Node2D
{
    private const float BarWidth = 44f;
    private const float BarHeight = 6f;
    private const float BarOffsetY = -46f;
    private const float ShieldOffsetY = BarOffsetY - BarHeight - 2f;

    private ITank? _tank;
    private Sprite2D _body = null!;
    private Sprite2D _turret = null!;
    private ColorRect _healthBar = null!;
    private ColorRect _shieldBar = null!;

    public override void _Ready()
    {
        _body = GetNode<Sprite2D>("Body");
        _turret = GetNode<Sprite2D>("Turret");
        _body.Texture = GD.Load<Texture2D>(AssetCatalogue.Active.TankBody);
        _turret.Texture = GD.Load<Texture2D>(AssetCatalogue.Active.TankTurret);
        BuildHealthBar();
    }

    public void Bind(ITank tank) => _tank = tank;

    /// <summary>When true the tank is hidden from view — it is lurking in grass and no enemy is close
    /// enough to spot it (the scene sets this for concealed adversaries, mirroring the AI's blindness
    /// so a tank in cover is genuinely hard to see). Set each frame by the scene.</summary>
    public bool Concealed { get; set; }

    /// <summary>Tints the whole view to mark which side it is on (white = friendly, reddened =
    /// enemy), so one neutral tank texture reads as either team.</summary>
    public void ApplyTeamTint(bool isEnemy) => Modulate = TeamPalette.TintFor(isEnemy);

    public override void _Process(double delta) => UpdateFromModel();

    /// <summary>Mirrors the bound tank's state onto the node, sprites, and health bar. Public
    /// so tests can assert the mirror without relying on frame timing.</summary>
    public void UpdateFromModel()
    {
        if (_tank is null)
        {
            return;
        }

        // A downed tank (0 hp) is awaiting respawn — hide it until it revives. A concealed tank
        // (lurking in grass, unseen) is hidden too, so cover actually hides it from view.
        Visible = _tank.Hp > 0 && !Concealed;
        if (!Visible)
        {
            return;
        }

        Position = new Vector2(_tank.Position.X, _tank.Position.Y);
        _body.Rotation = _tank.Rotation;
        _turret.Rotation = _tank.TurretRotation;
        UpdateHealthBar();
        UpdateShieldBar();
    }

    private void UpdateHealthBar()
    {
        var ratio = _tank!.MaxHp > 0 ? Mathf.Clamp((float)_tank.Hp / _tank.MaxHp, 0f, 1f) : 0f;
        _healthBar.Size = new Vector2(BarWidth * ratio, BarHeight);
        _healthBar.Color = ratio > 0.5f ? new Color(0.2f, 0.8f, 0.2f)
            : ratio > 0.25f ? new Color(0.9f, 0.8f, 0.1f)
            : new Color(0.9f, 0.2f, 0.2f);
    }

    // Over-shield draws as a thin cyan bar just above the health bar, scaled against MaxHp (so a
    // full-Hp-worth of shield reads as a full-width bar) and hidden entirely when unshielded.
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
        // The bar lives on the view root, which never rotates (only the sprites do), so it
        // stays axis-aligned above the tank.
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
