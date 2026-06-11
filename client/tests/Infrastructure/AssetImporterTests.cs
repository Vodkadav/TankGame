using System.IO;
using TankGame.Infrastructure;
using Xunit;

namespace TankGame.Tests.Infrastructure;

public class AssetImporterTests
{
    [Fact]
    public void EnsureImported_CopiesOnce_AndNeverOverwrites()
    {
        var root = Path.Combine(Path.GetTempPath(), "tankgame-import-" + Path.GetRandomFileName());
        var source = Path.Combine(root, "library", "kenney_pirate-kit");
        Directory.CreateDirectory(source);
        var sourceFile = Path.Combine(source, "ship.glb");
        File.WriteAllText(sourceFile, "original");
        var entry = new AssetEntry("kenney_pirate-kit/ship", "ship", "Dungeon", sourceFile);
        var importRoot = Path.Combine(root, "imported");

        try
        {
            var target = AssetImporter.EnsureImported(entry, importRoot);

            Assert.Equal(AssetImporter.TargetPath(entry.Id, importRoot), target);
            Assert.Equal("original", File.ReadAllText(target));

            File.WriteAllText(sourceFile, "changed later");
            AssetImporter.EnsureImported(entry, importRoot); // idempotent — local copy sticks
            Assert.Equal("original", File.ReadAllText(target));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
