using Godot;
using Chickensoft.GoDotTest;
using NVector2 = System.Numerics.Vector2;
using TankGame.Domain;
using TankGame.GameLogic;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class TankViewTests : TestClass
{
    private sealed class FixedInput(TankInput value) : IInputSource
    {
        public TankInput Read() => value;
    }

    private TankView _view = default!;

    public TankViewTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        // Loading the scene also imports/loads the placeholder textures (M1-T2).
        _view = GD.Load<PackedScene>("res://src/Presentation/Tank/TankView.tscn")
            .Instantiate<TankView>();
        TestScene.AddChild(_view);
    }

    [Cleanup]
    public void Cleanup() => _view.QueueFree();

    [Test]
    public void Scene_LoadsHullAndTurretStrips_FromTheCatalogue()
    {
        var body = _view.GetNode<Sprite2D>("Body");
        var turret = _view.GetNode<Sprite2D>("Turret");
        var hull = body.Texture as AtlasTexture;
        var gun = turret.Texture as AtlasTexture;

        if (hull is null || gun is null
            || hull.Atlas != GD.Load<Texture2D>(AssetCatalogue.Active.TankHull)
            || gun.Atlas != GD.Load<Texture2D>(AssetCatalogue.Active.TankTurret))
        {
            throw new System.Exception("Body and Turret sprites must draw from the catalogue's directional strips.");
        }
    }

    [Test]
    public void UpdateFromModel_MirrorsPosition_AndAimsTheTurretIndependentlyOfTheHull()
    {
        var input = new FixedInput(new TankInput(new NVector2(1f, 0f), Aim: 0.75f, Fire: false));
        var arena = new RectArena(new NVector2(-500f, -500f), new NVector2(500f, 500f));
        var tank = new Tank(input, new World(), arena, NVector2.Zero, speed: 100f, fireInterval: 0.3f, projectileSpeed: 600f);
        _view.Bind(tank);

        tank.Step(0.1f); // the model advances 10 units along +X (heading 0); the turret aims at 0.75 rad
        _view.UpdateFromModel();

        // World (10,0) projects to iso ((10-0)*1, (10+0)*0.5) = (10, 5).
        if (Mathf.Abs(_view.Position.X - 10f) > 0.01f || Mathf.Abs(_view.Position.Y - 5f) > 0.01f)
        {
            throw new System.Exception($"View should mirror the tank to iso (10,5); was {_view.Position}.");
        }

        // The facing drives the directional frame, not a sprite rotation — the sprites never spin.
        var turret = _view.GetNode<Sprite2D>("Turret");
        var body = _view.GetNode<Sprite2D>("Body");
        if (turret.Rotation != 0f || body.Rotation != 0f)
        {
            throw new System.Exception("Iso facing is by frame selection, so the sprites must not rotate.");
        }

        var facings = AssetCatalogue.Active.TankFacings;
        var hull = (AtlasTexture)body.Texture;
        var gun = (AtlasTexture)turret.Texture;
        var frameWidth = hull.Atlas.GetWidth() / (float)facings;
        var hullFrame = Mathf.RoundToInt(hull.Region.Position.X / frameWidth);
        var gunFrame = Mathf.RoundToInt(gun.Region.Position.X / frameWidth);

        // Aiming 0.75 rad off the chassis is ~2 of the 16 directional steps, so the turret frame leads
        // the hull frame by 2 — proving the turret picks its own frame from the aim.
        if (gunFrame != ((hullFrame + 2) % facings))
        {
            throw new System.Exception($"Turret frame should lead the hull by 2 (aim 0.75 rad); hull={hullFrame}, gun={gunFrame}.");
        }
    }

    [Test]
    public void UpdateFromModel_ShrinksTheHealthBar_AsTheTankTakesDamage()
    {
        var input = new FixedInput(new TankInput(NVector2.Zero, Aim: 0f, Fire: false));
        var arena = new RectArena(new NVector2(-500f, -500f), new NVector2(500f, 500f));
        var tank = new Tank(input, new World(), arena, NVector2.Zero, speed: 100f, fireInterval: 0.3f,
            projectileSpeed: 600f, maxHp: 4);
        _view.Bind(tank);

        _view.UpdateFromModel();
        var bar = _view.GetNode<ColorRect>("HealthBar");
        var fullWidth = bar.Size.X;

        tank.TakeDamage(2); // half health
        _view.UpdateFromModel();

        if (Mathf.Abs(bar.Size.X - (fullWidth / 2f)) > 0.5f)
        {
            throw new System.Exception($"Health bar should halve to {fullWidth / 2f}; was {bar.Size.X}.");
        }
    }

    [Test]
    public void ShieldBar_IsHiddenWhenUnshielded_AndShowsWhenShielded()
    {
        var input = new FixedInput(new TankInput(NVector2.Zero, Aim: 0f, Fire: false));
        var arena = new RectArena(new NVector2(-500f, -500f), new NVector2(500f, 500f));
        var tank = new Tank(input, new World(), arena, NVector2.Zero, speed: 100f, fireInterval: 0.3f,
            projectileSpeed: 600f, maxHp: 3);
        _view.Bind(tank);

        _view.UpdateFromModel();
        var shieldBar = _view.GetNode<ColorRect>("ShieldBar");
        if (shieldBar.Visible)
        {
            throw new System.Exception("Shield bar should be hidden when the tank has no shield.");
        }

        tank.AddShield(3);
        _view.UpdateFromModel();
        if (!shieldBar.Visible || shieldBar.Size.X <= 0f)
        {
            throw new System.Exception($"Shield bar should show with a positive width; visible={shieldBar.Visible}, w={shieldBar.Size.X}.");
        }
    }

    [Test]
    public void ApplyTeamTint_TintsEnemies_AndLeavesAlliesUntinted()
    {
        _view.ApplyTeamTint(isEnemy: true);
        if (_view.Modulate != TeamPalette.TintFor(isEnemy: true))
        {
            throw new System.Exception($"An enemy view should carry the enemy tint; was {_view.Modulate}.");
        }

        _view.ApplyTeamTint(isEnemy: false);
        if (_view.Modulate != Colors.White)
        {
            throw new System.Exception($"A friendly view should be untinted; was {_view.Modulate}.");
        }
    }

    [Test]
    public void Stealthed_DarkensTheTank_OverItsTeamTint()
    {
        _view.ApplyTeamTint(isEnemy: false);
        var lit = _view.Modulate;

        _view.Stealthed = true; // sitting in a bush
        var stealthed = _view.Modulate;
        if (stealthed.V >= lit.V)
        {
            throw new System.Exception($"A stealthed tank should be darker; was {stealthed} vs {lit}.");
        }

        _view.Stealthed = false; // left the bush
        if (_view.Modulate != lit)
        {
            throw new System.Exception($"Leaving cover should restore the team tint; was {_view.Modulate}.");
        }
    }

    [Test]
    public void ConcealedTank_IsHiddenFromView_UntilRevealed()
    {
        var input = new FixedInput(new TankInput(NVector2.Zero, Aim: 0f, Fire: false));
        var arena = new RectArena(new NVector2(-500f, -500f), new NVector2(500f, 500f));
        var tank = new Tank(input, new World(), arena, NVector2.Zero, speed: 100f, fireInterval: 0.3f,
            projectileSpeed: 600f, maxHp: 3);
        _view.Bind(tank);

        _view.Concealed = true; // lurking in grass, no enemy near
        _view.UpdateFromModel();
        if (_view.Visible)
        {
            throw new System.Exception("A concealed tank should be hidden from view.");
        }

        _view.Concealed = false; // spotted / left cover
        _view.UpdateFromModel();
        if (!_view.Visible)
        {
            throw new System.Exception("A revealed tank should be visible again.");
        }
    }

    [Test]
    public void DownedTank_IsHidden_AndReappearsOnRespawn()
    {
        var input = new FixedInput(new TankInput(NVector2.Zero, Aim: 0f, Fire: false));
        var arena = new RectArena(new NVector2(-500f, -500f), new NVector2(500f, 500f));
        var tank = new Tank(input, new World(), arena, NVector2.Zero, speed: 100f, fireInterval: 0.3f,
            projectileSpeed: 600f, maxHp: 1, team: 0, lives: 2);
        _view.Bind(tank);

        tank.TakeDamage(1); // downed, awaiting respawn
        _view.UpdateFromModel();
        if (_view.Visible)
        {
            throw new System.Exception("A downed tank's view should be hidden while it awaits respawn.");
        }

        tank.Step(Tank.RespawnDelay + 0.1f); // revive
        _view.UpdateFromModel();
        if (!_view.Visible)
        {
            throw new System.Exception("A respawned tank's view should be visible again.");
        }
    }
}
