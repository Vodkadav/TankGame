namespace TankGame.Presentation;

/// <summary>The set of one-shot sound effects played by <see cref="SfxPool"/>. Each maps to an OGG
/// file under <c>res://audio/sfx/</c>. Files that do not yet exist are silently skipped —
/// the pool degrades gracefully so the game runs without any audio assets installed.</summary>
public enum SfxKind
{
    /// <summary>A projectile was fired.</summary>
    Fire,

    /// <summary>A tank was destroyed (death explosion).</summary>
    Explosion,

    /// <summary>A destructible wall or crate reached 0 HP and crumbled.</summary>
    WallBreak,

    /// <summary>A powerup was collected — generic fallback when a kind has no dedicated clip.</summary>
    Pickup,

    /// <summary>The match ended — played once, non-positional.</summary>
    Victory,

    /// <summary>A menu button was pressed — non-positional.</summary>
    UiClick,

    /// <summary>Mouse entered a menu button — quieter than a full click.</summary>
    UiHover,

    // ── Per-powerup pickup cues (one per PowerupKind) ───────────────────────────────────────────
    /// <summary>Speed Boost collected — engine rev.</summary>
    PowerupSpeed,

    /// <summary>Rapid Fire collected — machine-gun rattle.</summary>
    PowerupRapidFire,

    /// <summary>Bouncing Ammo collected — ricochet ping.</summary>
    PowerupBouncing,

    /// <summary>Spread Shot collected — shotgun pump.</summary>
    PowerupSpread,

    /// <summary>Piercing Ammo collected — armour-piercing zing.</summary>
    PowerupPiercing,

    /// <summary>Repair collected — restorative chime.</summary>
    PowerupRepair,

    /// <summary>Shield collected — energy hum.</summary>
    PowerupShield,

    /// <summary>Missile collected — rocket whoosh.</summary>
    PowerupMissile,

    /// <summary>Airstrike (Telephone) collected — incoming jet + siren.</summary>
    PowerupAirstrike,

    // ── Announcer voice lines (non-positional; played on a dedicated voice channel) ──────────────
    /// <summary>The local player destroyed an enemy — "Enemy destroyed".</summary>
    KillEnemy,

    /// <summary>Second kill inside the streak window — "Double kill".</summary>
    StreakDouble,

    /// <summary>Third kill inside the streak window — "Triple kill".</summary>
    StreakTriple,

    /// <summary>Four or more kills inside the streak window — "Multi kill".</summary>
    StreakMulti,
}
