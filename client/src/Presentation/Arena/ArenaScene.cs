using Godot;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>M1 play scene root and composition root for gameplay: builds a keyboard/mouse
/// input source and a <see cref="Tank"/>, instances a <see cref="TankView"/> to render it,
/// and parents a camera to the view so the tank stays centred.</summary>
public partial class ArenaScene : Node2D
{
    private const float TankSpeed = 200f;

    public override void _Ready()
    {
        var input = new KeyboardMouseInputSource(GetViewport());
        var tank = new Tank(input, NVector2.Zero, TankSpeed);

        var view = GD.Load<PackedScene>("res://src/Presentation/Tank/TankView.tscn")
            .Instantiate<TankView>();
        AddChild(view);
        view.Bind(tank);

        // A lone enabled Camera2D becomes current automatically on entering the tree,
        // so it follows the view (tank) and keeps it centred — no MakeCurrent() needed.
        // Physics process callback matches the project's physics interpolation (avoids
        // Godot auto-overriding it and logging a warning).
        view.AddChild(new Camera2D { ProcessCallback = Camera2D.Camera2DProcessCallback.Physics });
    }
}
