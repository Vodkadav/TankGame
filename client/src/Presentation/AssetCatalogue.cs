namespace TankGame.Presentation;

/// <summary>The single source of truth for every sprite resource path. Views load their textures from
/// <see cref="Active"/> in code — nothing hardcodes a <c>res://</c> path in a scene or a view — so
/// swapping an asset is a one-line change here, never a hunt through scenes and code. Swap one asset
/// with <c>AssetCatalogue.Active = AssetCatalogue.Default with { TankBody = "res://…" }</c>, or a whole
/// set by pointing <see cref="Active"/> at another catalogue (e.g. the imported Kenney CC0 pack).</summary>
public sealed record AssetCatalogue(string TankBody, string TankTurret, string Bullet, string WallAtlas,
    string GroundTile)
{
    /// <summary>The active sprite set. The tank hull + turret are the imported Kenney CC0 art
    /// (neutral sand, tinted per team via <c>TankView.ApplyTeamTint</c>); the bullet and wall atlas
    /// are still placeholders (the wall atlas needs a 4-frame damage layout — a follow-up). Repoint
    /// any entry to swap one asset.</summary>
    public static readonly AssetCatalogue Default = new(
        TankBody: "res://src/Presentation/Tank/KenneyTankBody.png",
        TankTurret: "res://src/Presentation/Tank/KenneyTankTurret.png",
        Bullet: "res://src/Presentation/Projectile/Bullet.png",
        WallAtlas: "res://src/Presentation/Arena/Walls.png",
        GroundTile: "res://src/Presentation/Arena/GroundSand.png");

    /// <summary>The active set every view loads from. Defaults to <see cref="Default"/>.</summary>
    public static AssetCatalogue Active { get; set; } = Default;
}
