using System.Linq;
using TankGame.Infrastructure;
using Xunit;

namespace TankGame.Tests.Infrastructure;

// The asset browser's catalogue logic (owner ask 2026-06-11), tested over fake path lists — no
// file system. Only AssetLibrary.Scan touches the disk, and it degrades to an empty catalogue
// when the library root is absent (CI, other machines).
public class AssetLibraryTests
{
    private const string Root = @"C:\library";

    [Fact]
    public void Catalogue_BuildsPackSlashModelIds_AndGroupsByCategory()
    {
        var entries = AssetLibrary.Catalogue(new[]
        {
            @"C:\library\kenney_nature-kit\Models\GLB format\tree_oak.glb",
            @"C:\library\kenney_city-kit-suburban\building-a.glb",
        }, Root);

        var tree = entries.Single(e => e.Id == "kenney_nature-kit/tree_oak");
        Assert.Equal("Nature", tree.Category);
        Assert.Equal("tree oak", tree.DisplayName);

        var building = entries.Single(e => e.Id == "kenney_city-kit-suburban/building-a");
        Assert.Equal("Buildings", building.Category);
    }

    [Fact]
    public void Catalogue_DropsExcludedPacks_AndDuplicateIds()
    {
        var entries = AssetLibrary.Catalogue(new[]
        {
            @"C:\library\attackchopper\chopper.glb",          // unusable license — excluded
            @"C:\library\Crates Asset Package\crate.glb",     // no license — excluded
            @"C:\library\kenney_pirate-kit\ship.glb",
            @"C:\library\kenney_pirate-kit\sub\ship.glb",     // same id — first sighting wins
        }, Root);

        var entry = Assert.Single(entries);
        Assert.Equal("kenney_pirate-kit/ship", entry.Id);
    }

    [Fact]
    public void Search_MatchesNameIdAndCategory_CaseInsensitively()
    {
        var catalogue = AssetLibrary.Catalogue(new[]
        {
            @"C:\library\kenney_nature-kit\tree_oak.glb",
            @"C:\library\kenney_blaster-kit\blaster-heavy.glb",
        }, Root);

        Assert.Single(AssetLibrary.Search(catalogue, "OAK"));
        Assert.Single(AssetLibrary.Search(catalogue, "weapons"));          // category hit
        Assert.Equal(2, AssetLibrary.Search(catalogue, "  ").Count);       // blank = everything
        Assert.Empty(AssetLibrary.Search(catalogue, "spaceship"));
    }

    [Fact]
    public void Scan_OnAMachineWithoutTheLibrary_IsEmptyNotAnError()
    {
        var library = new AssetLibrary(@"C:\definitely\not\a\real\folder");

        Assert.Empty(library.Scan());
    }
}
