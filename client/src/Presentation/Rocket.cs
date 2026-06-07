using Godot;

namespace TankGame.Presentation;

/// <summary>Builds a small procedural rocket/missile (nose cone + body + tail fins) pointing along +Z,
/// used for both the Missile pickup emblem and the missile projectile (ADR-0017) — the asset library has
/// no single-piece rocket model, so this is assembled from primitives and tinted.</summary>
public static class Rocket
{
    public static Node3D Build(Color colour, float length)
    {
        var u = length / 26f; // parts below are authored ~26 units long, then scaled to `length`
        var bodyMat = new StandardMaterial3D { AlbedoColor = colour, Roughness = 0.6f };
        var noseMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.3f, 0.2f), Roughness = 0.6f };
        var finMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.2f, 0.24f), Roughness = 0.8f };

        var root = new Node3D { Name = "Rocket" };
        // Cylinders' long axis is Y; the X=90 rotation lays them (and the cone) along +Z.
        root.AddChild(Cyl(4f * u, 4f * u, 16f * u, new Vector3(0f, 0f, 0f), bodyMat));      // body
        root.AddChild(Cyl(0f, 4f * u, 8f * u, new Vector3(0f, 0f, 12f * u), noseMat));      // nose cone (+Z)
        for (var i = 0; i < 3; i++)
        {
            var fin = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.2f * u, 7f * u, 6f * u) },
                Position = new Vector3(0f, 0f, -8f * u),
                RotationDegrees = new Vector3(0f, 0f, i * 120f),
                MaterialOverride = finMat,
            };
            root.AddChild(fin);
        }

        return root;
    }

    private static MeshInstance3D Cyl(float top, float bottom, float height, Vector3 position, Material material) => new()
    {
        Mesh = new CylinderMesh { TopRadius = top, BottomRadius = bottom, Height = height },
        Position = position,
        RotationDegrees = new Vector3(90f, 0f, 0f),
        MaterialOverride = material,
    };
}
