namespace TankGame.Domain;

/// <summary>The authored pose of a placed prop (owner follow-up 2026-06-11): free rotation on all
/// three axes plus a uniform size — what the editor's selection gizmo edits. Cosmetic only: the
/// view poses the mesh; blocking and damage never change. Pure data, no Godot.</summary>
/// <param name="YawDeg">Turn around the vertical axis, degrees.</param>
/// <param name="PitchDeg">Tilt around the world X axis, degrees.</param>
/// <param name="RollDeg">Tilt around the world Z axis, degrees.</param>
/// <param name="Scale">Uniform size multiplier (1 = as authored).</param>
public readonly record struct PropTransform(float YawDeg, float PitchDeg, float RollDeg, float Scale)
{
    /// <summary>The untouched pose: no rotation, full size.</summary>
    public static readonly PropTransform Identity = new(0f, 0f, 0f, 1f);

    /// <summary>Whether this pose changes nothing — an identity entry is dropped from the document.</summary>
    public bool IsIdentity => this == Identity;
}
