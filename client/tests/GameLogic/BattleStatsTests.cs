using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

// Per-tank combat bookkeeping for the victory screen (owner feedback 2026-06-11): shots, hits,
// misses, kills, deaths, damage dealt/taken — driven through the REAL fire→fly→resolve loop
// (Tank, Projectile, CombatResolver, World), not synthetic calls.
public class BattleStatsTests
{
    private sealed class ScriptedInput(TankInput value) : IInputSource
    {
        public TankInput Value { get; set; } = value;
        public TankInput Read() => Value;
    }

    private const float TileSize = 64f;

    private sealed class Rig
    {
        public World World { get; }
        public CombatResolver Combat { get; }
        public BattleStats Stats { get; }
        public GridArena Arena { get; }

        public Rig()
        {
            // A steel-ringed 10x3 strip: row 1 is open floor, so an eastbound shot flies and a
            // westbound one dies on the border (a miss).
            var materials = new CellMaterial[10, 3];
            for (var x = 0; x < 10; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    var border = x == 0 || x == 9 || y == 0 || y == 2;
                    materials[x, y] = border ? CellMaterial.Steel : CellMaterial.Floor;
                }
            }

            Arena = new GridArena(WallGrid.FromMaterials(materials), TileSize, Vector2.Zero);
            Combat = new CombatResolver(hitRadius: 28f);
            World = new World(Combat);
            Stats = new BattleStats(World, Combat);
        }

        public Tank AddTank(string name, Vector2 position, int team, float aim, bool fire, int maxHp = 3)
        {
            var input = new ScriptedInput(new TankInput(Vector2.Zero, aim, fire));
            var tank = new Tank(input, World, Arena, position, speed: 100f, fireInterval: 10f,
                projectileSpeed: 600f, maxHp: maxHp, team: team, displayName: name);
            World.Spawn(tank);
            return tank;
        }

        public void Run(int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                World.Step(0.05f);
            }
        }
    }

    [Fact]
    public void FiringAShot_CountsShotsFired_ForTheShooter()
    {
        var rig = new Rig();
        var shooter = rig.AddTank("Greg", new Vector2(96f, 96f), team: 0, aim: 0f, fire: true);

        rig.Run(1); // one step: the trigger pull spawns one shot (then the cooldown holds)

        Assert.Equal(1, rig.Stats.For(shooter.Id).ShotsFired);
    }

    [Fact]
    public void AShotLandingLethally_CountsHitDamageKillAndDeath()
    {
        var rig = new Rig();
        var shooter = rig.AddTank("Greg", new Vector2(96f, 96f), team: 0, aim: 0f, fire: true);
        var victim = rig.AddTank("Kevin", new Vector2(416f, 96f), team: 1, aim: 3.14f, fire: false, maxHp: 1);

        rig.Run(20); // plenty of steps for the shot to cross and resolve

        var dealt = rig.Stats.For(shooter.Id);
        Assert.Equal(1, dealt.Hits);
        Assert.Equal(1, dealt.Kills);
        Assert.True(dealt.DamageDealt > 0);
        Assert.Equal(0, dealt.Misses);

        var taken = rig.Stats.For(victim.Id);
        Assert.Equal(1, taken.Deaths);
        Assert.Equal(dealt.DamageDealt, taken.DamageTaken);
    }

    [Fact]
    public void AShotDyingOnAWall_CountsAMiss()
    {
        var rig = new Rig();
        var shooter = rig.AddTank("Greg", new Vector2(96f, 96f), team: 0, aim: 3.14159f, fire: true);

        rig.Run(20); // westbound into the steel border right behind the muzzle

        Assert.Equal(1, rig.Stats.For(shooter.Id).Misses);
        Assert.Equal(0, rig.Stats.For(shooter.Id).Hits);
    }

    [Fact]
    public void Tallies_CarryEachTanksNameAndTeam()
    {
        var rig = new Rig();
        var tank = rig.AddTank("Sir Honkalot", new Vector2(96f, 96f), team: 2, aim: 0f, fire: false);

        var tally = rig.Stats.For(tank.Id);

        Assert.Equal("Sir Honkalot", tally.Name);
        Assert.Equal(2, tally.Team);
        Assert.Contains(rig.Stats.Tallies, t => t.Name == "Sir Honkalot");
    }
}
