#!/usr/bin/env python3
"""Prepare the PixVoxel City building as the iso Building tile.

The CellMaterial.Building was a generated block with little windows; the owner asked for the proper
stylised PixVoxel building. This composites `color0/City_Large_face0_0.png` (a neutral skyscraper
cluster) onto a 128-wide canvas with its base-diamond centre placed 33 px from the bottom — the same
ground-contact anchor every iso tile uses — so it stands on its cell. CC0 source; deterministic.

Usage: python scripts/prep_iso_building.py
"""

from PIL import Image

SRC = ("C:/programmering/Assets/visual/isometric/PixVoxel_Wargame/PixVoxel_Wargame/"
       "isometric/color0/City_Large_face0_0.png")
OUT = "client/src/Presentation/Arena/IsoBuilding.png"
W = 128
BASE_FROM_BOTTOM = 33
TOP_MARGIN = 8


def base_centre(im):
    """The base diamond's centre: horizontal middle of the opaque mass, at its widest low row."""
    w, h = im.size
    px = im.load()
    rows = []
    for y in range(h):
        xs = [x for x in range(w) if px[x, y][3] > 20]
        rows.append((min(xs), max(xs)) if xs else None)
    opaque = [(y, lo, hi) for y, r in enumerate(rows) if r for lo, hi in [r]]
    minx = min(lo for _, lo, _ in opaque)
    maxx = max(hi for _, _, hi in opaque)
    widest_y = max(opaque, key=lambda t: t[2] - t[1])[0]  # the base diamond's vertical middle
    return (minx + maxx) // 2, widest_y


def main():
    b = Image.open(SRC).convert("RGBA")
    cx, cy = base_centre(b)
    canvas_h = cy + BASE_FROM_BOTTOM + TOP_MARGIN
    canvas = Image.new("RGBA", (W, canvas_h), (0, 0, 0, 0))
    canvas.alpha_composite(b, (W // 2 - cx, canvas_h - BASE_FROM_BOTTOM - cy))
    canvas.save(OUT)
    print(f"wrote {OUT} ({canvas.width}x{canvas.height}) from base centre {(cx, cy)}")


if __name__ == "__main__":
    main()
