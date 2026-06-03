namespace TankGame.GameLogic;

/// <summary>The hand-authored battlefield (28×16 tiles): an open arena ringed by an
/// unbreakable steel border, with cover "strewn about" — small brick clusters (destructible)
/// and a few steel chunks (permanent) — rather than a maze of corridors. The interior is
/// mostly open floor so tanks circle, flank, and trade shots across the field. Parsed by
/// <see cref="LevelMap.Parse"/>. <c>#</c> steel, <c>x</c> brick, <c>.</c> floor,
/// <c>@</c> player spawn.</summary>
public static class Battlefield01
{
    public const string Text =
        "############################\n" +
        "#.@........................#\n" +
        "#......xx...........#......#\n" +
        "#......xx...........#......#\n" +
        "#.........x................#\n" +
        "#......................x...#\n" +
        "#............##............#\n" +
        "#...x........##............#\n" +
        "#...x......................#\n" +
        "#...x.............xx.......#\n" +
        "#.................x........#\n" +
        "#...............x..........#\n" +
        "#.....##.............x.....#\n" +
        "#..........x...............#\n" +
        "#.........xx...............#\n" +
        "############################";
}
