using Godot;
using Sentry;

namespace TankGame.Infrastructure;

/// <summary>Initialises Sentry crash reporting if a client DSN is present in the
/// environment. Called once from the composition root (Bootstrap). No-op when the
/// DSN is unset, so local/dev runs send nothing.</summary>
public static class SentryBootstrap
{
    public static void Init()
    {
        var dsn = OS.GetEnvironment("SENTRY_DSN_CLIENT");
        if (string.IsNullOrEmpty(dsn))
        {
            return;
        }

        SentrySdk.Init(o =>
        {
            o.Dsn = dsn;
            o.AutoSessionTracking = false;
            o.SendDefaultPii = false;
        });
    }
}
