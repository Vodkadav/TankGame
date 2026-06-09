using System;
using TankGame.GameLogic;

namespace TankGame.Presentation;

/// <summary>How a round is set up.</summary>
public enum GameMode
{
    /// <summary>One player versus the AI adversaries.</summary>
    OnePlayer,

    /// <summary>Two players on the same team versus the AI adversaries.</summary>
    TwoPlayerCoop,

    /// <summary>Two players against each other, no AI — last tank standing.</summary>
    TwoPlayerVersus,
}

/// <summary>A selectable battle arena (the map pool). The current procedural arena is "Desert War";
/// "Cliffs &amp; Valleys" is the upcoming multi-layer map (ADR-0018) and is not playable yet.</summary>
public enum ArenaId
{
    /// <summary>The dusty procedural square arena — the game's first map.</summary>
    DesertWar,

    /// <summary>The elevated multi-layer map (ADR-0018); not yet built.</summary>
    CliffsAndValleys,
}

/// <summary>Carries match-level state from the title screen into the play scene, which is loaded
/// fresh each round (so it cannot be passed by constructor). The <see cref="Series"/> persists
/// across the per-round scene reloads that drive a best-of-N match; <see cref="StartNewMatch"/>
/// resets it. Defaults let the play scene launch directly (tests, dev) without the title.</summary>
public static class GameSetup
{
    /// <summary>Round wins needed to take the match — best of three.</summary>
    public const int RoundsToWin = 2;

    public static GameMode Mode { get; set; } = GameMode.OnePlayer;

    /// <summary>The chosen battle arena (the Select Map screen sets it). Only <see cref="ArenaId.DesertWar"/>
    /// is playable today; it is the map the 3D play scene builds.</summary>
    public static ArenaId Arena { get; set; } = ArenaId.DesertWar;

    /// <summary>A user-built map to play instead of the procedural arena, set when the player picks one
    /// from "My Maps". Null means play the built-in <see cref="Arena"/>. Cleared by
    /// <see cref="StartNewMatch"/>, so any built-in launch resets it; the custom-map launch sets it back
    /// afterwards. It then persists across the per-round scene reloads of a best-of-N match.</summary>
    public static MapDefinition? CustomMap { get; set; }

    /// <summary>The arena's visual palette (S8 theming): ground colour + wall tint. A title control
    /// can later set it; defaults to the sandy reference look.</summary>
    public static ArenaTheme Theme { get; set; } = ArenaTheme.Default;

    /// <summary>The active match modifier (S9): a whole-round rule applied to every tank at spawn.
    /// A title control can later set it; defaults to none (a plain match).</summary>
    public static MatchModifier Modifier { get; set; } = MatchModifier.None;

    /// <summary>The running best-of-N series; survives per-round scene reloads.</summary>
    public static SeriesTracker Series { get; private set; } = new(RoundsToWin);

    /// <summary>Seed for the procedural arena (S8). Fixed by default so a direct launch (tests, dev)
    /// is reproducible; a new match randomises it so each match gets a fresh battlefield, and it
    /// then persists across the per-round scene reloads so a best-of-N series plays one arena.</summary>
    public static int ArenaSeed { get; private set; } = 1;

    /// <summary>Size of the generated arena, in tiles. Adjustable per match (a title control can set
    /// it); the camera framing and the generator both follow it, so any sensible size works.</summary>
    public static int ArenaWidth { get; set; } = 28;
    public static int ArenaHeight { get; set; } = 16;

    /// <summary>Begins a fresh match in <paramref name="mode"/>: sets the mode, resets the series to
    /// 0 - 0, and rolls a new arena seed. Called from the title screen and from "Play again".</summary>
    public static void StartNewMatch(GameMode mode)
    {
        Mode = mode;
        Series = new SeriesTracker(RoundsToWin);
        ArenaSeed = Guid.NewGuid().GetHashCode();
        CustomMap = null; // a fresh match defaults to the built-in arena; My Maps sets it back after
    }
}
