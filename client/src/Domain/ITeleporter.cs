using System.Numerics;

namespace TankGame.Domain;

/// <summary>One teleport pad: a spot on the field, on a given elevation <paramref name="Layer"/>, that
/// warps a tank to its linked partner. Pure data — the Presentation layer draws the glowing ring.</summary>
/// <param name="Position">World-space centre of the pad.</param>
/// <param name="Layer">The elevation layer the pad sits on (ADR-0018); only a tank on this layer can
/// use it. 0 is the ground layer.</param>
public readonly record struct TeleportPad(Vector2 Position, int Layer);

/// <summary>The teleport pads of a level. A tank consults it each step (like it consults
/// <see cref="ITerrain"/> for speed): driving onto a ready pad warps the tank to the pad it is linked
/// to. After a warp both ends go on a brief cooldown, so the tank arrives on a dormant pad and does not
/// ping-pong straight back. Pure query for the tank — cooldowns are advanced separately by the owner.</summary>
public interface ITeleporter
{
    /// <summary>If <paramref name="centre"/> (a tank on <paramref name="layer"/>) sits on a ready pad,
    /// reports the linked pad's <paramref name="destination"/> and <paramref name="destinationLayer"/>,
    /// puts both ends on cooldown, and returns true. Otherwise returns false and the tank stays put.</summary>
    bool TryTeleport(Vector2 centre, int layer, out Vector2 destination, out int destinationLayer);
}
