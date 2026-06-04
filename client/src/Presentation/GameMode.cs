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

    /// <summary>Begins a fresh match in <paramref name="mode"/>: sets the mode and resets the
    /// series to 0 - 0. Called from the title screen and from "Play again".</summary>
    public static void StartNewMatch(GameMode mode)
    {
        Mode = mode;
        Series = new SeriesTracker(RoundsToWin);
    }
}
