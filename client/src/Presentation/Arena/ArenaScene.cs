using System;
using Godot;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>M1 play scene root and gameplay composition root: builds the input source, a
/// <see cref="Tank"/>, and a bounding <see cref="RectArena"/>; renders the tank with a
/// following camera; draws the arena walls; and on fire-input spawns a <see cref="Projectile"/>
/// (rate-limited) with a <see cref="ProjectileView"/> that despawns when it hits a wall.</summary>
public partial class ArenaScene : Node2D
{
    private const float TankSpeed = 200f;
    private const float ProjectileSpeed = 600f;
    private const double FireInterval = 0.3;

    private static readonly NVector2 ArenaMin = new(-400f, -300f);
    private static readonly NVector2 ArenaMax = new(400f, 300f);

    private KeyboardMouseInputSource _input = null!;
    private Tank _tank = null!;
    private RectArena _arena = null!;
    private double _fireCooldown;

    public override void _Ready()
    {
        _input = new KeyboardMouseInputSource(GetViewport());
        _tank = new Tank(_input, NVector2.Zero, TankSpeed);
        _arena = new RectArena(ArenaMin, ArenaMax);

        AddChild(BuildWalls());
        AddChild(BuildInstructionsOverlay());

        var view = GD.Load<PackedScene>("res://src/Presentation/Tank/TankView.tscn")
            .Instantiate<TankView>();
        AddChild(view);
        view.Bind(_tank);
        view.AddChild(new Camera2D { ProcessCallback = Camera2D.Camera2DProcessCallback.Physics });
    }

    public override void _Process(double delta)
    {
        _fireCooldown -= delta;
        if (_fireCooldown <= 0d && _input.Read().Fire)
        {
            _fireCooldown = FireInterval;
            FireProjectile();
        }
    }

    private void FireProjectile()
    {
        var aim = _tank.TurretRotation;
        var direction = new NVector2(MathF.Cos(aim), MathF.Sin(aim));
        var projectile = new Projectile(_arena, _tank.Position, direction, ProjectileSpeed);

        var view = GD.Load<PackedScene>("res://src/Presentation/Projectile/ProjectileView.tscn")
            .Instantiate<ProjectileView>();
        AddChild(view);
        view.Bind(projectile);
    }

    // Screen-space overlay so the "how to play" line stays put while the camera tracks
    // the tank. The Label's text is the translation key; Godot auto-translates it via tr().
    private static CanvasLayer BuildInstructionsOverlay()
    {
        var layer = new CanvasLayer { Name = "InstructionsLayer" };
        var label = new Label { Name = "Instructions", Text = "m1.instructions" };
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.GrowHorizontal = Control.GrowDirection.Both;
        layer.AddChild(label);
        return layer;
    }

    private static Line2D BuildWalls()
    {
        var line = new Line2D { Width = 4f, DefaultColor = new Color(0.5f, 0.5f, 0.55f) };
        line.AddPoint(new Vector2(ArenaMin.X, ArenaMin.Y));
        line.AddPoint(new Vector2(ArenaMax.X, ArenaMin.Y));
        line.AddPoint(new Vector2(ArenaMax.X, ArenaMax.Y));
        line.AddPoint(new Vector2(ArenaMin.X, ArenaMax.Y));
        line.AddPoint(new Vector2(ArenaMin.X, ArenaMin.Y));
        return line;
    }
}
