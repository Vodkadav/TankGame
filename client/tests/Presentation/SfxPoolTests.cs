using System;
using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class SfxPoolTests : TestClass
{
    private SfxPool _pool = default!;

    public SfxPoolTests(Node testScene) : base(testScene) { }

    [Setup]
    public void Setup()
    {
        _pool = new SfxPool();
        TestScene.AddChild(_pool);
    }

    [Cleanup]
    public void Cleanup() => _pool.QueueFree();

    // Every kind maps to a real OGG: a null stream here means a missing or undecodable file
    // (e.g. a WAV smuggled in under an .ogg name — LoadFromBuffer would return null).
    [Test]
    public void EveryKind_ResolvesALoadedStream()
    {
        foreach (var kind in Enum.GetValues<SfxKind>())
            if (!_pool.HasStream(kind))
                throw new Exception($"SfxKind.{kind} must resolve a loaded audio stream.");
    }

    [Test]
    public void PlayAt_WithEveryKind_DoesNotThrow()
    {
        foreach (var kind in Enum.GetValues<SfxKind>())
            _pool.PlayAt(kind, Vector3.Zero);
    }

    // The pool must stay robust to a kind with no mapping: PlayAt no-ops instead of throwing.
    [Test]
    public void PlayAt_WithAnUnmappedKind_DoesNotThrow()
    {
        _pool.PlayAt((SfxKind)999, Vector3.Zero);
    }
}
