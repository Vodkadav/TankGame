using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>How a trigger-pull becomes shots in the world — the firing strategy. A weapon owns
/// the *count* and *kind* of projectile(s) a shot spawns: one straight shot (default), one
/// special-behaviour shot (<see cref="BehaviourWeapon"/>), or several fanned pellets
/// (<see cref="SpreadWeapon"/>). The tank supplies the muzzle/aim/speed/team; the weapon decides
/// what to spawn. Pure C#, no Godot. See <c>docs/adr/0013-weapon-behaviour-strategy.md</c>.</summary>
public interface IWeapon
{
    /// <summary>Spawns this weapon's shot(s) into <paramref name="world"/> from
    /// <paramref name="muzzle"/> along <paramref name="direction"/>.</summary>
    void Fire(IWorld world, IArena arena, Vector2 muzzle, Vector2 direction, float speed, int team);
}
