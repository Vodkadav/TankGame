using Godot;

namespace TankGame.Presentation;

/// <summary>The editor's selection gizmo (owner follow-up 2026-06-11): three circular axis controls
/// around a selected prop — drag the green ring to turn it (yaw), the red ring to tilt it forward
/// (pitch), the blue ring to tip it sideways (roll), the familiar 3D-editor colour language. The
/// rings render depth-test-off so the gizmo stays grabbable over terrain from any camera angle. The
/// scene owns the drag math; this node only draws the rings and answers "which ring is under this
/// screen point" via <see cref="RingAt"/>.</summary>
public partial class RotationGizmo3D : Node3D
{
    /// <summary>A ring's rotation axis, by world basis: 0 = X (pitch), 1 = Y (yaw), 2 = Z (roll).</summary>
    public const int AxisX = 0;
    public const int AxisY = 1;
    public const int AxisZ = 2;

    private const float Radius = 56f;
    private const float Thickness = 2.6f;
    private const float GrabTolerance = 12f; // world units around the ring line that count as a hit

    private static readonly Color[] AxisColours =
    {
        new(0.92f, 0.25f, 0.25f), // X — red
        new(0.30f, 0.85f, 0.30f), // Y — green
        new(0.30f, 0.55f, 0.95f), // Z — blue
    };

    public override void _Ready()
    {
        for (var axis = 0; axis < 3; axis++)
        {
            var ring = new MeshInstance3D
            {
                Name = $"Ring{axis}",
                Mesh = new TorusMesh
                {
                    InnerRadius = Radius - Thickness,
                    OuterRadius = Radius,
                    Rings = 48,
                    RingSegments = 8,
                },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = AxisColours[axis],
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    NoDepthTest = true,
                },
            };

            // A torus lies flat (its hole along Y) — reorient so each ring circles its own axis.
            ring.RotationDegrees = axis switch
            {
                AxisX => new Vector3(0f, 0f, 90f),
                AxisZ => new Vector3(90f, 0f, 0f),
                _ => Vector3.Zero,
            };
            AddChild(ring);
        }
    }

    /// <summary>The axis of the ring under <paramref name="screen"/>, or null when the point misses
    /// every ring. Each ring is tested as its circle in the axis plane: intersect the camera ray with
    /// the plane, then ask how far the hit sits from the ring line.</summary>
    public int? RingAt(Camera3D camera, Vector2 screen)
    {
        int? best = null;
        var bestMiss = float.MaxValue;
        for (var axis = 0; axis < 3; axis++)
        {
            if (PlaneHit(camera, screen, AxisVector(axis)) is not { } hit)
            {
                continue;
            }

            var miss = Mathf.Abs((hit - GlobalPosition).Length() - Radius);
            if (miss < GrabTolerance && miss < bestMiss)
            {
                best = axis;
                bestMiss = miss;
            }
        }

        return best;
    }

    /// <summary>The angle (radians) of the cursor around <paramref name="axis"/>, measured in the
    /// ring's plane — the scene differences two of these to turn a drag into degrees.</summary>
    public float? AngleAround(Camera3D camera, Vector2 screen, int axis)
    {
        if (PlaneHit(camera, screen, AxisVector(axis)) is not { } hit)
        {
            return null;
        }

        var normal = AxisVector(axis);
        var u = normal.Cross(Mathf.Abs(normal.Y) > 0.9f ? Vector3.Right : Vector3.Up).Normalized();
        var w = normal.Cross(u);
        var v = hit - GlobalPosition;
        return Mathf.Atan2(v.Dot(w), v.Dot(u));
    }

    private Vector3? PlaneHit(Camera3D camera, Vector2 screen, Vector3 normal)
    {
        var origin = camera.ProjectRayOrigin(screen);
        var direction = camera.ProjectRayNormal(screen);
        var denominator = direction.Dot(normal);
        if (Mathf.Abs(denominator) < 1e-5f)
        {
            return null; // the ray skims the ring's plane edge-on
        }

        var t = (GlobalPosition - origin).Dot(normal) / denominator;
        return t < 0f ? null : origin + (direction * t);
    }

    private static Vector3 AxisVector(int axis) => axis switch
    {
        AxisX => Vector3.Right,
        AxisY => Vector3.Up,
        _ => Vector3.Back,
    };
}
