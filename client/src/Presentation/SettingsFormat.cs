using System.Globalization;

namespace TankGame.Presentation;

// The Settings overlay's volume and brightness readouts MUST format numbers with InvariantCulture.
// The arcade ships on Godot's .NET-to-WASM runtime, which carries no ICU data; an un-qualified
// $"{x:F2}" resolves CultureInfo.CurrentCulture and throws there, aborting SettingsOverlay.Build()
// before it can show — the "tap Settings, nothing happens" web bug (owner report 2026-07-04). Every
// other number readout in Presentation already passes InvariantCulture; this keeps that one place
// tested and unmissable.
public static class SettingsFormat
{
    public static string Brightness(float multiplier) =>
        $"{multiplier.ToString("F2", CultureInfo.InvariantCulture)}×";

    public static string Db(int db) =>
        $"{db.ToString(CultureInfo.InvariantCulture)} dB";
}
