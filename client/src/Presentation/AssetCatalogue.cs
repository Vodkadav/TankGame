namespace TankGame.Presentation;

/// <summary>The single source of truth for every sprite resource path. Views load their textures from
/// <see cref="Active"/> in code — nothing hardcodes a <c>res://</c> path in a scene or a view — so
/// swapping an asset is a one-line change here, never a hunt through scenes and code. Swap one asset
/// with <c>AssetCatalogue.Active = AssetCatalogue.Default with { TankBody = "res://…" }</c>, or a whole
/// set by pointing <see cref="Active"/> at another catalogue (e.g. the imported Kenney CC0 pack).</summary>
public sealed record AssetCatalogue(string TankBody, string TankTurret, string Bullet, string WallAtlas)
{
    /// <summary>The current placeholder set (programmer-generated PIL art); to be repointed at the
    /// imported Kenney CC0 sprites once they land, asset by asset or all at once.</summary>
    public static readonly AssetCatalogue Default = new(
        TankBody: "res://src/Presentation/Tank/TankBody.png",
        TankTurret: "res://src/Presentation/Tank/TankTurret.png",
        Bullet: "res://src/Presentation/Projectile/Bullet.png",
        WallAtlas: "res://src/Presentation/Arena/Walls.png");

    /// <summary>The active set every view loads from. Defaults to <see cref="Default"/>.</summary>
    public static AssetCatalogue Active { get; set; } = Default;
}
