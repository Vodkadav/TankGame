namespace TankGame.Domain;

/// <summary>An incoming airstrike called in by a telephone pickup — a world <see cref="IEntity"/> that
/// sits at its target for a brief telegraph, then detonates, damaging enemy tanks within
/// <see cref="Radius"/>. The entity surface (Id/Position/IsAlive/Step) plus the blast radius is the
/// whole contract; the Presentation layer draws the warning marker from it.</summary>
public interface IAirstrike : IEntity
{
    /// <summary>World-space blast radius of the strike.</summary>
    float Radius { get; }
}
