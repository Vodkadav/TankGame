using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A piercing shot: travels straight, but on meeting a destructible (brick) wall it
/// damages it and punches through while it has pierce budget left, advancing past the struck cell;
/// a steel wall (or any permanent obstacle), or running out of budget, stops it like a straight
/// shot. The pierce budget is shared with the tank-combat pass via <see cref="ProjectileState.Pierce"/>
/// — so "pierce one target, the next stops it" holds whether the targets are walls or tanks. Needs
/// the tile size to step past a pierced cell (the arena exposes only world-space queries).</summary>
public sealed class PiercingBehaviour : IProjectileBehaviour
{
    private const float Nudge = 0.01f;
    private readonly float _tileSize;

    public PiercingBehaviour(float tileSize) => _tileSize = tileSize;

    public void Step(ProjectileState state, IArena arena, float deltaSeconds)
    {
        var remaining = state.Speed * deltaSeconds;

        while (remaining > 0f)
        {
            if (arena.RaycastFirstHit(state.Position, state.Direction, remaining, state.Layer) is not { } hit)
            {
                state.Position += state.Direction * remaining;
                return;
            }

            arena.DamageAt(hit.Point, state.Direction, state.Damage, state.Layer); // chips brick; a no-op on steel

            if (!hit.Destructible || state.Pierce <= 0)
            {
                state.Position = hit.Point; // steel, or no budget left → stop on impact
                state.IsAlive = false;
                return;
            }

            // Punch through this one cell: spend a pierce and step to just past the struck cell so
            // the next iteration tests what lies beyond it (an adjacent wall there stops the shot,
            // since the budget for this cell is now spent).
            state.Pierce--;
            var advance = _tileSize + Nudge;
            state.Position = hit.Point + (state.Direction * advance);
            remaining -= hit.Distance + advance;
        }
    }
}
