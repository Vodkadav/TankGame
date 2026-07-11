using System;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class DifficultyTests
{
    private const float JitterBoundRadians = 4f * MathF.PI / 180f;

    private sealed class StubTank : ITank
    {
        public StubTank(Vector2 position, int team)
        {
            Position = position;
            Team = team;
            Id = Guid.NewGuid();
        }

        public Guid Id { get; }
        public Vector2 Position { get; }
        public float Rotation => 0f;
        public float TurretRotation => 0f;
        public int Team { get; }
        public int Hp => 1;
        public int MaxHp => 1;
        public int Shield => 0;
        public bool IsAlive => true;
        public void TakeDamage(int amount) { }
        public void Step(float deltaSeconds) { }
    }

    private sealed class OpenArena : IArena
    {
        public RaycastHit? RaycastFirstHit(Vector2 origin, Vector2 direction, float maxDistance) => null;
        public void DamageAt(Vector2 point, Vector2 direction, int amount) { }
        public bool IsBlocked(Vector2 point) => false;
    }

    // A fixed temperament (Vision 700) so vision-scaling assertions are deterministic.
    private static readonly AiPersonality FixedPersonality =
        new(Aggression: 1f, Greed: 1f, Caution: 1f, Curiosity: 1f, Wanderlust: 1f, Vision: 700f, Standoff: 150f);

    private static AiInputSource AiAt(Vector2 enemyPosition, Difficulty difficulty)
    {
        var world = new World();
        var self = new StubTank(Vector2.Zero, team: 1);
        world.Spawn(self);
        world.Spawn(new StubTank(enemyPosition, team: 0));
        var ai = new AiInputSource(world, new OpenArena(), personality: FixedPersonality, seed: 7, difficulty: difficulty);
        ai.Bind(self);
        return ai;
    }

    [Fact]
    public void NormalPreset_IsTodaysTuning_Unchanged()
    {
        var preset = DifficultyPreset.For(Difficulty.Normal);

        Assert.Equal(1f, preset.VisionScale);
        Assert.Equal(1f, preset.FireIntervalScale);
        Assert.Equal(0f, preset.AimJitterDegrees);
    }

    [Fact]
    public void EasyPreset_ShortensVision_SlowsFire_AddsJitter()
    {
        var preset = DifficultyPreset.For(Difficulty.Easy);

        Assert.Equal(0.6f, preset.VisionScale);
        Assert.Equal(1.5f, preset.FireIntervalScale);
        Assert.Equal(4f, preset.AimJitterDegrees);
    }

    [Fact]
    public void HardPreset_ExtendsVision_QuickensFire_NoJitter()
    {
        var preset = DifficultyPreset.For(Difficulty.Hard);

        Assert.Equal(1.3f, preset.VisionScale);
        Assert.Equal(0.85f, preset.FireIntervalScale);
        Assert.Equal(0f, preset.AimJitterDegrees);
    }

    [Fact]
    public void EasyAi_CannotSeeAnEnemy_ThatNormalEngages()
    {
        // Enemy at 500 on +X: inside Normal's 700 vision (engage, aim exactly 0), outside
        // Easy's scaled 420 vision (blind — it wanders on a seeded random heading instead).
        var enemy = new Vector2(500f, 0f);

        var normalIntent = AiAt(enemy, Difficulty.Normal).Read();
        var easyIntent = AiAt(enemy, Difficulty.Easy).Read();

        Assert.Equal(0f, normalIntent.Aim, precision: 3);
        Assert.NotEqual(0f, easyIntent.Aim, precision: 3);
        Assert.False(easyIntent.Fire);
    }

    [Fact]
    public void HardAi_SeesAnEnemy_BeyondNormalVision()
    {
        // Enemy at 800: beyond Normal's 700 vision (wander) but inside Hard's scaled 910 (engage).
        var enemy = new Vector2(800f, 0f);

        var normalIntent = AiAt(enemy, Difficulty.Normal).Read();
        var hardIntent = AiAt(enemy, Difficulty.Hard).Read();

        Assert.NotEqual(0f, normalIntent.Aim, precision: 3);
        Assert.Equal(0f, hardIntent.Aim, precision: 3);
    }

    [Fact]
    public void EasyAi_JittersItsAim_WhenFiring()
    {
        // Enemy at 300 on +X is in fire range and inside Easy's 420 vision: it fires, but the
        // shot carries a seeded aim error within ±4 degrees rather than a perfect 0 bearing.
        var intent = AiAt(new Vector2(300f, 0f), Difficulty.Easy).Read();

        Assert.True(intent.Fire);
        Assert.NotEqual(0f, intent.Aim, precision: 4);
        Assert.True(MathF.Abs(intent.Aim) <= JitterBoundRadians, $"jitter {intent.Aim} exceeds ±4°");
    }

    [Fact]
    public void NormalAndHardAi_AimTrue_WhenFiring()
    {
        var normalIntent = AiAt(new Vector2(300f, 0f), Difficulty.Normal).Read();
        var hardIntent = AiAt(new Vector2(300f, 0f), Difficulty.Hard).Read();

        Assert.True(normalIntent.Fire);
        Assert.True(hardIntent.Fire);
        Assert.Equal(0f, normalIntent.Aim);
        Assert.Equal(0f, hardIntent.Aim);
    }
}
