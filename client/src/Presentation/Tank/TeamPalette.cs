using Godot;

namespace TankGame.Presentation;

/// <summary>The per-team body colour for the 3D tank, set as the albedo of the model's main material via
/// <c>TankView.ApplyTeamTint</c>, so one model reads as a vivid per-side colour while its black tracks and
/// yellow lights stay fixed. Up to four teams, each a distinct bright colour in the spirit of the cartoon
/// tank-battle reference: team 0 (the player) is green, then red, blue, yellow. One source of truth so the
/// local and net arena scenes colour consistently.</summary>
public static class TeamPalette
{
    private static readonly Color[] Colours =
    {
        new(0.30f, 0.78f, 0.28f), // 0 — player, green
        new(0.90f, 0.22f, 0.20f), // 1 — red
        new(0.20f, 0.46f, 0.92f), // 2 — blue
        new(0.97f, 0.78f, 0.12f), // 3 — yellow
    };

    /// <summary>The tint for a team, wrapping modulo the palette so any team index renders.</summary>
    public static Color TintFor(int team) => Colours[((team % Colours.Length) + Colours.Length) % Colours.Length];
}
