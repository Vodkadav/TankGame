using System;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>A view-model pickup for networked play: a mutable <see cref="IPowerup"/> whose state is
/// pushed in from a snapshot's <c>PowerupState</c> so the existing <c>Powerup3DView</c> can render a
/// guest's mirrored crates unchanged. The host owns collection, so this holds no rules — <see
/// cref="Step"/> is a no-op and <see cref="IsAlive"/> is always true (the scene adds/removes mirrors
/// by diffing snapshot ids, not by reaping).</summary>
public sealed class NetPowerup : IPowerup
{
    public Guid Id { get; } = Guid.NewGuid();
    public Vector2 Position { get; set; }
    public PowerupKind Kind { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsAlive => true;

    // Never raised — collection resolves on the host; the scene pops its floater from the snapshot diff.
    public event Action<PowerupKind>? Collected { add { } remove { } }

    public void Step(float deltaSeconds) { }
}
