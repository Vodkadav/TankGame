using Godot;

namespace TankGame.Presentation;

/// <summary>A tiny lobby-seat badge that says at a glance whether a seat is a human player (a round
/// person) or a computer-controlled tank (a blocky robot) — owner ask 2026-07-08. Drawn in code so it
/// stays crisp at this size and needs no art asset. Colours match the cartoon menu palette.</summary>
public partial class SeatIcon : Control
{
    private static readonly Color Ink = new(0.24f, 0.15f, 0.09f);
    private static readonly Color Human = new(0.36f, 0.74f, 0.36f);  // friendly green
    private static readonly Color Robot = new(0.58f, 0.64f, 0.74f);  // steel blue-gray
    private const float Size = 26f;

    private bool _isHuman;

    public SeatIcon()
    {
        CustomMinimumSize = new Vector2(Size, Size);
    }

    /// <summary>True = a joined human; false = an AI-filled seat.</summary>
    public bool IsHuman
    {
        get => _isHuman;
        set
        {
            if (_isHuman == value)
            {
                return;
            }

            _isHuman = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_isHuman)
        {
            DrawHuman();
        }
        else
        {
            DrawRobot();
        }
    }

    private void DrawHuman()
    {
        var head = new Vector2(13f, 9f);
        DrawCircle(head, 5.5f, Ink);
        DrawCircle(head, 4.5f, Human);
        // Shoulders: a rounded body block.
        DrawRect(new Rect2(4f, 15f, 18f, 9f), Ink);
        DrawRect(new Rect2(5f, 16f, 16f, 8f), Human);
    }

    private void DrawRobot()
    {
        // Antenna.
        DrawLine(new Vector2(13f, 3f), new Vector2(13f, 7f), Ink, 2f);
        DrawCircle(new Vector2(13f, 3f), 2f, Robot);
        // Head with two eyes.
        DrawRect(new Rect2(5f, 7f, 16f, 9f), Ink);
        DrawRect(new Rect2(6f, 8f, 14f, 7f), Robot);
        DrawRect(new Rect2(9f, 10f, 2.5f, 2.5f), Ink);
        DrawRect(new Rect2(14.5f, 10f, 2.5f, 2.5f), Ink);
        // Body.
        DrawRect(new Rect2(6f, 17f, 14f, 7f), Ink);
        DrawRect(new Rect2(7f, 18f, 12f, 6f), Robot);
    }
}
