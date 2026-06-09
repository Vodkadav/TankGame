using System;
using System.IO;
using System.Linq;
using TankGame.GameLogic;
using TankGame.Infrastructure;
using Xunit;

namespace TankGame.Tests.Infrastructure;

public sealed class MapRepositoryTests : IDisposable
{
    private readonly string _dir;
    private readonly MapRepository _repo;

    public MapRepositoryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "tankmaps-" + Guid.NewGuid().ToString("N"));
        _repo = new MapRepository(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void List_OnAMissingDirectory_IsEmpty()
    {
        Assert.Empty(_repo.List());
    }

    [Fact]
    public void Save_ThenList_ShowsTheStoredMapByName()
    {
        _repo.Save(MapDefinition.CreateBlank("My Arena", 6, 5));

        var stored = _repo.List();

        Assert.Single(stored);
        Assert.Equal("My Arena", stored[0].Name);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsTheDocument()
    {
        var id = _repo.Save(SampleArena.Build());

        var restored = _repo.Load(id);

        Assert.Equal("Sample Skirmish", restored.Name);
        Assert.Equal(SampleArena.Build().Width, restored.Width);
        Assert.NotEmpty(restored.EnemySpawns);
    }

    [Fact]
    public void Save_OverwritesAMapWithTheSameName()
    {
        _repo.Save(SampleArena.Build());
        _repo.Save(SampleArena.Build());

        Assert.Single(_repo.List());
    }
}
