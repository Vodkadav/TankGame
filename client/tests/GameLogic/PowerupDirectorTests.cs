using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class PowerupDirectorTests
{
    private const float TileSize = 64f;
    private const float PickupRadius = 20f;

    private sealed class NoInput : IInputSource
    {
        public TankInput Read() => new(Vector2.Zero, Aim: 0f, Fire: false);
    }

    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => false;
    }

    private sealed class NoEffect : IPickupEffect
    {
        public void ApplyTo(Tank tank, IWorld world) { }
    }

    private static Vector2 CellCentre(int x, int y) => new((x + 0.5f) * TileSize, (y + 0.5f) * TileSize);

    private static PowerupDirector DirectorFor(World world, int seed, IReadOnlyList<(int X, int Y)> floorCells) =>
        new(world, seed, floorCells, Vector2.Zero, TileSize,
            (kind, pos) => new Powerup(world, pos, kind, new NoEffect(), PickupRadius));

    private static List<IPowerup> LivePickups(World world) => world.Entities.OfType<IPowerup>().ToList();

    private static void StepSeconds(World world, float seconds)
    {
        // 0.05 s ticks like the net cadence — small enough that interval edges are exercised.
        for (var t = 0f; t < seconds; t += 0.05f)
        {
            world.Step(0.05f);
        }
    }

    [Fact]
    public void FirstSpawn_LandsWithinTheJitteredWindow()
    {
        var world = new World();
        world.Spawn(DirectorFor(world, seed: 42, new[] { (5, 5) }));

        StepSeconds(world, 14.8f);
        Assert.Empty(LivePickups(world)); // never before 20 - 5 s

        StepSeconds(world, 10.5f); // now past 25.3 s total — beyond 20 + 5 s
        Assert.Single(LivePickups(world));
    }

    [Fact]
    public void SpawnSequence_IsDeterministic_ForTheSameSeed()
    {
        List<(PowerupKind Kind, Vector2 Pos)> Run(int seed)
        {
            var world = new World();
            var spawned = new List<(PowerupKind, Vector2)>();
            world.EntitySpawned += e =>
            {
                if (e is IPowerup p)
                {
                    spawned.Add((p.Kind, p.Position));
                }
            };
            var cells = new List<(int X, int Y)>();
            for (var x = 1; x < 20; x++)
            {
                for (var y = 1; y < 20; y++)
                {
                    cells.Add((x, y));
                }
            }

            world.Spawn(DirectorFor(world, seed, cells));
            StepSeconds(world, 80f); // at least three spawns at 15-25 s apart
            return spawned;
        }

        var first = Run(7);
        var second = Run(7);
        Assert.True(first.Count >= 3);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Placement_Rejects_CellsWithinTwoCellsOfALiveTank()
    {
        var world = new World();
        var tank = new Tank(new NoInput(), world, new OpenArena(), CellCentre(2, 2),
            speed: 100f, fireInterval: 0.3f, projectileSpeed: 600f);
        world.Spawn(tank);

        // Every candidate except (10, 10) is within 2 cells (Chebyshev) of the tank at (2, 2).
        world.Spawn(DirectorFor(world, seed: 1, new[] { (1, 1), (2, 3), (4, 4), (10, 10) }));
        StepSeconds(world, 26f);

        var pickup = Assert.Single(LivePickups(world));
        Assert.Equal(CellCentre(10, 10), pickup.Position);
    }

    [Fact]
    public void Placement_Rejects_CellsWithinTwoCellsOfAnExistingPickup()
    {
        var world = new World();
        world.Spawn(new Powerup(world, CellCentre(2, 2), PowerupKind.Repair, new NoEffect(), PickupRadius));

        world.Spawn(DirectorFor(world, seed: 1, new[] { (2, 2), (3, 3), (0, 4), (10, 10) }));
        StepSeconds(world, 26f);

        var pickups = LivePickups(world);
        Assert.Equal(2, pickups.Count);
        Assert.Contains(pickups, p => p.Position == CellCentre(10, 10));
    }

    [Fact]
    public void Spawn_IsSkipped_WhileSixPickupsAreLive()
    {
        var world = new World();
        for (var i = 0; i < 6; i++)
        {
            world.Spawn(new Powerup(world, CellCentre(10 + (i * 3), 20), PowerupKind.Repair, new NoEffect(), PickupRadius));
        }

        world.Spawn(DirectorFor(world, seed: 3, new[] { (1, 1) }));
        StepSeconds(world, 26f);

        Assert.Equal(6, LivePickups(world).Count); // the due spawn was skipped, not queued
    }

    [Fact]
    public void PickKind_FollowsTheLockedWeights()
    {
        // Repair + the three ammo crates weight 5, Speed/RapidFire/Shield 3, Missile/Telephone 1 (of 31).
        var rng = new Random(11);
        var counts = new Dictionary<PowerupKind, int>();
        const int draws = 31_000;
        for (var i = 0; i < draws; i++)
        {
            var kind = PowerupDirector.PickKind(rng);
            counts[kind] = counts.GetValueOrDefault(kind) + 1;
        }

        Assert.InRange(counts[PowerupKind.Repair], 4500, 5500);        // ~5000
        Assert.InRange(counts[PowerupKind.PiercingAmmo], 4500, 5500);  // ~5000
        Assert.InRange(counts[PowerupKind.Shield], 2600, 3400);        // ~3000
        Assert.InRange(counts[PowerupKind.Missile], 700, 1300);        // ~1000
        Assert.InRange(counts[PowerupKind.Telephone], 700, 1300);      // ~1000
    }
}
