using System.Numerics;

namespace TankGame.Domain;

/// <summary>Where the battlefield hides things. A tank standing on a concealing cell (a bush
/// today; smoke later) is invisible to distant viewers — only spotted from close up — so cover
/// like this enables hiding and ambush. Pure query; concealment never blocks movement or
/// projectiles, it only governs who can be <em>seen</em>. The multiplayer-fair counterpart of
/// this lives server-side (see the fog-of-war proposal); locally it is a render/AI concern.</summary>
public interface IConcealment
{
    /// <summary>Whether <paramref name="point"/> lies on a concealing cell.</summary>
    bool ConcealsAt(Vector2 point);
}
