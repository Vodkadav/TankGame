using Godot;

namespace TankGame.Presentation;

/// <summary>The per-team multiply tint applied to a tank sprite via <c>Sprite2D.Modulate</c>, so one
/// neutral tank texture reads as either side. Friendly is white (the sprite as-authored); the enemy
/// tint reddens it so the two sides read apart. Centralised here so both the local and the net arena
/// scenes tint consistently from one source.</summary>
public static class TeamPalette
{
    private static readonly Color Enemy = new(1f, 0.5f, 0.5f);

    public static Color TintFor(bool isEnemy) => isEnemy ? Enemy : Colors.White;
}
