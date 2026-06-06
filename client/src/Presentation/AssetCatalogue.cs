namespace TankGame.Presentation;

/// <summary>The single source of truth for every sprite resource path. Views load their textures from
/// <see cref="Active"/> in code — nothing hardcodes a <c>res://</c> path in a scene or a view — so
/// swapping an asset is a one-line change here, never a hunt through scenes and code. Swap one asset
/// with <c>AssetCatalogue.Active = AssetCatalogue.Default with { TankHull = "res://…" }</c>, or a whole
/// set by pointing <see cref="Active"/> at another catalogue (e.g. the imported Kenney CC0 pack).</summary>
public sealed record AssetCatalogue(
    string TankHull, string TankTurret, int TankFacings, string Bullet,
    string GroundTile, string PickupDisc,
    string WaterTile, string BridgeTile, string MountainTile,
    string BrickIntactTile, string BrickCrackedTile, string BrickRubbleTile,
    string SteelTile, string CrateTile, string BuildingTile,
    string BushTile, string SandbagTile)
{
    /// <summary>The active sprite set. The tank is the iso Super_Tank (CC BY 4.0, Zsky_01) rendered to
    /// directional sprite strips — a hull strip and an independently-aiming turret strip of
    /// <c>TankFacings</c> frames each (neutral tan, tinted per team via <c>TankView.ApplyTeamTint</c>);
    /// the ground/terrain are native PixVoxel iso tiles; the walls are generated iso blocks (placeholder,
    /// raised — see <c>scripts/gen_iso_blocks.py</c>). Repoint any entry to swap one asset.</summary>
    public static readonly AssetCatalogue Default = new(
        TankHull: "res://src/Presentation/Tank/IsoTankHull.png",
        TankTurret: "res://src/Presentation/Tank/IsoTankTurret.png",
        TankFacings: 16,
        Bullet: "res://src/Presentation/Projectile/KenneyBullet.png",
        GroundTile: "res://src/Presentation/Arena/IsoGroundSeamless.png",
        PickupDisc: "res://src/Presentation/Arena/PickupDisc.png",
        WaterTile: "res://src/Presentation/Arena/IsoWater.png",
        BridgeTile: "res://src/Presentation/Arena/IsoBridge.png",
        MountainTile: "res://src/Presentation/Arena/IsoMountainStacked.png",
        BrickIntactTile: "res://src/Presentation/Arena/IsoBrick0.png",
        BrickCrackedTile: "res://src/Presentation/Arena/IsoBrick1.png",
        BrickRubbleTile: "res://src/Presentation/Arena/IsoBrick2.png",
        SteelTile: "res://src/Presentation/Arena/IsoSteel.png",
        CrateTile: "res://src/Presentation/Arena/IsoCrate.png",
        BuildingTile: "res://src/Presentation/Arena/IsoBuilding.png",
        BushTile: "res://src/Presentation/Arena/IsoBush.png",
        SandbagTile: "res://src/Presentation/Arena/IsoSandbags.png");

    /// <summary>The active set every view loads from. Defaults to <see cref="Default"/>.</summary>
    public static AssetCatalogue Active { get; set; } = Default;
}
