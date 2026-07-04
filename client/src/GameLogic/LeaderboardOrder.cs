using System;
using System.Collections.Generic;
using System.Linq;

namespace TankGame.GameLogic;

/// <summary>Orders leaderboard rows by a metric. Most metrics rank best-first when they're highest
/// (kills, damage dealt, repairs, assists); a few are better when LOWER — deaths and damage taken —
/// and rank ascending, so the tank that died the fewest times or took the least damage sits on top.</summary>
public static class LeaderboardOrder
{
    public static IEnumerable<T> Rank<T>(IEnumerable<T> items, Func<T, int> value, bool lowerIsBetter)
        => lowerIsBetter ? items.OrderBy(value) : items.OrderByDescending(value);
}
