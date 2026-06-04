using System.Globalization;
using Godot;
using TankGame.GameLogic;

namespace TankGame.Presentation;

/// <summary>A screen-space HUD (top-left) that shows the per-team damage and kill/death meters.
/// Subscribes to a <see cref="MeterBoard"/> and re-renders the localized <c>hud.meters</c> string
/// (EN/ES/DK) whenever a shot lands. A pure mirror — it holds no game rules, only reads the tally.
/// Kept separate from <see cref="ScoreOverlay"/> (the round score, top-right) so each HUD has one
/// responsibility.</summary>
public partial class MetersOverlay : CanvasLayer
{
    public const string Key = "hud.meters";

    // Player team is 0, the adversary team is 1 across every mode (1P/co-op vs AI, versus P1/P2).
    private const int LeftTeam = 0;
    private const int RightTeam = 1;

    private Label _label = null!;
    private MeterBoard? _board;

    public override void _Ready()
    {
        _label = new Label { Name = "MetersReadout" };
        _label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _label.Position = new Vector2(8f, 8f);
        AddChild(_label);
        Refresh();
    }

    /// <summary>Tracks a meter board and shows its running tallies.</summary>
    public void Bind(MeterBoard board)
    {
        _board = board;
        board.Changed += Refresh;
        Refresh();
    }

    /// <summary>Re-renders the meters in the current locale. Public so a locale test can force a
    /// re-render after switching languages. Order: damage L-R, then K/D L (kills/deaths) - R.</summary>
    public void Refresh() =>
        _label.Text = string.Format(
            CultureInfo.InvariantCulture,
            TranslationServer.Translate(Key),
            _board?.DamageBy(LeftTeam) ?? 0,
            _board?.DamageBy(RightTeam) ?? 0,
            _board?.KillsBy(LeftTeam) ?? 0,
            _board?.DeathsOf(LeftTeam) ?? 0,
            _board?.KillsBy(RightTeam) ?? 0,
            _board?.DeathsOf(RightTeam) ?? 0);
}
