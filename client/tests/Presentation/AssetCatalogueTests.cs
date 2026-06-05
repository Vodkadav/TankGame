using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class AssetCatalogueTests : TestClass
{
    public AssetCatalogueTests(Node testScene) : base(testScene) { }

    [Test]
    public void Default_EveryPathLoadsAsATexture()
    {
        // The catalogue is the single source of truth for sprite paths; a typo would silently leave
        // a sprite blank, so guard that every default path actually resolves to a texture.
        foreach (var path in new[]
        {
            AssetCatalogue.Default.TankBody,
            AssetCatalogue.Default.TankTurret,
            AssetCatalogue.Default.Bullet,
            AssetCatalogue.Default.GroundTile,
            AssetCatalogue.Default.PickupDisc,
            AssetCatalogue.Default.WaterTile,
            AssetCatalogue.Default.BridgeTile,
            AssetCatalogue.Default.MountainTile,
            AssetCatalogue.Default.BrickIntactTile,
            AssetCatalogue.Default.BrickCrackedTile,
            AssetCatalogue.Default.BrickRubbleTile,
            AssetCatalogue.Default.SteelTile,
            AssetCatalogue.Default.CrateTile,
            AssetCatalogue.Default.BuildingTile,
            AssetCatalogue.Default.BushTile,
            AssetCatalogue.Default.SandbagTile,
        })
        {
            if (GD.Load<Texture2D>(path) is null)
            {
                throw new System.Exception($"Catalogue path '{path}' did not load as a Texture2D.");
            }
        }
    }

    [Test]
    public void Active_DefaultsToTheDefaultSet()
    {
        if (AssetCatalogue.Active != AssetCatalogue.Default)
        {
            throw new System.Exception("Active should default to the Default set.");
        }
    }

    [Test]
    public void Active_CanSwapASingleAsset_WithoutTouchingTheRest()
    {
        var original = AssetCatalogue.Active;
        try
        {
            AssetCatalogue.Active = original with { TankBody = "res://src/Presentation/Projectile/Bullet.png" };

            if (AssetCatalogue.Active.TankBody != "res://src/Presentation/Projectile/Bullet.png")
            {
                throw new System.Exception("Swapping one asset should change just that entry.");
            }

            if (AssetCatalogue.Active.SteelTile != original.SteelTile)
            {
                throw new System.Exception("Swapping one asset must leave the others untouched.");
            }
        }
        finally
        {
            AssetCatalogue.Active = original;
        }
    }
}
