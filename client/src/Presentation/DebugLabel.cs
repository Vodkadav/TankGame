using Godot;

namespace TankGame.Presentation;

/// <summary>A floating billboard name tag for the 3D preview, so the owner can see what each asset is and
/// confirm it renders as intended (ADR-0017 debug aid). Every tag joins the <c>debuglabels</c> group so
/// the scene can toggle them all (the "L" key). Developer tooling — English only, not localised.</summary>
public static class DebugLabel
{
    public const string Group = "debuglabels";

    public static Label3D Make(string text, float y)
    {
        var label = new Label3D
        {
            Name = "DebugLabel",
            Text = text,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = 48,
            PixelSize = 0.45f,
            OutlineSize = 10,
            Position = new Vector3(0f, y, 0f),
        };
        label.AddToGroup(Group);
        return label;
    }
}
