#!/usr/bin/env python3
"""Generate placeholder *isometric* wall blocks (Phase 2 iso-walls pass).

Programmer-generated placeholder art — public domain, deterministic (no randomness),
to be replaced by cohesive iso wall art later (see docs/credits/assets.md). Each block
is a raised iso cube: a top diamond face plus two shaded front faces, so a wall reads as
standing up from the ground rather than as a flat tile.

Geometry matches the PixVoxel terrain tiles so blocks align on the same grid: every image
is 128 wide and its BASE diamond is bottom-aligned exactly like a 128x90 flat tile (top
vertex 25 px / bottom vertex 89 px above the base), with the block height added ABOVE. The
base-diamond centre therefore always sits 33 px from the image bottom, which is what
IsoTerrainView's vertical anchor (33 - height/2) relies on.

Outputs (client/src/Presentation/Arena/):
  IsoBrick0/1/2.png (intact/cracked/rubble)  IsoSteel.png  IsoCrate.png  IsoBuilding.png

Usage: python scripts/gen_iso_blocks.py
"""

from PIL import Image, ImageDraw

W = 128                 # tile width (diamond span)
DIAMOND_TOP = 25        # base diamond top-vertex y within the bottom 90 px (matches PixVoxel tiles)
DIAMOND_MID = 57        # base diamond left/right-vertex y
DIAMOND_BOT = 89        # base diamond bottom-vertex y
OUT = "client/src/Presentation/Arena"


def shade(c, f):
    return tuple(min(255, int(x * f)) for x in c[:3]) + (255,)


def block(base, height, top_detail=None, front_detail=None):
    """A raised iso cube `height` px tall on the 128-wide base diamond."""
    img = Image.new("RGBA", (W, height + 90), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    h = height

    # Top face is the base diamond raised by `height`; the front faces drop back down to the base.
    t, r, b, l = (64, DIAMOND_TOP), (W - 1, DIAMOND_MID), (64, DIAMOND_BOT), (0, DIAMOND_MID)
    lb, bb, rb = (0, DIAMOND_MID + h), (64, DIAMOND_BOT + h), (W - 1, DIAMOND_MID + h)

    left_face = shade(base, 0.70)
    right_face = shade(base, 0.50)
    edge = shade(base, 0.35)

    d.polygon([l, b, bb, lb], fill=left_face)          # front-left face
    d.polygon([b, r, rb, bb], fill=right_face)         # front-right face
    d.polygon([t, r, b, l], fill=shade(base, 1.0))     # top face (drawn last, on top)

    # Crisp silhouette + the ridge between the two front faces.
    d.line([t, r, b, l, t], fill=edge)
    d.line([l, lb], fill=edge)
    d.line([r, rb], fill=edge)
    d.line([b, bb], fill=edge)

    if top_detail:
        top_detail(d, base)
    if front_detail:
        front_detail(d, base, h)
    return img


def brick_top(d, base):
    # A few mortar courses parallel to the diamond edges read as brickwork on the top face.
    mortar = shade(base, 0.45)
    for k in (18, 36):
        d.line([(64 - k, DIAMOND_TOP + k // 2 + 1), (k, DIAMOND_MID)], fill=mortar)
        d.line([(64 + k, DIAMOND_TOP + k // 2 + 1), (W - 1 - k, DIAMOND_MID)], fill=mortar)


def brick_front(d, base, h):
    mortar = shade(base, 0.32)
    for yy in range(DIAMOND_MID + 8, DIAMOND_MID + h, 9):  # horizontal courses on both faces
        d.line([(0, yy), (64, yy + 16)], fill=mortar)
        d.line([(64, yy + 16), (W - 1, yy)], fill=mortar)


def cracks(d):
    c = (28, 20, 18, 255)
    d.line([(48, 30), (60, 52), (52, 70)], fill=c)
    d.line([(80, 36), (72, 58), (84, 78)], fill=c)


def crate_front(d, base, h):
    brace = shade(base, 1.15)
    d.line([(4, DIAMOND_MID + h - 6), (60, DIAMOND_BOT + h - 4)], fill=brace, width=2)
    d.line([(64, DIAMOND_BOT + h - 4), (W - 5, DIAMOND_MID + h - 6)], fill=brace, width=2)


def building_front(d, base, h):
    win = (96, 140, 170, 255)
    frame = shade(base, 0.4)
    for row in range(DIAMOND_MID + 14, DIAMOND_MID + h - 6, 16):
        for cx in (22, 42):
            d.rectangle([cx, row, cx + 9, row + 9], fill=win, outline=frame)        # left face
        for cx in (78, 98):
            d.rectangle([cx, row - 8, cx + 9, row + 1], fill=win, outline=frame)    # right face


def main():
    BRICK = (150, 70, 52, 255)
    jobs = [
        ("IsoBrick0.png", block(BRICK, 38, brick_top, brick_front)),
        ("IsoBrick1.png", _with(block(shade(BRICK, 0.85), 38, brick_top, brick_front), cracks)),
        ("IsoBrick2.png", _with(block(shade(BRICK, 0.7), 14, None, brick_front), cracks)),
        ("IsoSteel.png", block((120, 124, 132, 255), 42)),  # plain plate — no rivets
        ("IsoCrate.png", block((168, 120, 66, 255), 32, None, crate_front)),
        ("IsoBuilding.png", block((158, 150, 138, 255), 56, None, building_front)),
    ]
    for name, img in jobs:
        path = f"{OUT}/{name}"
        img.save(path)
        print(f"wrote {path} ({img.width}x{img.height})")


def _with(img, extra):
    extra(ImageDraw.Draw(img))
    return img


if __name__ == "__main__":
    main()
