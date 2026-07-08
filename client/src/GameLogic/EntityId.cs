using System;

namespace TankGame.GameLogic;

/// <summary>Allocates unique entity ids.
///
/// On desktop/Android this is just <see cref="Guid.NewGuid"/>. On the experimental .NET WASM web
/// runtime, however, the cryptographic RNG that backs <c>Guid.NewGuid()</c> is unavailable (the
/// editor fork's README flags crypto APIs as non-working), so every call returns the same value.
/// That collapses every entity's identity, which silently breaks combat: <c>CombatResolver</c> skips
/// a tank when <c>tank.Id == shot.Owner</c> and the AI skips a tank when <c>tank.Id == self.Id</c> —
/// with all ids equal, every shot spares every target and every enemy looks like "me", so nothing
/// takes damage and the AI never fires. A monotonic counter gives guaranteed-unique ids with no
/// crypto dependency on the web build; desktop keeps the random Guid (netcode relies on it).</summary>
public static class EntityId
{
#if GODOT_WEB
    private static long _counter;
#endif

    public static Guid Next()
    {
#if GODOT_WEB
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(bytes, System.Threading.Interlocked.Increment(ref _counter));
        return new Guid(bytes);
#else
        return Guid.NewGuid();
#endif
    }
}
