using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A carpet-bombing airstrike (called in by a telephone pickup): an ordered run of blast zones
/// that arm one after another — zone <c>i</c> lights up at <c>i·step</c> — and then detonate in the same
/// order, the first detonating as the last is still lighting up (zone <c>i</c> booms at
/// <c>(count·step) + i·step</c>). Each detonation damages every tank not on the caller's team within the
/// zone radius, once. Pure C# — the Presentation layer draws the zones and the countdown.</summary>
public sealed class Airstrike : IAirstrike
{
    private readonly IWorld _world;
    private readonly int _callerTeam;
    private readonly int _damage;
    private readonly Vector2[] _centres;
    private readonly float _step;
    private readonly bool[] _detonated;
    private float _elapsed;

    /// <param name="world">The world whose tanks the blasts scan.</param>
    /// <param name="zones">Ordered zone centres (light-up and detonation order).</param>
    /// <param name="callerTeam">The calling tank's team — its side is spared.</param>
    /// <param name="zoneRadius">Blast radius of each zone.</param>
    /// <param name="step">Seconds between each zone lighting up (and between each detonation).</param>
    /// <param name="damage">Damage dealt to each tank caught in a zone's blast.</param>
    public Airstrike(IWorld world, IReadOnlyList<Vector2> zones, int callerTeam, float zoneRadius, float step, int damage)
    {
        Id = Guid.NewGuid();
        _world = world;
        _callerTeam = callerTeam;
        _damage = damage;
        _centres = zones.ToArray();
        Radius = zoneRadius;
        _step = step;
        _detonated = new bool[_centres.Length];
        IsAlive = _centres.Length > 0;
        Position = _centres.Length > 0 ? _centres[0] : Vector2.Zero;
    }

    public Guid Id { get; }
    public Vector2 Position { get; }
    public float Radius { get; }
    public bool IsAlive { get; private set; }

    private float ExplodeTime(int i) => (_centres.Length * _step) + (i * _step);
    private float ArmTime(int i) => i * _step;

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
