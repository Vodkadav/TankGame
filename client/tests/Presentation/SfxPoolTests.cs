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

    // WallBreak is mapped to "" (barrier-break SFX removed): playing it must be a silent no-op —
    // no stream, no crash. The test passes iff PlayAt returns without throwing.
    [Test]
    public void PlayAt_WithASilentPlaceholderKind_DoesNotThrow()
    {
        _pool.PlayAt(SfxKind.WallBreak, Vector3.Zero);
    }
}
