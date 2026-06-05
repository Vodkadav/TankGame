using Godot;
using Chickensoft.GoDotTest;
using TankGame.Domain;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class PickupFloaterTests : TestClass
{
    public PickupFloaterTests(Node testScene) : base(testScene) { }

    [Test]
    public void EveryKind_HasADistinctNonEmptyLabelKey()
    {
        var keys = new System.Collections.Generic.HashSet<string>();
        foreach (PowerupKind kind in System.Enum.GetValues(typeof(PowerupKind)))
        {
            var key = PickupFloater.LabelKeyFor(kind);
            if (string.IsNullOrEmpty(key) || !keys.Add(key))
            {
                throw new System.Exception($"Kind {kind} needs its own non-empty label key; got '{key}'.");
            }
        }
    }

    [Test]
    public void Show_PlacesTheNode_AndLabelsItWithTheKindsKey()
    {
        var floater = new PickupFloater();
        TestScene.AddChild(floater);
        try
        {
            floater.Show(new Vector2(120f, 80f), PickupFloater.LabelKeyFor(PowerupKind.RapidFire));

            if (Mathf.Abs(floater.Position.X - 120f) > 0.01f || Mathf.Abs(floater.Position.Y - 80f) > 0.01f)
            {
                throw new System.Exception($"Floater should sit at the pickup spot; was {floater.Position}.");
            }

            var label = floater.GetNodeOrNull<Label>("Text")
                ?? throw new System.Exception("PickupFloater must build a 'Text' Label.");
            if (label.Text != PickupFloater.LabelKeyFor(PowerupKind.RapidFire))
            {
                throw new System.Exception($"Label should carry the kind's translation key; was '{label.Text}'.");
            }
        }
        finally
        {
            floater.QueueFree();
        }
    }

    [Test]
    public void Advance_RisesAndFades_ThenFreesItselfAfterItsLifetime()
    {
        var floater = new PickupFloater();
        TestScene.AddChild(floater);
        floater.Show(new Vector2(0f, 0f), PickupFloater.LabelKeyFor(PowerupKind.Repair));

        floater.Advance(0.2f);
        if (floater.Position.Y >= 0f)
        {
            throw new System.Exception($"Floater should rise (negative Y) over time; was {floater.Position.Y}.");
        }
        if (floater.Modulate.A >= 1f)
        {
            throw new System.Exception($"Floater should fade as it rises; alpha was {floater.Modulate.A}.");
        }

        floater.Advance(5f); // well past its lifetime
        if (GodotObject.IsInstanceValid(floater) && !floater.IsQueuedForDeletion())
        {
            throw new System.Exception("Floater should free itself once its lifetime elapses.");
        }
    }
}
