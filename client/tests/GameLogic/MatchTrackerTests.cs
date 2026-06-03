using System;
using System.Collections.Generic;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class MatchTrackerTests
{
    private sealed class StubTank : ITank
    {
        public StubTank(int team, bool alive) { Team = team; IsAlive = alive; Id = Guid.NewGuid(); }
        public Guid Id { get; }
        public Vector2 Position => Vector2.Zero;
        public float Rotation => 0f;
        public float TurretRotation => 0f;
        public int Team { get; }
        public int Hp => IsAlive ? 1 : 0;
        public int MaxHp => 1;
        public bool IsAlive { get; }
        public void TakeDamage(int amount) { }
        public void Step(float deltaSeconds) { }
    }

    private static MatchResult Evaluate(params IEntity[] entities) =>
        new MatchTracker().Evaluate(new List<IEntity>(entities));

    [Fact]
    public void TwoTeamsAlive_IsUndecided()
    {
        var result = Evaluate(new StubTank(team: 0, alive: true), new StubTank(team: 1, alive: true));

        Assert.False(result.Decided);
    }

    [Fact]
    public void OneTeamLeft_DecidesThatTeamTheWinner()
    {
        var result = Evaluate(
            new StubTank(team: 0, alive: true),
            new StubTank(team: 1, alive: false)); // the enemy is dead

        Assert.True(result.Decided);
        Assert.Equal(0, result.WinningTeam);
    }

    [Fact]
    public void NoTeamLeft_IsADecidedDraw()
    {
        var result = Evaluate(new StubTank(team: 0, alive: false), new StubTank(team: 1, alive: false));

        Assert.True(result.Decided);
        Assert.Null(result.WinningTeam);
    }

    [Fact]
    public void MultipleLiveTanksOnTheSameTeam_CountAsOneTeam()
    {
        var result = Evaluate(
            new StubTank(team: 1, alive: true),
            new StubTank(team: 1, alive: true)); // two enemies, no player left

        Assert.True(result.Decided);
        Assert.Equal(1, result.WinningTeam);
    }
}
