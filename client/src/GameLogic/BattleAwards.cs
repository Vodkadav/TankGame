using System.Collections.Generic;
using System.Linq;

namespace TankGame.GameLogic;

/// <summary>The end-of-match honours (owner feedback 2026-06-11), shown as tags beside the
/// relevant tanks on the victory screen.</summary>
public enum AwardKind
{
    /// <summary>Most kills; damage dealt breaks ties. Withheld when nobody killed anything.</summary>
    MostDeadly,

    /// <summary>Fewest deaths; least damage taken breaks ties.</summary>
    MostEvasive,

    /// <summary>Best accuracy (hits per shot) among tanks with a real sample (≥ 3 shots).</summary>
    Sharpshooter,

    /// <summary>Most damage soaked. Withheld when nobody was hit at all.</summary>
    BulletSponge,
}

/// <summary>Computes the award tags from the match's <see cref="BattleStats"/> tallies. Pure and
/// deterministic: every rule is max/min with an explicit tie-break, and remaining ties go to the
/// earlier tally (spawn order). One tank can sweep several awards.</summary>
public static class BattleAwards
{
    /// <summary>Shots a tank must have fired before its accuracy counts for Sharpshooter — a
    /// single lucky pellet is not marksmanship.</summary>
    public const int SharpshooterMinShots = 3;

    /// <summary>The banner's headliner (victory screen v2): the winning team's top killer, damage
    /// dealt breaking ties and spawn order after that. Null on a draw — nobody is victorious.</summary>
    public static BattleStats.TankTally? Champion(
        IReadOnlyList<BattleStats.TankTally> tallies, int? winningTeam)
    {
        if (winningTeam is not int team)
        {
            return null;
        }

        return tallies
            .Where(t => t.Team == team)
            .OrderByDescending(t => t.Kills)
            .ThenByDescending(t => t.DamageDealt)
            .FirstOrDefault();
    }

    public static IReadOnlyList<(AwardKind Kind, BattleStats.TankTally Winner)> Compute(
        IReadOnlyList<BattleStats.TankTally> tallies)
    {
        var awards = new List<(AwardKind, BattleStats.TankTally)>();
        if (tallies.Count == 0)
        {
            return awards;
        }

        var deadly = tallies.OrderByDescending(t => t.Kills).ThenByDescending(t => t.DamageDealt).First();
        if (deadly.Kills > 0)
        {
            awards.Add((AwardKind.MostDeadly, deadly));
        }

        awards.Add((AwardKind.MostEvasive,
            tallies.OrderBy(t => t.Deaths).ThenBy(t => t.DamageTaken).First()));

        var marksmen = tallies.Where(t => t.ShotsFired >= SharpshooterMinShots).ToList();
        if (marksmen.Count > 0)
        {
            awards.Add((AwardKind.Sharpshooter, marksmen
                .OrderByDescending(t => (float)t.Hits / t.ShotsFired)
                .ThenByDescending(t => t.Hits)
                .First()));
        }

        var sponge = tallies.OrderByDescending(t => t.DamageTaken).First();
        if (sponge.DamageTaken > 0)
        {
            awards.Add((AwardKind.BulletSponge, sponge));
        }

        return awards;
    }
}
