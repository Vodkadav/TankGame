using System.Globalization;
using Godot;
using TankGame.GameLogic;

namespace TankGame.Presentation;

/// <summary>A screen-space HUD that shows the per-team kill score. Subscribes to a
/// <see cref="ScoreBoard"/> and re-renders the localized <c>hud.score</c> string (EN/ES/DK)
/// as "Score {team0} - {team1}" whenever a kill lands. A pure mirror — it holds no game
/// rules, only reads the tally.</summary>
public partial class ScoreOverlay : CanvasLayer
{
    public const string Key = "hud.score";

    // Player team is 0, the adversary team is 1 across every mode (1P/co-op vs AI, versus P1/P2).
    private const int LeftTeam = 0;
    private const int RightTeam = 1;

    private Label _label = null!;
    private ScoreBoard? _board;

    public override void _Ready()
    {
        _label = new Label { Name = "ScoreCounter" };
        _label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _label.HorizontalAlignment = HorizontalAlignment.Right;
        _label.GrowHorizontal = Control.GrowDirection.Begin;
        _label.Position = new Vector2(-Hud.Margin, Hud.LineY(0)); // top-right row 0
        Hud.Style(_label);
        AddChild(_label);
        Refresh();
    }

    /// <summary>Tracks a score board and shows its running tally.</summary>
    public void Bind(ScoreBoard board)
    {
        _board = board;
        board.Changed += Refresh;
        Refresh();
    }

    /// <summary>Re-renders the score in the current locale. Public so a locale test can force
    /// a re-render after switching languages.</summary>
    public void Refresh() =>
        _label.Text = string.Format(
            CultureInfo.InvariantCulture,
            TranslationServer.Translate(Key),
            _board?.KillsFor(LeftTeam) ?? 0,
            _board?.KillsFor(RightTeam) ?? 0);
}
