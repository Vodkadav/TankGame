namespace TankGame.Presentation;

/// <summary>The set of one-shot sound effects played by <see cref="SfxPool"/>. Each maps to a FLAC
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

    /// <summary>A powerup was collected by a tank.</summary>
    Pickup,

    /// <summary>The match ended — played once, non-positional.</summary>
    Victory,

    /// <summary>A menu button was pressed — non-positional.</summary>
    UiClick,

    /// <summary>Mouse entered a menu button — quieter than a full click.</summary>
    UiHover,
}
