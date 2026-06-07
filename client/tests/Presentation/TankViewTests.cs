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
        // Loading the scene builds the 3D tank in its SubViewport and loads Tank3D.glb (M1-T2).
        _view = GD.Load<PackedScene>("res://src/Presentation/Tank/TankView.tscn")
            .Instantiate<TankView>();
        TestScene.AddChild(_view);
    }

    [Cleanup]
    public void Cleanup() => _view.QueueFree();

    private static T? Find<T>(Node node, System.Func<T, bool>? pred = null) where T : Node
    {
        if (node is T hit && (pred is null || pred(hit)))
        {
            return hit;
        }

        foreach (var child in node.GetChildren())
        {
            if (Find(child, pred) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static bool YawClose(float a, float b)
    {
        var d = Mathf.Wrap(a - b, -Mathf.Pi, Mathf.Pi);
        return Mathf.Abs(d) < 0.02f;
    }

    [Test]
    public void Scene_BuildsThe3DTankInASubViewport()
    {
        var viewport = Find<SubViewport>(_view);
        var sprite = Find<Sprite2D>(_view);
        var hull = Find<Node3D>(_view, n => n.Name.ToString().Contains("Base"));
        var turret = Find<Node3D>(_view, n => n.Name.ToString().Contains("Turret"));

        if (viewport is null || sprite is null || hull is null || turret is null)
        {
            throw new System.Exception("TankView must build a SubViewport, a display Sprite2D, and the 3D hull + turret nodes.");
        }
    }

    [Test]
    public void UpdateFromModel_MirrorsPosition_AndAimsTheTurretIndependentlyOfTheHull()
    {
        var input = new FixedInput(new TankInput(new NVector2(1f, 0f), Aim: 0.75f, Fire: false));
        var arena = new RectArena(new NVector2(-500f, -500f), new NVector2(500f, 500f));
        var tank = new Tank(input, new World(), arena, NVector2.Zero, speed: 100f, fireInterval: 0.3f, projectileSpeed: 600f);
        _view.Bind(tank);

        tank.Step(0.1f); // advances 10 units along +X (heading 0); the turret aims at 0.75 rad
        _view.UpdateFromModel();

        // World (10,0) projects to iso ((10-0)*1, (10+0)*0.5) = (10, 5).
        if (Mathf.Abs(_view.Position.X - 10f) > 0.01f || Mathf.Abs(_view.Position.Y - 5f) > 0.01f)
        {
            throw new System.Exception($"View should mirror the tank to iso (10,5); was {_view.Position}.");
        }

        var hull = Find<Node3D>(_view, n => n.Name.ToString().Contains("Base"))!;
        var turret = Find<Node3D>(_view, n => n.Name.ToString().Contains("Turret"))!;

        // The turret is a child of the hull, so its world yaw is hull.Y + turret.Y. Aiming 0.75 off the
        // chassis must leave the turret's world yaw 0.75 from the hull's — proving it aims independently
        // (and the right way: the rendered turn winds opposite the game angle, so it trails).
        var worldDelta = turret.Rotation.Y; // turret local yaw == turretWorld - hullWorld by construction
        if (!YawClose(worldDelta, tank.Rotation - tank.TurretRotation))
        {
            throw new System.Exception($"Turret should aim 0.75 rad off the hull independently; local yaw was {worldDelta}.");
        }
    }

    [Test]
    public void ApplyTeamTint_PaintsTheBodyMaterial_WithTheTeamColour()
    {
        _view.ApplyTeamTint(1);
        var body = Find<MeshInstance3D>(_view, m => m.Name.ToString().Contains("Base"))!;
        var mat = body.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;

        if (mat is null || mat.AlbedoColor != TeamPalette.TintFor(1))
        {
            throw new System.Exception($"The body material should take team 1's colour; was {mat?.AlbedoColor}.");
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
    public void Stealthed_DarkensTheTank()
    {
        var sprite = Find<Sprite2D>(_view)!;

        _view.Stealthed = true; // sitting in a bush
        if (sprite.Modulate.V >= 1f)
        {
            throw new System.Exception($"A stealthed tank should be darkened; modulate was {sprite.Modulate}.");
        }

        _view.Stealthed = false; // left the bush
        if (sprite.Modulate != Colors.White)
        {
            throw new System.Exception($"Leaving cover should restore full brightness; was {sprite.Modulate}.");
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
