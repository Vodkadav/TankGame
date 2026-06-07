using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>Maps the game's flat 2D plane to the 3D world (the 3D analogue of the retired
/// <c>IsoProjection</c>, ADR-0017). Domain/GameLogic positions are <c>System.Numerics.Vector2</c> on a
/// flat field; the 3D presentation lays that field on the ground (XZ) plane, so game <c>(x, y)</c>
/// becomes world <c>(x, 0, y)</c>. One place owns the mapping so the 3D views, camera and input agree.</summary>
public static class GroundProjection
{
    /// <summary>World position of a flat game point, optionally lifted to <paramref name="height"/>.</summary>
    public static Vector3 ToWorld(NVector2 flat, float height = 0f) => new(flat.X, height, flat.Y);
}
