using System;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class ProjectileTests
{
    // Never hits anything.
    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
    }

    // A wall on the +X axis at x = WallX. Records the last damage it was asked to apply.
    private sealed class WallAtX(float wallX) : IArena
    {
        public int DamageApplied { get; private set; }

        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance)
        {
            var remaining = wallX - origin.X;
            return remaining >= 0f && maxDistance >= remaining
                ? new RaycastHit(new Vector2(wallX, origin.Y), remaining)
                : null;
        }

        public void DamageAt(Vector2 point, Vector2 direction, int amount) => DamageApplied += amount;
    }

    private const float Speed = 200f;

    [Fact]
    public void Step_MovesAtSpeed_WhenNothingIsHit()
    {
        var shot = new Projectile(new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed);

        shot.Step(0.1f);

        Assert.True(shot.IsAlive);
        Assert.Equal(new Vector2(20f, 0f), shot.Position);
    }

    [Fact]
    public void Step_NormalisesDirection_SoNonUnitInputStillTravelsAtSpeed()
    {
        var shot = new Projectile(new OpenArena(), Vector2.Zero, new Vector2(5f, 0f), Speed);

        shot.Step(0.1f);

        Assert.Equal(20f, shot.Position.Length(), precision: 3);
    }

    [Fact]
    public void Step_DiesAndSnapsToHitPoint_WhenItReachesAWall()
    {
        var shot = new Projectile(new WallAtX(10f), Vector2.Zero, new Vector2(1f, 0f), Speed);

        shot.Step(0.1f); // would travel 20 units, but the wall is at 10

        Assert.False(shot.IsAlive);
        Assert.Equal(new Vector2(10f, 0f), shot.Position);
    }

    [Fact]
    public void Id_IsAssignedAndStableAcrossSteps()
    {
        var shot = new Projectile(new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed);
        var id = shot.Id;

        shot.Step(0.1f);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(id, shot.Id); // identity is stable for the entity's lifetime
    }

    [Fact]
    public void Id_IsUniquePerProjectile()
    {
        var a = new Projectile(new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed);
        var b = new Projectile(new OpenArena(), Vector2.Zero, new Vector2(1f, 0f), Speed);

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Step_AppliesDamageToTheArena_OnImpact()
    {
        var wall = new WallAtX(10f);
        var shot = new Projectile(wall, Vector2.Zero, new Vector2(1f, 0f), Speed, damage: 2);

        shot.Step(0.1f); // reaches the wall

        Assert.False(shot.IsAlive);
        Assert.Equal(2, wall.DamageApplied);
    }

    [Fact]
    public void Step_DecrementsBrickHp_WhenItHitsAWallInTheGrid()
    {
        // A 3-cell row with a brick at column 2 (world x in [20,30)); origin at world (0,0).
        var grid = WallGrid.FromMaterials(new[,]
        {
            { CellMaterial.Floor },
            { CellMaterial.Floor },
            { CellMaterial.Brick },
        });
        var arena = new GridArena(grid, tileSize: 10f, origin: Vector2.Zero);
        var shot = new Projectile(arena, new Vector2(5f, 5f), new Vector2(1f, 0f), speed: 1000f);

        shot.Step(0.1f); // travels 100 units, well past the brick's near face at x=20

        Assert.False(shot.IsAlive);
        Assert.Equal(WallGrid.DefaultBrickHp - 1, grid.GetCell(2, 0).Hp);
    }

    [Fact]
    public void Step_IsNoOp_OnceDead()
    {
        var shot = new Projectile(new WallAtX(10f), Vector2.Zero, new Vector2(1f, 0f), Speed);
        shot.Step(0.1f);
        var deadPosition = shot.Position;

        shot.Step(0.1f);

        Assert.False(shot.IsAlive);
        Assert.Equal(deadPosition, shot.Position);
    }
}
