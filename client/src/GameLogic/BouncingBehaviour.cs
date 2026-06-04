using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A ricochet shot: travels straight, but on meeting a wall it reflects off the hit
/// face's <see cref="RaycastHit.Normal"/> and keeps going, up to <c>bounces</c> times; the bounce
/// after the last reflects no more — it lands and damages the wall like a straight shot. Bounces
/// within a single tick are resolved in a loop so a fast shot can ricochet more than once per
/// step. Per-shot bounce budget lives here, so each shot gets its own instance.</summary>
public sealed class BouncingBehaviour : IProjectileBehaviour
{
    private const float Nudge = 0.01f; // step off the wall after a bounce so we don't re-hit at distance 0
    private int _bouncesLeft;

    public BouncingBehaviour(int bounces) => _bouncesLeft = bounces;

    public void Step(ProjectileState state, IArena arena, float deltaSeconds)
    {
        var remaining = state.Speed * deltaSeconds;

        while (remaining > 0f)
        {
            if (arena.RaycastFirstHit(state.Position, state.Direction, remaining) is not { } hit)
            {
                state.Position += state.Direction * remaining;
                return;
            }

            if (_bouncesLeft <= 0)
            {
                state.Position = hit.Point;
                arena.DamageAt(hit.Point, state.Direction, state.Damage);
                state.IsAlive = false;
                return;
            }

            // Reflect: v' = v - 2(v·n)n, then nudge off the surface to make progress.
            state.Position = hit.Point;
            state.Direction -= 2f * Vector2.Dot(state.Direction, hit.Normal) * hit.Normal;
            _bouncesLeft--;
            remaining -= hit.Distance + Nudge;
            state.Position += state.Direction * Nudge;
        }
    }
}
