using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TankGame.GameLogic;

/// <summary>The networked match's stat book — the net victory screen's data source. No stats travel
/// on the wire: both roles feed the same per-tank hp stream (the host from its authoritative world
/// each tick, a guest from every snapshot's tank states), so damage taken, repairs and the final
/// standing all derive identically on every peer from observed hp deltas. Pure C#.</summary>
public sealed class NetMatchStats
{
    /// <summary>One slot's running tally. Mutable bookkeeping the screen reads at match end.</summary>
    public sealed class SlotTally
    {
        public byte Slot { get; init; }
        public string Name { get; set; } = string.Empty;
        public int Team { get; set; }
        public int Hp { get; internal set; }
        public bool Alive { get; internal set; } = true;
        public int DamageTaken { get; internal set; }
        public int Repairs { get; internal set; }

        /// <summary>When this tank fell, as a monotonically increasing sequence — higher means it
        /// outlived more of the field. Zero while alive.</summary>
        internal int EliminatedAt { get; set; }

        internal bool Baselined { get; set; }
    }

    private readonly Dictionary<byte, SlotTally> _bySlot = new();
    private readonly List<SlotTally> _ordered = new(); // registration order = the roster's seat order
    private int _eliminationSeq;

    /// <summary>Every observed slot's tally, in registration order.</summary>
    public IReadOnlyList<SlotTally> Tallies => _ordered;

    /// <summary>Seats a slot with its roster name and team before play (both roles derive the same
    /// roster, so both books carry the same rows).</summary>
    public void Register(byte slot, string name, int team)
    {
        var tally = GetOrCreate(slot);
        tally.Name = name;
        tally.Team = team;
    }

    /// <summary>One hp sighting for a slot. The first sighting is the baseline; every later drop is
    /// damage taken, every rise a repair, and the drop to zero stamps the elimination order.</summary>
    public void Observe(byte slot, int hp)
    {
        var tally = GetOrCreate(slot);
        if (!tally.Baselined)
        {
            tally.Baselined = true;
            tally.Hp = hp;
            tally.Alive = hp > 0;
            return;
        }

        if (hp < tally.Hp)
        {
            tally.DamageTaken += tally.Hp - hp;
        }
        else if (hp > tally.Hp)
        {
            tally.Repairs += hp - tally.Hp;
        }

        tally.Hp = hp;
        if (tally.Alive && hp <= 0)
        {
            tally.Alive = false;
            tally.EliminatedAt = ++_eliminationSeq;
        }
    }

    /// <summary>The final standing: survivors first (healthiest on top), then the fallen ranked by
    /// how long they lasted — a later death places higher.</summary>
    public IReadOnlyList<SlotTally> Standings()
        => _ordered
            .OrderByDescending(t => t.Alive)
            .ThenByDescending(t => t.Alive ? t.Hp : t.EliminatedAt)
            .ToList();

    private SlotTally GetOrCreate(byte slot)
    {
        if (!_bySlot.TryGetValue(slot, out var tally))
        {
            // A slot the roster never named (a stray snapshot) still gets a readable row.
            tally = new SlotTally
            {
                Slot = slot,
                Name = string.Format(CultureInfo.InvariantCulture, "Tank {0}", slot + 1),
                Team = slot,
            };
            _bySlot[slot] = tally;
            _ordered.Add(tally);
        }

        return tally;
    }
}
