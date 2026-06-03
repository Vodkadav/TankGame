namespace TankGame.GameLogic;

/// <summary>The hand-authored M2 labyrinth (28×16 tiles): a steel border with serpentine
/// brick walls (and two steel pillars that never break) forming winding corridors. Parsed by
/// <see cref="MazeDefinition.Parse"/>. <c>#</c> steel, <c>x</c> brick, <c>.</c> floor,
/// <c>@</c> tank spawn.</summary>
public static class Maze01
{
    public const string Text =
        "############################\n" +
        "#.@.x.......x.......#......#\n" +
        "#...x.......x.......#......#\n" +
        "#...x.......x.......#......#\n" +
        "#...x.......x.......#......#\n" +
        "#...x...#...x...x...#...x..#\n" +
        "#...x...#...x...x...#...x..#\n" +
        "#...x...#...x...x...#...x..#\n" +
        "#...x...#...x...x...#...x..#\n" +
        "#...x...#...x...x...#...x..#\n" +
        "#...x...#...x...x...#...x..#\n" +
        "#.......#.......x.......x..#\n" +
        "#.......#.......x.......x..#\n" +
        "#.......#.......x.......x..#\n" +
        "#.......#.......x.......x..#\n" +
        "############################";
}
