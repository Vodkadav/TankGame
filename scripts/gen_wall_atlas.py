#!/usr/bin/env python3
"""Generate the placeholder wall atlas for M2 (brick damage states + steel).

Programmer-generated placeholder art — public domain, to be replaced by Kenney
"Top-down Shooter" CC0 wall tiles (see docs/credits/assets.md). Deterministic (no
randomness) so re-running reproduces the exact same PNG.

Atlas layout: nine 32x32 tiles in a horizontal strip (288x32), left to right:
  0 brick intact  (hp 3)   1 brick cracked (hp 2)
  2 brick rubble  (hp 1)   3 steel (indestructible)   4 crate (destructible)
  5 water (blocks movement, not shots)   6 bridge (passable crossing)
  7 mountain (impassable, blocks shots)   8 building (solid)
Floor is drawn as no tile, so it is absent from the atlas.

Usage: python scripts/gen_wall_atlas.py
"""

from PIL import Image, ImageDraw

TILE = 32
FRAMES = 9
OUT = "client/src/Presentation/Arena/Walls.png"

MORTAR = (60, 55, 52, 255)
BRICK = (150, 70, 52, 255)
BRICK_DARK = (110, 48, 36, 255)
CRACK = (32, 24, 22, 255)
STEEL = (120, 124, 132, 255)
STEEL_LIGHT = (160, 165, 172, 255)
STEEL_DARK = (78, 82, 90, 255)
RIVET = (54, 57, 64, 255)
WOOD = (168, 120, 66, 255)
WOOD_DARK = (120, 82, 42, 255)
WOOD_LIGHT = (198, 150, 92, 255)
WATER = (54, 110, 170, 255)
WATER_LIGHT = (92, 150, 205, 255)
PLANK = (150, 108, 60, 255)
PLANK_DARK = (104, 72, 38, 255)
ROCK = (108, 104, 100, 255)
ROCK_DARK = (74, 70, 66, 255)
ROCK_LIGHT = (150, 146, 140, 255)
SNOW = (226, 230, 236, 255)
WALL = (158, 150, 138, 255)
WALL_DARK = (112, 104, 92, 255)
WINDOW = (96, 140, 170, 255)


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


def draw_crate(img):
    """A wooden crate: plank fill, dark border, and a diagonal cross brace."""
    draw = ImageDraw.Draw(img)
    draw.rectangle([0, 0, TILE - 1, TILE - 1], fill=WOOD)
    for y in range(0, TILE, 8):
        draw.line([0, y, TILE - 1, y], fill=WOOD_DARK)  # plank seams
    draw.rectangle([0, 0, TILE - 1, TILE - 1], outline=WOOD_DARK, width=2)  # frame
    draw.line([2, 2, TILE - 3, TILE - 3], fill=WOOD_LIGHT, width=2)  # cross brace
    draw.line([TILE - 3, 2, 2, TILE - 3], fill=WOOD_LIGHT, width=2)


def draw_water(img):
    """Flat blue water with a couple of lighter wavelets."""
    draw = ImageDraw.Draw(img)
    draw.rectangle([0, 0, TILE - 1, TILE - 1], fill=WATER)
    for wy in (9, 21):
        draw.arc([4, wy - 3, 14, wy + 3], 200, 340, fill=WATER_LIGHT, width=2)
        draw.arc([18, wy - 1, 28, wy + 5], 200, 340, fill=WATER_LIGHT, width=2)


def draw_bridge(img):
    """Wooden planks running across, over the water."""
    draw = ImageDraw.Draw(img)
    draw.rectangle([0, 0, TILE - 1, TILE - 1], fill=WATER)        # water shows at the edges
    draw.rectangle([0, 4, TILE - 1, TILE - 5], fill=PLANK)        # the deck
    for x in range(0, TILE, 6):
        draw.line([x, 4, x, TILE - 5], fill=PLANK_DARK)          # plank seams
    draw.line([0, 4, TILE - 1, 4], fill=PLANK_DARK)
    draw.line([0, TILE - 5, TILE - 1, TILE - 5], fill=PLANK_DARK)


def draw_mountain(img):
    """A grey rocky block with a darker base and a light/snow cap."""
    draw = ImageDraw.Draw(img)
    draw.rectangle([0, 0, TILE - 1, TILE - 1], fill=ROCK)
    draw.polygon([(2, TILE - 2), (12, 6), (20, 16), (26, 4), (TILE - 2, TILE - 2)], fill=ROCK_DARK)
    draw.polygon([(8, 12), (12, 6), (16, 12)], fill=SNOW)        # one peak's cap
    draw.polygon([(22, 10), (26, 4), (30, 10)], fill=SNOW)       # the other
    draw.line([0, 0, TILE - 1, 0], fill=ROCK_LIGHT)


def draw_building(img):
    """A solid building block: stone wall with a dark base and a grid of little windows."""
    draw = ImageDraw.Draw(img)
    draw.rectangle([0, 0, TILE - 1, TILE - 1], fill=WALL)
    draw.rectangle([0, 0, TILE - 1, TILE - 1], outline=WALL_DARK, width=2)
    for wy in (6, 16, 24):
        for wx in (5, 14, 23):
            draw.rectangle([wx, wy, wx + 4, wy + 4], fill=WINDOW, outline=WALL_DARK)


def main():
    atlas = Image.new("RGBA", (TILE * FRAMES, TILE), (0, 0, 0, 0))
    for state in range(3):  # intact, cracked, rubble
        tile = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
        draw_brick(tile, state)
        atlas.paste(tile, (state * TILE, 0))
    for index, painter in ((3, draw_steel), (4, draw_crate), (5, draw_water),
                           (6, draw_bridge), (7, draw_mountain), (8, draw_building)):
        tile = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
        painter(tile)
        atlas.paste(tile, (index * TILE, 0))
    atlas.save(OUT)
    print(f"wrote {OUT} ({atlas.width}x{atlas.height})")


if __name__ == "__main__":
    main()
