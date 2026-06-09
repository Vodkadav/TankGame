using System.Collections.Generic;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>The teleport pads of a level as an <see cref="ITeleporter"/>: linked pad pairs a tank can
/// drive onto to warp to the partner pad. After a warp both ends go on a cooldown, so a tank arrives on
/// a dormant pad and can drive off without bouncing straight back (the key to a good feel). A pad only
/// accepts a tank on its own elevation layer, and the partner sets the destination layer — so a pair can
/// connect two layers as well as two places. Pure C#, deterministic — no Godot; the owner advances the
/// cooldowns with <see cref="Step"/> once per fixed tick.</summary>
public sealed class Teleporter : ITeleporter
{
    /// <summary>Default seconds a pad stays dormant after a warp (the owner can override) — long enough
    /// to drive clear of the destination pad before it could fire again.</summary>
    public const float DefaultCooldownSeconds = 3.5f;

    // A linked pair plus each end's remaining cooldown. A pad fires only while its own cooldown is 0; a
    // warp puts both ends on cooldown so neither end can re-fire until the tank has moved on.
    private sealed class Link
    {
        public TeleportPad A;
        public TeleportPad B;
        public float CooldownA;
        public float CooldownB;
    }

    private readonly List<Link> _links = new();
    private readonly float _radiusSquared;
    private readonly float _cooldownSeconds;

    /// <param name="links">The pad pairs; each tuple links pad <c>a</c> with pad <c>b</c>.</param>
    /// <param name="padRadius">How close a tank's centre must be to a pad to trigger it.</param>
    /// <param name="cooldownSeconds">Seconds both ends stay dormant after a warp.</param>
    public Teleporter(IEnumerable<(TeleportPad a, TeleportPad b)> links, float padRadius,
        float cooldownSeconds = DefaultCooldownSeconds)
    {
        foreach (var (a, b) in links)
        {
            _links.Add(new Link { A = a, B = b });
        }

        _radiusSquared = padRadius * padRadius;
        _cooldownSeconds = cooldownSeconds;
    }

    /// <summary>Ages every pad's cooldown by <paramref name="deltaSeconds"/>. Called once per fixed
    /// tick by the owner, independent of how many tanks consult the pads.</summary>
    public void Step(float deltaSeconds)
    {
        foreach (var link in _links)
        {
            link.CooldownA = Cool(link.CooldownA, deltaSeconds);
            link.CooldownB = Cool(link.CooldownB, deltaSeconds);
        }
    }

    public bool TryTeleport(Vector2 centre, int layer, out Vector2 destination, out int destinationLayer)
    {
        foreach (var link in _links)
        {
            if (link.CooldownA <= 0f && OnPad(centre, layer, link.A))
            {
                Fire(link, out destination, out destinationLayer, link.B);
                return true;
            }

            if (link.CooldownB <= 0f && OnPad(centre, layer, link.B))
            {
                Fire(link, out destination, out destinationLayer, link.A);
                return true;
            }
        }

        destination = default;
        destinationLayer = 0;
        return false;
    }

    private void Fire(Link link, out Vector2 destination, out int destinationLayer, TeleportPad target)
    {
        destination = target.Position;
        destinationLayer = target.Layer;
        link.CooldownA = _cooldownSeconds;
        link.CooldownB = _cooldownSeconds;
    }

    private bool OnPad(Vector2 centre, int layer, TeleportPad pad) =>
        pad.Layer == layer && Vector2.DistanceSquared(centre, pad.Position) <= _radiusSquared;

    private static float Cool(float remaining, float deltaSeconds) =>
        remaining > 0f ? System.MathF.Max(0f, remaining - deltaSeconds) : 0f;
}
