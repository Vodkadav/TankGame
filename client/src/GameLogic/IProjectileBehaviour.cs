using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>How a shot moves and what it does on contact — the Strategy half of S2 (the shot's
/// data lives in <see cref="ProjectileState"/>). One implementation per movement/impact style:
/// <see cref="StraightBehaviour"/> today, then bouncing (reflect on the hit
/// <see cref="RaycastHit.Normal"/>), piercing, zig-zag, homing, and so on — a new ammo type is a
/// new behaviour only when the motion is genuinely new (see
/// <c>docs/adr/0013-weapon-behaviour-strategy.md</c>). Pure C#: deterministic, no Godot.</summary>
public interface IProjectileBehaviour
{
    /// <summary>Advances <paramref name="state"/> by <paramref name="deltaSeconds"/>, querying
    /// <paramref name="arena"/> for collisions and mutating the state's position/direction or
    /// expiring it.</summary>
    void Step(ProjectileState state, IArena arena, float deltaSeconds);
}
