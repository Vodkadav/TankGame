using Godot;

namespace TankGame.Presentation;

/// <summary>A screen-space banner for the networked match's connection state (M3-T9): "Connecting…"
/// until the server welcomes us, "Connected" once it has, and "Player 2 joined" when the opponent
/// first appears. The label's text is a translation key, so Godot renders it in the active locale
/// (EN/ES/DK). Pure view — <see cref="NetArenaScene"/> calls <see cref="SetStatus"/>.</summary>
public partial class NetStatusOverlay : CanvasLayer
{
    public const string ConnectingKey = "net.connecting";
    public const string ConnectedKey = "net.connected";
    public const string Player2JoinedKey = "net.player2_joined";
    public const string RoundOverKey = "net.round_over";

    private Label _label = null!;

    public override void _Ready()
    {
        _label = new Label { Name = "NetStatus", Text = ConnectingKey };
        _label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.GrowHorizontal = Control.GrowDirection.Both;
        _label.Position = new Vector2(0f, 8f);
        AddChild(_label);
    }

    /// <summary>Shows <paramref name="key"/> (a translation key) as the current status.</summary>
    public void SetStatus(string key) => _label.Text = key;
}
