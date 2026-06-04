namespace TankGame.Domain.Net;

/// <summary>The client's view of network time — the basis for snapshot interpolation and for
/// converting between server ticks and seconds. The implementation advances <see cref="Now"/>
/// monotonically (decoupled from frame time so a stutter cannot rewind interpolation);
/// <see cref="TickRateHz"/> is the server's fixed simulation rate (20 Hz for M3).</summary>
public interface INetClock
{
    /// <summary>Monotonic seconds since the transport connected.</summary>
    double Now { get; }

    /// <summary>Server simulation rate in ticks per second.</summary>
    int TickRateHz { get; }
}
