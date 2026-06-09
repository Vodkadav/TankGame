using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>Maps the game's flat 2D plane to the 3D world (the 3D analogue of the retired
/// <c>IsoProjection</c>, ADR-0017). Domain/GameLogic positions are <c>System.Numerics.Vector2</c> on a
/// flat field; the 3D presentation lays that field on the ground (XZ) plane, so game <c>(x, y)</c>
/// becomes world <c>(x, 0, y)</c>. One place owns the mapping so the 3D views, camera and input agree.</summary>
public static class GroundProjection
{
    /// <summary>World units one elevation layer rises (ADR-0018). A plateau cell, and any entity on it,
    /// sits this much higher per layer than the valley floor; the terrain, tank and projectile views all
    /// offset by it so the height reads in 3D. Flat (layer-0) maps are unaffected. ~0.6 of a 64 cell.</summary>
    public const float LayerHeight = 38f;

    /// <summary>World position of a flat game point, optionally lifted to <paramref name="height"/>.</summary>
    public static Vector3 ToWorld(NVector2 flat, float height = 0f) => new(flat.X, height, flat.Y);

    /// <summary>World position of a flat game point on elevation <paramref name="layer"/> (ADR-0018),
    /// lifted by the layer's height plus an optional <paramref name="height"/>. Layer 0 ⇒ the flat
    /// behaviour, so existing callers are unchanged.</summary>
    public static Vector3 ToWorld(NVector2 flat, int layer, float height = 0f) =>
        new(flat.X, height + (layer * LayerHeight), flat.Y);
}
