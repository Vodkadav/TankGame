using Godot;
using Chickensoft.GoDotTest;
using TankGame.Presentation;

namespace TankGame.Tests.Presentation;

public class TeamPaletteTests : TestClass
{
    public TeamPaletteTests(Node testScene) : base(testScene) { }

    [Test]
    public void Friendly_IsUntinted()
    {
        // A friendly tank renders the sprite as-authored (white multiply = no tint).
        if (TeamPalette.TintFor(isEnemy: false) != Colors.White)
        {
            throw new System.Exception("A friendly tank should be untinted (white Modulate).");
        }
    }

    [Test]
    public void Enemy_IsTinted_AndDistinctFromFriendly()
    {
        var enemy = TeamPalette.TintFor(isEnemy: true);
        if (enemy == Colors.White || enemy == TeamPalette.TintFor(isEnemy: false))
        {
            throw new System.Exception($"An enemy tank should carry a distinct tint; was {enemy}.");
        }
    }
}
