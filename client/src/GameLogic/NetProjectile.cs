using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A view-model projectile for networked play (ADR-0019 step 4): a mutable
/// <see cref="IProjectile"/> whose transform is pushed in from a snapshot's <c>ProjectileState</c> so
/// the existing <c>Projectile3DView</c> can render a guest's shots unchanged. The host owns the
/// simulation, so this holds no rules — <see cref="Step"/> is a no-op and <see cref="IsAlive"/> is
/// always true (the scene shows or hides shots by rebuilding from each snapshot, not by reaping).</summary>
public sealed class NetProjectile : IProjectile
{
    public Guid Id { get; } = EntityId.Next();
    public Vector2 Position { get; set; }
    public Vector2 Direction { get; set; }
    public int Team { get; set; }
    public ProjectileStyle Style { get; set; }
    public int Layer { get; set; }

    public bool IsAlive => true;

    public void Step(float deltaSeconds) { }
}
