#!/usr/bin/env python3
"""Generate the placeholder wall atlas for M2 (brick damage states + steel).

Programmer-generated placeholder art — public domain, to be replaced by Kenney
"Top-down Shooter" CC0 wall tiles (see docs/credits/assets.md). Deterministic (no
randomness) so re-running reproduces the exact same PNG.

Atlas layout: four 32x32 tiles in a horizontal strip (128x32), left to right:
  0 brick intact  (hp 3)   1 brick cracked (hp 2)
  2 brick rubble  (hp 1)   3 steel (indestructible)
Floor is drawn as no tile, so it is absent from the atlas.

Usage: python scripts/gen_wall_atlas.py
"""

from PIL import Image, ImageDraw

TILE = 32
FRAMES = 4
OUT = "client/src/Presentation/Arena/Walls.png"

MORTAR = (60, 55, 52, 255)
BRICK = (150, 70, 52, 255)
BRICK_DARK = (110, 48, 36, 255)
CRACK = (32, 24, 22, 255)
STEEL = (120, 124, 132, 255)
STEEL_LIGHT = (160, 165, 172, 255)
STEEL_DARK = (78, 82, 90, 255)
RIVET = (54, 57, 64, 255)


def brick_base(draw):
    """A running-bond brick pattern filling one tile."""
    draw.rectangle([0, 0, TILE - 1, TILE - 1], fill=MORTAR)
    row_h = 8
    brick_w = 16
    gap = 2
    for ry, top in enumerate(range(0, TILE, row_h)):
        offset = -8 if ry % 2 else 0
        for left in range(offset, TILE, brick_w):
            x0, y0 = left + gap, top + gap
            x1, y1 = left + brick_w - gap, top + row_h - gap
            if x1 <= 0 or x0 >= TILE:
                continue
            draw.rectangle([max(0, x0), y0, min(TILE - 1, x1), y1], fill=BRICK)
            draw.line([max(0, x0), y1, min(TILE - 1, x1), y1], fill=BRICK_DARK)


def add_cracks(draw, segments):
    for seg in segments:
        draw.line(seg, fill=CRACK, width=1)


def draw_brick(img, state):
    draw = ImageDraw.Draw(img)
    brick_base(draw)
    if state >= 1:  # cracked
        add_cracks(draw, [[(8, 2), (12, 14), (9, 24), (14, 30)]])
    if state >= 2:  # rubble — more cracks and knocked-out chunks
        add_cracks(draw, [
            [(24, 0), (20, 10), (26, 18), (22, 31)],
            [(2, 16), (16, 18), (30, 15)],
        ])
        for hole in [(4, 4, 9, 9), (22, 22, 28, 28)]:
            draw.rectangle(list(hole), fill=MORTAR)


def draw_steel(img):
    draw = ImageDraw.Draw(img)
    draw.rectangle([0, 0, TILE - 1, TILE - 1], fill=STEEL)
    draw.line([0, 0, TILE - 1, 0], fill=STEEL_LIGHT)
    draw.line([0, 0, 0, TILE - 1], fill=STEEL_LIGHT)
    draw.line([TILE - 1, 0, TILE - 1, TILE - 1], fill=STEEL_DARK)
    draw.line([0, TILE - 1, TILE - 1, TILE - 1], fill=STEEL_DARK)
    for cx, cy in [(6, 6), (TILE - 7, 6), (6, TILE - 7), (TILE - 7, TILE - 7)]:
        draw.ellipse([cx - 2, cy - 2, cx + 2, cy + 2], fill=RIVET)


def main():
    atlas = Image.new("RGBA", (TILE * FRAMES, TILE), (0, 0, 0, 0))
    for state in range(3):  # intact, cracked, rubble
        tile = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
        draw_brick(tile, state)
        atlas.paste(tile, (state * TILE, 0))
    steel = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
    draw_steel(steel)
    atlas.paste(steel, (3 * TILE, 0))
    atlas.save(OUT)
    print(f"wrote {OUT} ({atlas.width}x{atlas.height})")


if __name__ == "__main__":
    main()
