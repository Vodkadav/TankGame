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

/// <summary>Carries match-level state from the title screen into the play scene, which is loaded
/// fresh each round (so it cannot be passed by constructor). The <see cref="Series"/> persists
/// across the per-round scene reloads that drive a best-of-N match; <see cref="StartNewMatch"/>
/// resets it. Defaults let the play scene launch directly (tests, dev) without the title.</summary>
public static class GameSetup
{
    /// <summary>Round wins needed to take the match — best of three.</summary>
    public const int RoundsToWin = 2;

    public static GameMode Mode { get; set; } = GameMode.OnePlayer;

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
    }
}
