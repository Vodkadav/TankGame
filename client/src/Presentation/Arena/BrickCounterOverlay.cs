using System.Globalization;
using Godot;
using TankGame.Domain;

namespace TankGame.Presentation;

/// <summary>A screen-space dev overlay that counts destroyed brick walls. Subscribes to an
/// <see cref="IWallGrid"/> and increments when a cell breaks to floor, rendering the count
/// through the localized <c>m2.bricks_destroyed</c> string (EN/ES/DK). A pure mirror — it
/// holds no game rules.</summary>
public partial class BrickCounterOverlay : CanvasLayer
{
    public const string Key = "m2.bricks_destroyed";

    private Label _label = null!;
    private int _destroyed;

    /// <summary>Bricks destroyed so far — exposed for tests.</summary>
    public int Destroyed => _destroyed;

    public override void _Ready()
    {
        _label = new Label { Name = "BrickCounter" };
        _label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        _label.Position = new Vector2(Hud.Margin, Hud.LineY(1)); // top-left row 1, below the meters
        Hud.Style(_label);
        AddChild(_label);
        Refresh();
    }

    /// <summary>Tracks the grid and shows its destroyed-brick count.</summary>
    public void Bind(IWallGrid grid)
    {
        grid.CellChanged += OnCellChanged;
        Refresh();
    }

    /// <summary>Re-renders the count in the current locale. Public so a locale test can force
    /// a re-render after switching languages.</summary>
    public void Refresh() =>
        _label.Text = string.Format(CultureInfo.InvariantCulture, TranslationServer.Translate(Key), _destroyed);

    private void OnCellChanged(WallCellChanged change)
    {
        if (change.Cell.Material != CellMaterial.Floor)
        {
            return; // a chip (still brick), not a destruction
        }

        _destroyed++;
        Refresh();
    }
}
