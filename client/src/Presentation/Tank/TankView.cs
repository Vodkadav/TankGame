using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>Renders an <see cref="ITank"/>: a pure mirror that copies the model's position onto the
/// node and picks the directional sprite frames — the chassis facing selects the hull frame, the aim
/// selects the independently-rotating turret frame — plus a health bar above the tank. The hull and
/// turret are pre-rendered iso strips (see <c>scripts/render_iso_tank.py</c>); the model's continuous
/// facings snap to the nearest frame via <see cref="IsoSpriteFacing"/>. The world owns the tick
/// (advancing the tank); this view holds no game rules.</summary>
public partial class TankView : Node2D
{
    private const float BarWidth = 44f;
    private const float BarHeight = 6f;
    private const float BarOffsetY = -58f;
    private const float ShieldOffsetY = BarOffsetY - BarHeight - 2f;

    // The art's ground-contact line sits this far above the frame's bottom (the shared iso anchor),
    // so the tank seats on its cell regardless of frame height.
    private const float GroundContactFromBottom = 33f;

    // Frame 0 of each strip depicts a facing rotated a quarter turn from the model's zero heading
    // (the render camera matches IsoProjection's, with the model's forward along -Y). Added to the
    // model angle before snapping to a frame. Playtest-tunable: a wrong facing is a single change here.
    private const float FacingOffset = Mathf.Pi / 2f;

    private ITank? _tank;
    private Sprite2D _body = null!;
    private Sprite2D _turret = null!;
    private AtlasTexture _hull = null!;
    private AtlasTexture _gun = null!;
    private int _facings;
    private float _frameWidth;
    private float _frameHeight;
    private ColorRect _healthBar = null!;
    private ColorRect _shieldBar = null!;

    public override void _Ready()
    {
        _body = GetNode<Sprite2D>("Body");
        _turret = GetNode<Sprite2D>("Turret");

        _facings = AssetCatalogue.Active.TankFacings;
        var hullSheet = GD.Load<Texture2D>(AssetCatalogue.Active.TankHull);
        var turretSheet = GD.Load<Texture2D>(AssetCatalogue.Active.TankTurret);
        _frameWidth = hullSheet.GetWidth() / (float)_facings;
        _frameHeight = hullSheet.GetHeight();

        _hull = new AtlasTexture { Atlas = hullSheet };
        _gun = new AtlasTexture { Atlas = turretSheet };
        _body.Texture = _hull;
        _turret.Texture = _gun;

        // Both layers were rendered in one canvas, so an identical offset keeps the turret seated on
        // the hull; the offset lifts the art so its ground line lands on the tank's cell.
        var anchor = new Vector2(0f, GroundContactFromBottom - (_frameHeight / 2f));
        _body.Offset = anchor;
        _turret.Offset = anchor;
        SelectFrame(_hull, 0);
        SelectFrame(_gun, 0);

        BuildHealthBar();
    }

    public void Bind(ITank tank) => _tank = tank;

    /// <summary>When true the tank is hidden from view — it is lurking in grass and no enemy is close
    /// enough to spot it (the scene sets this for concealed adversaries, mirroring the AI's blindness
    /// so a tank in cover is genuinely hard to see). Set each frame by the scene.</summary>
    public bool Concealed { get; set; }

    private Color _teamTint = Colors.White;
    private bool _stealthed;

    /// <summary>Tints the whole view to mark which side it is on (white = friendly, reddened =
    /// enemy), so one neutral tank texture reads as either team.</summary>
    public void ApplyTeamTint(bool isEnemy)
    {
        _teamTint = TeamPalette.TintFor(isEnemy);
        ApplyTint();
    }

    /// <summary>When true the tank is hiding in a bush — darken it to signal it is in stealth cover.
    /// Keeps the team tint underneath. Set each frame by the scene for the player's own tank.</summary>
    public bool Stealthed
    {
        get => _stealthed;
        set { _stealthed = value; ApplyTint(); }
    }

    private void ApplyTint() => Modulate = _stealthed ? _teamTint.Darkened(0.55f) : _teamTint;

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

        var screen = IsoProjection.WorldToScreen(_tank.Position);
        Position = new Vector2(screen.X, screen.Y);
        ZIndex = IsoProjection.DepthOf(_tank.Position); // nearer (greater x+y) draws over farther
        SelectFrame(_hull, IsoSpriteFacing.FrameIndex(_tank.Rotation + FacingOffset, _facings));
        SelectFrame(_gun, IsoSpriteFacing.FrameIndex(_tank.TurretRotation + FacingOffset, _facings));
        UpdateHealthBar();
        UpdateShieldBar();
    }

    private void SelectFrame(AtlasTexture atlas, int frame) =>
        atlas.Region = new Rect2(frame * _frameWidth, 0f, _frameWidth, _frameHeight);

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
