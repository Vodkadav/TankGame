using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>The default shot: travels in a straight line at constant speed and dies on the first
/// arena hit, snapping to the contact point and damaging it. This is the behaviour every shot had
/// before S2; it is stateless, so one shared <see cref="Instance"/> serves every straight
/// shot.</summary>
public sealed class StraightBehaviour : IProjectileBehaviour
{
    public static readonly StraightBehaviour Instance = new();

    private StraightBehaviour() { }

    public void Step(ProjectileState state, IArena arena, float deltaSeconds)
    {
        var distance = state.Speed * deltaSeconds;

        if (arena.RaycastFirstHit(state.Position, state.Direction, distance, state.Layer) is { } hit)
        {
            state.Position = hit.Point;
            arena.DamageAt(hit.Point, state.Direction, state.Damage, state.Layer);
            state.IsAlive = false;
            return;
        }

        state.Position += state.Direction * distance;
    }
}
