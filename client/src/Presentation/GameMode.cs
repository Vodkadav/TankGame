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

/// <summary>Carries the selected <see cref="GameMode"/> from the title screen into the play
/// scene, which is loaded fresh (so it cannot be passed by constructor). Defaults to
/// <see cref="GameMode.OnePlayer"/> so launching the play scene directly still works.</summary>
public static class GameSetup
{
    public static GameMode Mode { get; set; } = GameMode.OnePlayer;
}
