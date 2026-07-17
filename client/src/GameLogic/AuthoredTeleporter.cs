using System.Collections.Generic;
using System.Numerics;
using TankGame.Domain;

namespace TankGame.GameLogic;

/// <summary>Builds the <see cref="Teleporter"/> for a map's authored pad links (a custom map's, or
/// Cliffs' cross-layer pair): each cell becomes a world-space pad on whatever elevation layer the
/// grid gives that cell — the layer is derived, never authored separately, so pad data stays plain
/// cells and cannot disagree with the map. Shared by the local and networked arenas: pads are static
/// map features every peer derives identically from the same resolved map, so nothing about them
/// travels on the wire. The returned pad list is in link order (each link's A then B), matching
/// <see cref="Teleporter.PadStatuses"/>, so views built in this order mirror state by index.</summary>
public static class AuthoredTeleporter
{
    public static (Teleporter Teleporter, IReadOnlyList<TeleportPad> Pads) Build(
        IReadOnlyList<TeleportPadLink> links, IWallGrid grid, float tileSize, Vector2 origin, float padRadius)
    {
        var pairs = new List<(TeleportPad, TeleportPad)>(links.Count);
        var pads = new List<TeleportPad>(links.Count * 2);
        foreach (var link in links)
        {
            var a = PadAt(link.AX, link.AY, grid, tileSize, origin);
            var b = PadAt(link.BX, link.BY, grid, tileSize, origin);
            pairs.Add((a, b));
            pads.Add(a);
            pads.Add(b);
        }

        return (new Teleporter(pairs, padRadius), pads);
    }

    private static TeleportPad PadAt(int x, int y, IWallGrid grid, float tileSize, Vector2 origin) => new(
        new Vector2(origin.X + ((x + 0.5f) * tileSize), origin.Y + ((y + 0.5f) * tileSize)),
        grid.LayerAt(x, y));
}
