using System;
using System.Collections.Generic;
using Godot;

namespace TankGame.Presentation;

/// <summary>A top-of-screen row of small tank icons showing how many respawns the player has left (owner
/// ask). It starts full (<see cref="MaxIcons"/> icons) and dims one each time the tank dies — a lit icon
/// is a revive still in hand, a dimmed one is spent. Drive it once a frame with <see cref="Show"/>, passing
/// the player's respawns remaining (<c>LivesRemaining - 1</c>).</summary>
public partial class RespawnHud : CanvasLayer
{
    /// <summary>The most respawns the row can show — the player starts with this many.</summary>
    public const int MaxIcons = 5;

    private readonly List<TankIcon> _icons = new();

    public override void _Ready()
    {
        var row = new HBoxContainer { Name = "Icons" };
        row.AddThemeConstantOverride("separation", 8);
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        row.GrowHorizontal = Control.GrowDirection.Both;
        row.Position += new Vector2(0f, 12f); // a small margin below the top edge
        AddChild(row);

        for (var i = 0; i < MaxIcons; i++)
        {
            var icon = new TankIcon { Name = $"TankIcon{i}" };
            _icons.Add(icon);
            row.AddChild(icon);
        }
    }

    /// <summary>Lights the first <paramref name="respawnsRemaining"/> icons and dims the rest (clamped to
    /// [0, <see cref="MaxIcons"/>]). A spent icon stays in place — greyed — so the row keeps its length.</summary>
    public void Show(int respawnsRemaining)
    {
        var lit = Math.Clamp(respawnsRemaining, 0, MaxIcons);
        for (var i = 0; i < _icons.Count; i++)
        {
            _icons[i].SetSpent(i >= lit);
        }
    }
}

/// <summary>A small procedurally-drawn top-down tank silhouette (no art asset needed, so it can't fail to
/// load on the web export). Drawn khaki when a respawn is in hand, greyed once spent.</summary>
public partial class TankIcon : Control
{
    private static readonly Color LiveTint = new(0.60f, 0.62f, 0.28f); // khaki tank
    private static readonly Color SpentTint = new(0.28f, 0.28f, 0.30f, 0.55f);
    private static readonly Color Outline = new(0f, 0f, 0f, 0.85f);

    private bool _spent;

    /// <summary>Whether this respawn has been used up (drawn greyed). False = a revive still in hand.</summary>
    public bool IsSpent => _spent;

    public override void _Ready() => CustomMinimumSize = new Vector2(34f, 34f);

    public void SetSpent(bool spent)
    {
        if (_spent == spent)
        {
            return;
        }

        _spent = spent;
        QueueRedraw();
    }

    // Top-down tank: two tread bars, a hull, a turret, and a barrel pointing up. Sized to the control.
    public override void _Draw()
    {
        var s = Size == Vector2.Zero ? new Vector2(34f, 34f) : Size;
        var body = _spent ? SpentTint : LiveTint;
        var tread = new Color(body.R * 0.55f, body.G * 0.55f, body.B * 0.55f, body.A);

        var treadW = s.X * 0.18f;
        DrawRect(new Rect2(0f, s.Y * 0.18f, treadW, s.Y * 0.64f), tread);                 // left tread
        DrawRect(new Rect2(s.X - treadW, s.Y * 0.18f, treadW, s.Y * 0.64f), tread);       // right tread

        var hull = new Rect2(s.X * 0.22f, s.Y * 0.24f, s.X * 0.56f, s.Y * 0.52f);
        DrawRect(hull, body);
        DrawRect(hull, Outline, filled: false, width: 1.5f);

        DrawRect(new Rect2(s.X * 0.46f, s.Y * 0.04f, s.X * 0.08f, s.Y * 0.30f), body);    // barrel
        DrawCircle(new Vector2(s.X * 0.5f, s.Y * 0.5f), s.X * 0.16f, tread);              // turret
    }
}
