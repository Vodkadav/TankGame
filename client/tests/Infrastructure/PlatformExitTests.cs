using TankGame.Infrastructure;
using Xunit;

namespace TankGame.Tests.Infrastructure;

// The web-vs-desktop exit decision behind PlatformExit.Run, checked without a Godot runtime.
public class PlatformExitTests
{
    [Fact]
    public void Resolve_WebExport_ReturnsToArcade()
        => Assert.Equal(PlatformExit.Destination.ReturnToArcade, PlatformExit.Resolve(isWebExport: true));

    [Fact]
    public void Resolve_Desktop_QuitsApp()
        => Assert.Equal(PlatformExit.Destination.QuitApp, PlatformExit.Resolve(isWebExport: false));
}
