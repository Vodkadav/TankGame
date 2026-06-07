using Godot;

namespace TankGame.Presentation;

/// <summary>The per-team multiply tint applied to a tank sprite via <c>Sprite2D.Modulate</c>, so one
/// neutral (near-white) tank texture reads as a vivid per-side colour. Up to four teams, each a distinct
/// colour in the spirit of the cartoon tank-battle reference: team 0 (the player) is green, then red,
/// blue, yellow. Centralised here so the local and net arena scenes tint consistently from one source.</summary>
public static class TeamPalette
{
    private static readonly Color[] Colours =
    {
        new(0.45f, 0.85f, 0.40f), // 0 — player, green
        new(0.95f, 0.40f, 0.40f), // 1 — red
        new(0.45f, 0.62f, 0.98f), // 2 — blue
        new(0.98f, 0.82f, 0.30f), // 3 — yellow
    };

    /// <summary>The tint for a team, wrapping modulo the palette so any team index renders.</summary>
    public static Color TintFor(int team) => Colours[((team % Colours.Length) + Colours.Length) % Colours.Length];
}
