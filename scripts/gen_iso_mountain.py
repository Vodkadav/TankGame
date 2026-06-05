#!/usr/bin/env python3
"""Build a TALL isometric mountain by stacking the PixVoxel mountain tile in tapering layers.

The single PixVoxel `IsoMountain.png` block is only ~15 px taller than a flat tile, so it reads as
a bump rather than a mountain. This composites several copies of it, each higher one smaller and
shifted up, into one sprite — "all the layers stacked on top of each other" — so the cell renders
as a proper raised iso mountain. Deterministic; reads a committed source and writes a committed PNG.

The base (largest) layer stays bottom-aligned exactly where the flat tiles place their diamond, so
IsoTerrainView's vertical anchor (33 - height/2) keeps the mountain's foot on its cell.

Usage: python scripts/gen_iso_mountain.py
"""

from PIL import Image

SRC = "client/src/Presentation/Arena/IsoMountain.png"        # one PixVoxel mountain block (128x90)
OUT = "client/src/Presentation/Arena/IsoMountainStacked.png"

# Each tier: (scale, lift in px above the base). Bottom tier is full size at the base; higher tiers
# shrink and rise so the silhouette steps up to a tall peak — ~3-4 wall-blocks tall, not a bump.
TIERS = [(1.00, 0), (0.84, 30), (0.69, 58), (0.55, 84), (0.42, 108), (0.30, 128), (0.19, 146)]


def main():
    base = Image.open(SRC).convert("RGBA")
    bw, bh = base.size
    top_lift = max(lift for _, lift in TIERS)
    canvas = Image.new("RGBA", (bw, bh + top_lift), (0, 0, 0, 0))

    # Paint largest/lowest first so higher tiers overlap on top.
    for scale, lift in TIERS:
        w, h = max(1, round(bw * scale)), max(1, round(bh * scale))
        tier = base.resize((w, h), Image.NEAREST)
        x = (bw - w) // 2                       # centre horizontally
        y = (bh + top_lift) - h - lift          # bottom-align the base, raise higher tiers
        canvas.alpha_composite(tier, (x, y))

    canvas.save(OUT)
    print(f"wrote {OUT} ({canvas.width}x{canvas.height})")


if __name__ == "__main__":
    main()
