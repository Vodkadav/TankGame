using System;
using System.Numerics;
using TankGame.Domain;
using TankGame.GameLogic;
using Xunit;

namespace TankGame.Tests.GameLogic;

public class PowerupEffectsTests
{
    [Fact]
    public void Catalogue_CoversEveryPowerupKind()
    {
        var catalogue = PowerupEffects.Catalogue(Vector2.Zero, new Vector2(1920f, 1920f), tileSize: 64f);

        foreach (PowerupKind kind in Enum.GetValues<PowerupKind>())
        {
            Assert.True(catalogue.ContainsKey(kind), $"catalogue must map {kind}");
        }
    }

    [Fact]
    public void Catalogue_MapsEachKindToItsEffectShape()
    {
        var catalogue = PowerupEffects.Catalogue(Vector2.Zero, new Vector2(1920f, 1920f), tileSize: 64f);

        Assert.IsType<StatusEffectPickup>(catalogue[PowerupKind.SpeedBoost]);
        Assert.IsType<StatusEffectPickup>(catalogue[PowerupKind.RapidFire]);
        Assert.IsType<AmmoPickup>(catalogue[PowerupKind.BouncingAmmo]);
        Assert.IsType<AmmoPickup>(catalogue[PowerupKind.SpreadAmmo]);
        Assert.IsType<AmmoPickup>(catalogue[PowerupKind.PiercingAmmo]);
        Assert.IsType<AmmoPickup>(catalogue[PowerupKind.Missile]);
        Assert.IsType<RepairPickup>(catalogue[PowerupKind.Repair]);
        Assert.IsType<ShieldPickup>(catalogue[PowerupKind.Shield]);
        Assert.IsType<AirstrikePickup>(catalogue[PowerupKind.Telephone]);
    }
}
