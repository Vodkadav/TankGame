using System;
using System.Collections.Generic;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>Drops powerups onto the field over time (the "boost director"): a world entity ticked by
/// the ordinary step loop that spawns one weighted-random pickup every ~20 s (±5 s jitter) on a random
/// passable floor cell, rejecting spots within 2 cells of any live tank or existing pickup and skipping
/// a spawn while 6 pickups are already live. Deterministic from its seed (solo: the arena seed; net:
/// the match seed) — NEVER time-seeded — so the same match drops the same crates in the same order.
/// Pure C#: the scene supplies the pickup factory (kind + position → a <see cref="Powerup"/> with its
/// effect), so the balance catalogue stays out of GameLogic timing.</summary>
public sealed class PowerupDirector : IEntity
{
    /// <summary>Mean seconds between drops.</summary>
    public const float BaseIntervalSeconds = 20f;

    /// <summary>Uniform jitter around <see cref="BaseIntervalSeconds"/> (so 15–25 s).</summary>
    public const float IntervalJitterSeconds = 5f;

    /// <summary>No new drop while this many pickups are already on the field.</summary>
    public const int MaxLivePickups = 6;

    /// <summary>A candidate cell within this Chebyshev cell distance of a live tank or a pickup is rejected.</summary>
    public const int MinCellSeparation = 2;

    /// <summary>How long a director-dropped pickup stays before fading uncollected — the scenes pass
    /// this to their pickup factories (<see cref="Powerup"/>'s <c>despawnAfter</c>).</summary>
    public const float DespawnSeconds = 30f;

    private const int PlacementTries = 30;

    // Locked weights (of 27): Repair + the ammo crates 5, the stat boosts 3, the heavy hitters 1.
    private static readonly (PowerupKind Kind, int Weight)[] KindWeights =
    {
        (PowerupKind.Repair, 5),
        (PowerupKind.BouncingAmmo, 5),
        (PowerupKind.SpreadAmmo, 5),
        (PowerupKind.PiercingAmmo, 5),
        (PowerupKind.SpeedBoost, 3),
        (PowerupKind.RapidFire, 3),
        (PowerupKind.Shield, 3),
        (PowerupKind.Missile, 1),
        (PowerupKind.Telephone, 1),
    };

    private readonly IWorld _world;
    private readonly Random _rng;
    private readonly IReadOnlyList<(int X, int Y)> _floorCells;
    private readonly Vector2 _origin;
    private readonly float _tileSize;
    private readonly Func<PowerupKind, Vector2, Powerup> _createPickup;
    private float _untilNextSpawn;

    /// <param name="world">The world scanned for live tanks/pickups and spawned into.</param>
    /// <param name="seed">Deterministic RNG seed — the arena seed (solo) or the match seed (net).</param>
    /// <param name="floorCells">Candidate drop cells: the arena's passable floor. In generated arenas
    /// every passable cell is reachable by construction (the generator walls off cut-off floor).</param>
    /// <param name="origin">World position of cell (0, 0)'s corner.</param>
    /// <param name="tileSize">World units per cell.</param>
    /// <param name="createPickup">Builds the pickup (kind + world position → a Powerup wired with its
    /// effect and the 30 s despawn timer); the director spawns the result into the world.</param>
    public PowerupDirector(IWorld world, int seed, IReadOnlyList<(int X, int Y)> floorCells,
        Vector2 origin, float tileSize, Func<PowerupKind, Vector2, Powerup> createPickup)
    {
        _world = world;
        _rng = new Random(seed);
        _floorCells = floorCells;
        _origin = origin;
        _tileSize = tileSize;
        _createPickup = createPickup;
        _untilNextSpawn = NextInterval();
    }

    public Guid Id { get; } = Guid.NewGuid();
    public Vector2 Position => Vector2.Zero; // bookkeeping entity — nowhere on the field
    public bool IsAlive => true;

    public void Step(float deltaSeconds)
    {
        _untilNextSpawn -= deltaSeconds;
        if (_untilNextSpawn > 0f)
        {
            return;
        }

        _untilNextSpawn += NextInterval();
        TrySpawn();
    }

    /// <summary>One weighted draw over the locked kind table. Public static so the weights are testable
    /// without stepping a world through hundreds of intervals.</summary>
    public static PowerupKind PickKind(Random rng)
    {
        var total = 0;
        foreach (var (_, weight) in KindWeights)
        {
            total += weight;
        }

        var roll = rng.Next(total);
        foreach (var (kind, weight) in KindWeights)
        {
            roll -= weight;
            if (roll < 0)
            {
                return kind;
            }
        }

        return PowerupKind.Repair; // unreachable — the weights sum to total
    }

    private float NextInterval() =>
        BaseIntervalSeconds + ((((float)_rng.NextDouble() * 2f) - 1f) * IntervalJitterSeconds);

    private void TrySpawn()
    {
        var live = 0;
        foreach (var entity in _world.Entities)
        {
            if (entity is IPowerup)
            {
                live++;
            }
        }

        if (live >= MaxLivePickups || _floorCells.Count == 0)
        {
            return; // field is saturated — this drop is skipped, not queued
        }

        var kind = PickKind(_rng);
        for (var attempt = 0; attempt < PlacementTries; attempt++)
        {
            var cell = _floorCells[_rng.Next(_floorCells.Count)];
            if (!IsClear(cell))
            {
                continue;
            }

            _world.Spawn(_createPickup(kind, CellCentre(cell)));
            return;
        }
        // Every try landed too close to a tank or pickup — skip this drop; the next one re-rolls.
    }

    private bool IsClear((int X, int Y) cell)
    {
        foreach (var entity in _world.Entities)
        {
            var occupied = entity switch
            {
                ITank tank when tank.Hp > 0 => true,
                IPowerup => true,
                _ => false,
            };

            if (!occupied)
            {
                continue;
            }

            var other = CellOf(entity.Position);
            if (Math.Max(Math.Abs(cell.X - other.X), Math.Abs(cell.Y - other.Y)) <= MinCellSeparation)
            {
                return false;
            }
        }

        return true;
    }

    private (int X, int Y) CellOf(Vector2 position) => (
        (int)MathF.Floor((position.X - _origin.X) / _tileSize),
        (int)MathF.Floor((position.Y - _origin.Y) / _tileSize));

    private Vector2 CellCentre((int X, int Y) cell) => new(
        _origin.X + ((cell.X + 0.5f) * _tileSize),
        _origin.Y + ((cell.Y + 0.5f) * _tileSize));
}
