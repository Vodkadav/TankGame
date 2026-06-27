namespace TankGame.GameLogic;

/// <summary>The streak tier a kill reached — escalating announcer callouts.</summary>
public enum StreakTier
{
    /// <summary>A lone kill (or the first after a reset / window lapse).</summary>
    Single,

    /// <summary>Two consecutive kills inside the window.</summary>
    Double,

    /// <summary>Three consecutive kills inside the window.</summary>
    Triple,

    /// <summary>Four or more consecutive kills inside the window.</summary>
    Multi,
}

/// <summary>Tracks one combatant's kill streak: every kill landed since the combatant last died
/// extends the streak, with no time limit between kills. Only <see cref="Reset"/> (the combatant's
/// own death) ends it. Pure C# so it is unit-tested without Godot, like
/// <see cref="BattleStats"/>.</summary>
public sealed class KillStreakTracker
{
    private int _count;

    /// <summary>The current consecutive-kill count (0 before the first kill / after a reset).</summary>
    public int Count => _count;

    /// <summary>Register a kill: extends the streak by one and returns the tier reached. The streak
    /// is bounded only by <see cref="Reset"/> — any kill while alive counts, however long since the
    /// last one (owner ask: a streak is "kills without dying", not "kills in quick succession").</summary>
    public StreakTier RegisterKill()
    {
        _count++;
        return _count switch
        {
            1 => StreakTier.Single,
            2 => StreakTier.Double,
            3 => StreakTier.Triple,
            _ => StreakTier.Multi,
        };
    }

    /// <summary>Drop the streak so the next kill starts fresh (the combatant died).</summary>
    public void Reset() => _count = 0;
}
