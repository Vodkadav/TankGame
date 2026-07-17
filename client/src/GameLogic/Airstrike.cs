using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A carpet-bombing airstrike (called in by a telephone pickup): an ordered run of blast zones
/// that first light up (arm) in an expanding sweep spread across <c>armWindow</c> seconds — zone <c>i</c>
/// lights at <c>armWindow · i/(count-1)</c>, so the whole blob is telegraphed red by <c>armWindow</c>.
/// Only then do the zones detonate, in the same growth order: the first boom lands at <c>armWindow + delay</c>
/// and the rest sweep outward behind it. Each detonation damages every tank not on the caller's team within
/// the zone radius, once. Pure C# — the Presentation layer draws the zones.</summary>
public sealed class Airstrike : IAirstrike
{
    private readonly IWorld _world;
    private readonly int _callerTeam;
    private readonly int _damage;
    private readonly Vector2[] _centres;
    private readonly float _armWindow;
    private readonly float _delay;
    private readonly bool[] _detonated;
    private float _elapsed;

    /// <param name="world">The world whose tanks the blasts scan.</param>
    /// <param name="zones">Ordered zone centres (light-up and detonation order).</param>
    /// <param name="callerTeam">The calling tank's team — its side is spared.</param>
    /// <param name="zoneRadius">Blast radius of each zone.</param>
    /// <param name="armWindow">Seconds over which all zones light up, from the first to the last.</param>
    /// <param name="delay">Seconds between a zone lighting up and that zone detonating.</param>
    /// <param name="damage">Damage dealt to each tank caught in a zone's blast.</param>
    public Airstrike(IWorld world, IReadOnlyList<Vector2> zones, int callerTeam, float zoneRadius,
        float armWindow, float delay, int damage)
    {
        Id = EntityId.Next();
        _world = world;
        _callerTeam = callerTeam;
        _damage = damage;
        _centres = zones.ToArray();
        Radius = zoneRadius;
        _armWindow = armWindow;
        _delay = delay;
        _detonated = new bool[_centres.Length];
        IsAlive = _centres.Length > 0;
        Position = _centres.Length > 0 ? _centres[0] : Vector2.Zero;
    }

    public Guid Id { get; }
    public Vector2 Position { get; }
    public float Radius { get; }
    public bool IsAlive { get; private set; }

    // Highlights are spread evenly across the arm window (a single zone lights immediately), so the whole
    // blob is telegraphed by armWindow. Nothing detonates until then: the first zone booms at
    // armWindow + delay and the rest sweep behind it in the same expanding order.
    private float ArmTime(int i) => _centres.Length <= 1 ? 0f : _armWindow * i / (_centres.Length - 1);
    private float ExplodeTime(int i) => _armWindow + _delay + ArmTime(i);

    public void Step(float deltaSeconds)
    {
        if (!IsAlive)
        {
            return;
        }

        _elapsed += deltaSeconds;
        for (var i = 0; i < _centres.Length; i++)
        {
            if (_detonated[i] || _elapsed < ExplodeTime(i))
            {
                continue;
            }

            _detonated[i] = true;
            foreach (var tank in _world.Entities.OfType<Tank>())
            {
                if (tank.Hp > 0 && tank.Team != _callerTeam
                    && Vector2.DistanceSquared(_centres[i], tank.Position) <= Radius * Radius)
                {
                    tank.TakeDamage(_damage);
                }
            }
        }

        if (Array.TrueForAll(_detonated, d => d))
        {
            IsAlive = false;
        }
    }

    public IReadOnlyList<AirstrikeZone> Zones
    {
        get
        {
            var zones = new AirstrikeZone[_centres.Length];
            for (var i = 0; i < _centres.Length; i++)
            {
                var phase = _detonated[i] ? AirstrikeZonePhase.Detonated
                    : _elapsed >= ArmTime(i) ? AirstrikeZonePhase.Armed
                    : AirstrikeZonePhase.Pending;
                var countdown = MathF.Max(0f, ExplodeTime(i) - _elapsed);
                zones[i] = new AirstrikeZone(_centres[i], Radius, phase, countdown);
            }

            return zones;
        }
    }
}
