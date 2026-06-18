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

/// <summary>Tracks one combatant's kill streak: consecutive kills landed within a sliding time
/// window. A kill outside the window — or after <see cref="Reset"/> (e.g. the combatant's own
/// death) — starts a fresh streak at one. Pure C# so it is unit-tested without Godot, like
/// <see cref="BattleStats"/>.</summary>
public sealed class KillStreakTracker
{
    private readonly float _windowSeconds;
    private int _count;
    private float _lastKillTime;
    private bool _hasKill;

    public KillStreakTracker(float windowSeconds = 4f) => _windowSeconds = windowSeconds;

    /// <summary>The current consecutive-kill count (0 before the first kill).</summary>
    public int Count => _count;

    /// <summary>Register a kill at <paramref name="now"/> (seconds, monotonic). A kill within the
    /// window of the previous one extends the streak; otherwise it resets to a single. Returns the
    /// tier reached.</summary>
    public StreakTier RegisterKill(float now)
    {
        _count = _hasKill && now - _lastKillTime <= _windowSeconds ? _count + 1 : 1;
        _hasKill = true;
        _lastKillTime = now;
        return _count switch
        {
            1 => StreakTier.Single,
            2 => StreakTier.Double,
            3 => StreakTier.Triple,
            _ => StreakTier.Multi,
        };
    }

    /// <summary>Drop the streak so the next kill starts fresh (e.g. the combatant died).</summary>
    public void Reset()
    {
        _count = 0;
        _hasKill = false;
    }
}
