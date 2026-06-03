namespace TankGame.Domain;

/// <summary>An entity with hit points that can be damaged and destroyed. The combat pass
/// applies damage through this contract, so it works for tanks today and any future
/// damageable entity (turrets, destructible props) without the resolver knowing the concrete
/// type. Pure C# — no Godot, no networking.</summary>
public interface IDamageable
{
    /// <summary>Current hit points; 0 means destroyed.</summary>
    int Hp { get; }

    /// <summary>Hit points at full health.</summary>
    int MaxHp { get; }

    /// <summary>Reduces <see cref="Hp"/> by <paramref name="amount"/>, clamped at 0. Damage
    /// to an already-destroyed entity is a no-op.</summary>
    void TakeDamage(int amount);
}
