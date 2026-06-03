namespace TankGame.GameLogic;

/// <summary>The hand-authored battlefield (28×16 tiles): an open arena ringed by an
/// unbreakable steel border, with cover "strewn about" — small brick clusters (destructible),
/// a few steel chunks (permanent), and bush patches (passable concealment to hide in) — rather
/// than a maze of corridors. The interior is mostly open floor so tanks circle, flank, and
/// trade shots across the field. Parsed by <see cref="LevelMap.Parse"/>. <c>#</c> steel,
/// <c>x</c> brick, <c>b</c> bush, <c>.</c> floor, <c>@</c> player spawn.</summary>
public static class Battlefield01
{
    public const string Text =
        "############################\n" +
        "#.@........................#\n" +
        "#......xx...........#......#\n" +
        "#......xx...........#......#\n" +
        "#...bb....x................#\n" +
        "#....................bbx...#\n" +
        "#............##............#\n" +
        "#...x........##............#\n" +
        "#...x......................#\n" +
        "#...x.......bb....xx.......#\n" +
        "#.................x........#\n" +
        "#...............x..........#\n" +
        "#.....##.bb......bb..x.....#\n" +
        "#..........x...............#\n" +
        "#.........xx...............#\n" +
        "############################";
}
