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
    public void Material_HasTheRightBlockingRules(CellMaterial material, bool blocksMovement, bool blocksShots)
    {
        Assert.Equal(blocksMovement, CellMaterials.BlocksMovement(material));
        Assert.Equal(blocksShots, CellMaterials.BlocksShots(material));
    }
}
