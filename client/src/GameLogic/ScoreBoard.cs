using System;
using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>Per-team kill tally for a match. A kill is credited to the team whose shot
/// destroyed a tank (see <see cref="CombatResolver.TankKilled"/>). Pure C# — deterministic and
/// engine-free, so the same score can be computed server-side later. Raises
/// <see cref="Changed"/> on every recorded kill so a HUD can re-render without polling.</summary>
public sealed class ScoreBoard
{
    private readonly Dictionary<int, int> _kills = new();

    /// <summary>Fired after a kill is recorded so observers (the HUD) can refresh.</summary>
    public event Action? Changed;

    /// <summary>Credits one kill to <paramref name="team"/>.</summary>
    public void RecordKill(int team)
    {
        _kills[team] = KillsFor(team) + 1;
        Changed?.Invoke();
    }

    /// <summary>Kills credited to <paramref name="team"/> so far (0 if it has none).</summary>
    public int KillsFor(int team) => _kills.GetValueOrDefault(team);
}
