using TankGame.Domain;
using Xunit;

namespace TankGame.Tests.Domain;

public class CellMaterialsTests
{
    [Theory]
    [InlineData(CellMaterial.Floor, false, false)]
    [InlineData(CellMaterial.Bridge, false, false)] // a bridge is passable to both
    [InlineData(CellMaterial.Water, true, false)]   // water stops tanks but shots fly over it
    [InlineData(CellMaterial.Brick, true, true)]
    [InlineData(CellMaterial.Steel, true, true)]
    [InlineData(CellMaterial.Crate, true, true)]
    [InlineData(CellMaterial.Lava, false, false)] // lava is drivable-onto (but lethal) and shots fly over
    public void Material_HasTheRightBlockingRules(CellMaterial material, bool blocksMovement, bool blocksShots)
    {
        Assert.Equal(blocksMovement, CellMaterials.BlocksMovement(material));
        Assert.Equal(blocksShots, CellMaterials.BlocksShots(material));
    }

    [Theory]
    [InlineData(CellMaterial.Lava, true)]
    [InlineData(CellMaterial.Floor, false)]
    [InlineData(CellMaterial.Water, false)]
    [InlineData(CellMaterial.Bridge, false)]
    public void OnlyLava_IsLethal(CellMaterial material, bool lethal)
    {
        Assert.Equal(lethal, CellMaterials.IsLethal(material));
    }
}
