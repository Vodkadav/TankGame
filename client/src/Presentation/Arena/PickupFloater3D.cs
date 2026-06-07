using Godot;

namespace TankGame.Presentation;

/// <summary>A short-lived billboard label that pops up in the 3D world where a pickup was collected and
/// names it, so the player learns what each powerup does (ADR-0017, the 3D replacement for
/// <c>PickupFloater</c>). It rises and fades, then frees itself. The text is the kind's translation key,
/// auto-translated by Godot.</summary>
public partial class PickupFloater3D : Node3D
{
    private const float LifeSeconds = 1.2f;
    private const float RiseSpeed = 34f;

    private Label3D _label = null!;
    private float _elapsed;

    /// <summary>Places the floater at <paramref name="worldPosition"/> and labels it with the key.</summary>
    public void Show(Vector3 worldPosition, string textKey)
    {
        Position = worldPosition;
        _label = new Label3D
        {
            Name = "Text",
            Text = TranslationServer.Translate(textKey),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = 64,
            PixelSize = 0.5f,
            Modulate = Colors.White,
            OutlineSize = 12,
        };
        AddChild(_label);
    }

    public override void _Process(double delta) => Advance((float)delta);

    public void Advance(float delta)
    {
        _elapsed += delta;
        Position += new Vector3(0f, RiseSpeed * delta, 0f);
        _label.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(1f - (_elapsed / LifeSeconds), 0f, 1f));

        if (_elapsed >= LifeSeconds)
        {
            QueueFree();
        }
    }
}
