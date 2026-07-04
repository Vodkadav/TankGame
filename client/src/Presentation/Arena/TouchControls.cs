using Godot;
using TankGame.Infrastructure;
using NVector2 = System.Numerics.Vector2;

namespace TankGame.Presentation;

/// <summary>The on-screen twin-stick overlay for phones (owner ask 2026-06-30): the game ships as a
/// native Android APK, so without on-screen controls a touch device has no way to drive or aim. The
/// left half of the screen is the drive stick, the right half the aim+fire stick; each is a "floating"
/// thumbstick that springs up wherever the thumb lands and follows it. The tank reads the outputs
/// through <see cref="TouchInput3DSource"/>, which auto-fires while the aim stick is held.
///
/// <para>It draws itself but never blocks the UI: the root is mouse-filter-ignore and touches are
/// handled in <see cref="_UnhandledInput"/>, which only runs for touches the GUI (pause / replay
/// buttons) did not already consume — so those buttons keep working over the sticks.</para></summary>
public partial class TouchControls : Control
{
    private const float Radius = 120f;     // how far the thumb can push from the stick's base
    private const float KnobRadius = 52f;
    private const float BaseRingWidth = 6f;

    private static readonly Color BaseColour = new(1f, 1f, 1f, 0.22f);
    private static readonly Color KnobColour = new(1f, 1f, 1f, 0.45f);
    private static readonly Color HintColour = new(1f, 1f, 1f, 0.10f);

    private sealed class Stick
    {
        public bool Active;
        public int Finger = -1;
        public Vector2 Base;
        public Vector2 Knob;
    }

    private readonly Stick _move = new();
    private readonly Stick _aim = new();

    /// <summary>The drive stick's output (screen-space, magnitude 0..1; zero when idle).</summary>
    public NVector2 MoveOutput => OutputOf(_move);

    /// <summary>The aim+fire stick's output (screen-space, magnitude 0..1; zero when idle).</summary>
    public NVector2 AimOutput => OutputOf(_aim);

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore; // never steal a tap meant for a button
    }

    private static NVector2 OutputOf(Stick stick) => stick.Active
        ? TouchInput3DSource.StickOutput(new NVector2(stick.Knob.X, stick.Knob.Y),
            new NVector2(stick.Base.X, stick.Base.Y), Radius)
        : NVector2.Zero;

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventScreenTouch touch when touch.Pressed:
                Claim(touch.Position, touch.Index);
                break;
            case InputEventScreenTouch touch:
                Release(touch.Index);
                break;
            case InputEventScreenDrag drag:
                Drag(drag.Position, drag.Index);
                break;
        }
    }

    // A new touch grabs the stick for the half of the screen it landed in, if that stick is free.
    private void Claim(Vector2 position, int finger)
    {
        var stick = position.X < Size.X * 0.5f ? _move : _aim;
        if (stick.Active)
        {
            return; // that thumb is already down; ignore a second finger in the same half
        }

        stick.Active = true;
        stick.Finger = finger;
        stick.Base = position;
        stick.Knob = position;
        QueueRedraw();
    }

    private void Drag(Vector2 position, int finger)
    {
        var stick = FingerStick(finger);
        if (stick is null)
        {
            return;
        }

        stick.Knob = position;
        QueueRedraw();
    }

    private void Release(int finger)
    {
        var stick = FingerStick(finger);
        if (stick is null)
        {
            return;
        }

        stick.Active = false;
        stick.Finger = -1;
        QueueRedraw();
    }

    private Stick? FingerStick(int finger)
    {
        if (_move.Active && _move.Finger == finger) return _move;
        if (_aim.Active && _aim.Finger == finger) return _aim;
        return null;
    }

    public override void _Draw()
    {
        // Faint resting hints so a first-time player knows where to put each thumb.
        var y = Size.Y * 0.72f;
        if (!_move.Active) DrawArc(new Vector2(Size.X * 0.18f, y), Radius, 0f, Mathf.Tau, 48, HintColour, 4f, true);
        if (!_aim.Active) DrawArc(new Vector2(Size.X * 0.82f, y), Radius, 0f, Mathf.Tau, 48, HintColour, 4f, true);

        DrawStick(_move);
        DrawStick(_aim);
    }

    private void DrawStick(Stick stick)
    {
        if (!stick.Active)
        {
            return;
        }

        var offset = stick.Knob - stick.Base;
        if (offset.Length() > Radius)
        {
            offset = offset.Normalized() * Radius;
        }

        DrawArc(stick.Base, Radius, 0f, Mathf.Tau, 48, BaseColour, BaseRingWidth, true);
        DrawCircle(stick.Base + offset, KnobRadius, KnobColour);
    }
}
