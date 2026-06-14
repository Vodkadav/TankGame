using System.Collections.Generic;
using Godot;

namespace TankGame.Presentation;

/// <summary>Auto-fits an imported model to a target size so kit assets of unknown native scale drop into
/// the game without hand-tuned scale constants (ADR-0017). Measures the model's combined bounding box and
/// scales it so its horizontal footprint spans <c>targetSpan</c> world units, then positions it either
/// seated on the ground (terrain) or centred on the origin (floating emblems). The model must already be
/// in the tree at the origin so its mesh transforms are valid.</summary>
public static class ModelFit
{
    /// <summary>Scale <paramref name="model"/> to <paramref name="targetSpan"/> and place it; seats its
    /// base on the ground when <paramref name="seatOnGround"/>, else centres it on the origin.</summary>
    public static void Apply(Node3D model, float targetSpan, bool seatOnGround)
    {
        var box = Measure(model);
        var span = Mathf.Max(box.Size.X, box.Size.Z);
        var scale = span > 1e-4f ? targetSpan / span : 1f;
        model.Scale = new Vector3(scale, scale, scale);

        var centre = box.GetCenter();
        var y = seatOnGround ? -box.Position.Y * scale : -centre.Y * scale;
        model.Position = new Vector3(-centre.X * scale, y, -centre.Z * scale);
    }

    /// <summary>Like <see cref="Apply"/> but fills a rectangular footprint, scaling X and Z independently
    /// to <paramref name="spanX"/> × <paramref name="spanZ"/> (height scales with their average) — for a
    /// model that should fill a multi-cell region such as a building over a block of cells.</summary>
    public static void ApplyBox(Node3D model, float spanX, float spanZ, bool seatOnGround)
    {
        var box = Measure(model);
        var sx = box.Size.X > 1e-4f ? spanX / box.Size.X : 1f;
        var sz = box.Size.Z > 1e-4f ? spanZ / box.Size.Z : 1f;
        var sy = (sx + sz) / 2f;
        model.Scale = new Vector3(sx, sy, sz);

        var centre = box.GetCenter();
        var y = seatOnGround ? -box.Position.Y * sy : -centre.Y * sy;
        model.Position = new Vector3(-centre.X * sx, y, -centre.Z * sz);
    }

    /// <summary>Overrides every surface of <paramref name="model"/> with one flat colour — used to give
    /// kit models a suitable colour when their original (external Kenney colormap) texture is not shipped
    /// with the bare .glb, so they would otherwise render white.</summary>
    public static void Tint(Node3D model, Color albedo)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = albedo,
            Roughness = 1f,
            SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
        };
        foreach (var mi in MeshInstances(model))
        {
            mi.MaterialOverride = material;
        }
    }

    /// <summary>Multi-colour tint for kit models (owner feedback 2026-06-11): the LARGEST part (by
    /// bounding-box surface area, so flat panels still rank) takes <paramref name="primary"/> and
    /// every smaller part cycles the <paramref name="secondaries"/> as detail colours, starting at an
    /// offset from <paramref name="seed"/> so different assets in one category vary while any one
    /// asset always looks the same. Only BARE surfaces are touched — a surface whose material
    /// carries an albedo texture keeps it (so mixed models keep their real textures); detection is
    /// per material, not file size, because a big geometry-only .glb is still textureless. Ranking
    /// covers every surface, so the primary/detail split is stable whether or not parts are bare.</summary>
    public static void TintPalette(Node3D model, Color primary, IReadOnlyList<Color> secondaries, int seed = 0)
    {
        var surfaces = new List<(MeshInstance3D Mi, int Surface, float Size)>();
        foreach (var mi in MeshInstances(model))
        {
            var size = mi.GetAabb().Size;
            var area = (size.X * size.Y) + (size.Y * size.Z) + (size.X * size.Z);
            for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                surfaces.Add((mi, s, area));
            }
        }

        var ranked = new List<(MeshInstance3D Mi, int Surface, float Size)>(surfaces);
        ranked.Sort((a, b) => b.Size.CompareTo(a.Size));

        var offset = System.Math.Abs(seed);
        for (var i = 0; i < ranked.Count; i++)
        {
            if (HasAlbedoTexture(ranked[i].Mi, ranked[i].Surface))
            {
                continue; // genuinely textured — never paint over the real artwork
            }

            var colour = i == 0 || secondaries.Count == 0
                ? primary
                : secondaries[(i - 1 + offset) % secondaries.Count];
            ranked[i].Mi.SetSurfaceOverrideMaterial(ranked[i].Surface, new StandardMaterial3D
            {
                AlbedoColor = colour,
                Roughness = 1f,
                SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled,
            });
        }
    }

    private static bool HasAlbedoTexture(MeshInstance3D mi, int surface) =>
        mi.GetActiveMaterial(surface) is BaseMaterial3D material && material.AlbedoTexture is not null;

    // Union of the model's MeshInstance3D bounding boxes in the MODEL's own local space — each mesh's
    // global transform is taken relative to the model root, so the measurement is independent of where
    // the model sits in the world (it may be parented at a cell's world position). The model must be in
    // the tree for the transforms to be valid.
    private static Aabb Measure(Node3D model)
    {
        var toModel = model.GlobalTransform.AffineInverse();
        Aabb? box = null;
        foreach (var mi in MeshInstances(model))
        {
            var local = TransformAabb(toModel * mi.GlobalTransform, mi.GetAabb());
            box = box is null ? local : box.Value.Merge(local);
        }

        return box ?? new Aabb(Vector3.Zero, Vector3.One);
    }

    private static Aabb TransformAabb(Transform3D t, Aabb a)
    {
        var result = new Aabb(t * a.Position, Vector3.Zero);
        for (var i = 1; i < 8; i++)
        {
            result = result.Expand(t * a.GetEndpoint(i));
        }

        return result;
    }

    private static IEnumerable<MeshInstance3D> MeshInstances(Node node)
    {
        if (node is MeshInstance3D mi && mi.Mesh is not null)
        {
            yield return mi;
        }

        foreach (var child in node.GetChildren())
        {
            foreach (var found in MeshInstances(child))
            {
                yield return found;
            }
        }
    }
}
