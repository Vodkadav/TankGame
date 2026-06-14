using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

// The smart multi-colour tint (owner feedback 2026-06-11): the largest part wears the primary, the
// details cycle the secondary palette deterministically.
public class ModelFitTests : TestClass
{
    private Node3D _model = default!;

    public ModelFitTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _model = new Node3D();
        _model.AddChild(new MeshInstance3D { Name = "Body", Mesh = new BoxMesh { Size = new Vector3(4f, 4f, 4f) } });
        _model.AddChild(new MeshInstance3D { Name = "DetailA", Mesh = new BoxMesh { Size = Vector3.One } });
        _model.AddChild(new MeshInstance3D { Name = "DetailB", Mesh = new BoxMesh { Size = Vector3.One * 0.5f } });
        TestScene.AddChild(_model);
    }

    [Cleanup]
    public void Cleanup() => _model.Free();

    [Test]
    public void TintPalette_GivesTheBiggestPartThePrimary_AndCyclesDetails()
    {
        var primary = new Color(0.1f, 0.5f, 0.1f);
        var detailOne = new Color(0.5f, 0.3f, 0.1f);
        var detailTwo = new Color(0.8f, 0.8f, 0.2f);

        ModelFit.TintPalette(_model, primary, new[] { detailOne, detailTwo }, seed: 0);

        if (SurfaceColour("Body") != primary)
        {
            throw new System.Exception("The largest part must wear the primary colour.");
        }

        if (SurfaceColour("DetailA") != detailOne || SurfaceColour("DetailB") != detailTwo)
        {
            throw new System.Exception("The details must cycle the secondary palette in size order.");
        }
    }

    [Test]
    public void TintPalette_NeverPaintsOver_AGenuinelyTexturedSurface()
    {
        var textured = _model.GetNode<MeshInstance3D>("DetailA");
        var image = Image.CreateEmpty(2, 2, false, Image.Format.Rgba8);
        textured.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(image),
        });

        ModelFit.TintPalette(_model, new Color(0.1f, 0.5f, 0.1f),
            new[] { new Color(0.5f, 0.3f, 0.1f) }, seed: 0);

        var kept = textured.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
        if (kept?.AlbedoTexture is null)
        {
            throw new System.Exception("A surface with a real texture must keep it — never tint over artwork.");
        }

        if (SurfaceColour("Body") != new Color(0.1f, 0.5f, 0.1f))
        {
            throw new System.Exception("Bare surfaces around a textured one must still take the palette.");
        }
    }

    [Test]
    public void TintPalette_SeedRotatesTheDetailColours_NotThePrimary()
    {
        var primary = new Color(0.1f, 0.5f, 0.1f);
        var detailOne = new Color(0.5f, 0.3f, 0.1f);
        var detailTwo = new Color(0.8f, 0.8f, 0.2f);

        ModelFit.TintPalette(_model, primary, new[] { detailOne, detailTwo }, seed: 1);

        if (SurfaceColour("Body") != primary)
        {
            throw new System.Exception("The primary never rotates with the seed.");
        }

        if (SurfaceColour("DetailA") != detailTwo)
        {
            throw new System.Exception("The seed must offset which secondary a detail takes.");
        }
    }

    private Color SurfaceColour(string meshName)
    {
        var mi = _model.GetNode<MeshInstance3D>(meshName);
        var material = mi.GetSurfaceOverrideMaterial(0) as StandardMaterial3D
            ?? throw new System.Exception($"'{meshName}' must have a surface override after tinting.");
        return material.AlbedoColor;
    }
}
