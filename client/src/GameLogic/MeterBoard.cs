using System;
using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>Per-team performance meters for a match: total damage dealt, kills, and deaths. Fed by
/// <see cref="CombatResolver.Hit"/> (S9). Pure C# — deterministic and engine-free, so the same
/// meters can be computed server-side later. Raises <see cref="Changed"/> on every recorded hit so
/// a HUD can re-render without polling. Single responsibility: it only tallies — the round-winning
/// score lives in <see cref="ScoreBoard"/>.</summary>
public sealed class MeterBoard
{
    private readonly Dictionary<int, int> _damage = new();
    private readonly Dictionary<int, int> _kills = new();
    private readonly Dictionary<int, int> _deaths = new();

    /// <summary>Fired after a hit is recorded so observers (the HUD) can refresh.</summary>
    public event Action? Changed;

    /// <summary>Records one landed shot: adds its damage to the shooter's meter and, if it was the
    /// killing blow, a kill to the shooter and a death to the victim.</summary>
    public void Record(int shooterTeam, int victimTeam, int amount, bool killed)
    {
        _damage[shooterTeam] = DamageBy(shooterTeam) + amount;
        if (killed)
        {
            _kills[shooterTeam] = KillsBy(shooterTeam) + 1;
            _deaths[victimTeam] = DeathsOf(victimTeam) + 1;
        }

        Changed?.Invoke();
    }

    /// <summary>Total damage <paramref name="team"/> has dealt (0 if none).</summary>
    public int DamageBy(int team) => _damage.GetValueOrDefault(team);

    /// <summary>Kills <paramref name="team"/> has scored (0 if none).</summary>
    public int KillsBy(int team) => _kills.GetValueOrDefault(team);

    /// <summary>Times <paramref name="team"/> has been destroyed (0 if none).</summary>
    public int DeathsOf(int team) => _deaths.GetValueOrDefault(team);
}
