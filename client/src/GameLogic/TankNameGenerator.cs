using System;
using System.Collections.Generic;

namespace TankGame.GameLogic;

/// <summary>Deals out battle names for AI tanks — the requested house style is derpy-meets-edgelord,
/// so a lobby reads like a public server circa 2009. Deterministic for a seed (the arena seed, so a
/// best-of-N series keeps its cast) and unique until the pool runs dry, after which the deck simply
/// reshuffles — an 8-tank match can never starve it. Pure C#, no Godot.</summary>
public sealed class TankNameGenerator
{
    private static readonly string[] Pool =
    {
        "Sir Honkalot",
        "xX_Sh4d0w_Xx",
        "DOOMSPROCKET",
        "Kevin",
        "Lord Vexarion",
        "Captain Wobbles",
        "N1GHTSL4YER",
        "Baron von Boom",
        "Greg the Unready",
        "TURBO_DOOM",
        "Wheelie McTreadface",
        "Soggy Biscuit",
        "xXNoScopeXx",
        "Private Panic",
        "Crouton",
        "Mr. Snuggles",
        "Diesel Dave",
        "The Crundler",
        "EDGELORD SUPREME",
        "Tiny Vengeance",
        "Beefcake",
        "Sgt. Oopsie",
        "VoidReaper2007",
        "Hamsterball",
    };

    /// <summary>How many names the deck holds before it must reshuffle.</summary>
    public static int PoolSize => Pool.Length;

    private readonly List<string> _deck;
    private readonly Random _rng;

    public TankNameGenerator(int seed)
    {
        _rng = new Random(seed);
        _deck = new List<string>(Pool);
    }

    /// <summary>The next name: drawn at random from the deck so it cannot repeat until every name
    /// has been dealt once.</summary>
    public string Next()
    {
        if (_deck.Count == 0)
        {
            _deck.AddRange(Pool);
        }

        var index = _rng.Next(_deck.Count);
        var name = _deck[index];
        _deck.RemoveAt(index);
        return name;
    }
}
