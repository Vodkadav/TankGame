#!/usr/bin/env python3
"""Generate dimensional iso art for the passable terrain: bushes, sandbags, and the bridge.

These read as flat coloured squares today (the bush/sandbag overlays draw flat polygons; the bridge
is a flat tile). This draws low rounded clumps / a railed deck so they have some height. Programmer
-generated placeholder art — public domain, deterministic.

Every tile is 128 wide and bottom-aligned so its ground contact sits 33 px from the image bottom,
matching the other iso tiles' anchor (the overlay/IsoTerrainView lift the art by 33 - height/2).

Outputs (client/src/Presentation/Arena/): IsoBush.png  IsoSandbags.png  IsoBridge.png

Usage: python scripts/gen_iso_nature.py
"""

from PIL import Image, ImageDraw

W = 128
OUT = "client/src/Presentation/Arena"


def shade(c, f):
    return tuple(min(255, int(x * f)) for x in c[:3]) + (c[3] if len(c) > 3 else 255,)


def blob(d, cx, cy, rx, ry, base):
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=base)
    d.ellipse([cx - rx * 0.6, cy - ry, cx + rx * 0.2, cy - ry * 0.2], fill=shade(base, 1.25))  # highlight


def bush():
    # A low leafy clump of overlapping green blobs; translucent so a hidden tank still half-reads.
    h = 70
    img = Image.new("RGBA", (W, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    g = (60, 132, 56, 235)
    base_y = h - 33  # ground contact, 33 px from the bottom
    for cx, cy, rx, ry in [(48, base_y - 6, 26, 16), (82, base_y - 4, 24, 15),
                           (64, base_y - 20, 28, 18), (40, base_y - 16, 18, 13), (90, base_y - 16, 16, 12)]:
        blob(d, cx, cy, rx, ry, g)
    return img


def sandbags():
    # Two low rows of tan bags.
    h = 60
    img = Image.new("RGBA", (W, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    tan = (196, 168, 110, 255)
    base_y = h - 33
    for cx in (40, 64, 88):                                   # back row
        blob(d, cx, base_y - 16, 16, 10, shade(tan, 0.9))
    for cx in (52, 76):                                       # front row, lower
        blob(d, cx, base_y - 4, 17, 11, tan)
    d.line([(20, base_y), (108, base_y)], fill=shade(tan, 0.6))  # ground shadow line
    return img


def bridge():
    # A plank deck on the cell diamond with low rails along the two back edges and a thickness band.
    h = 90
    img = Image.new("RGBA", (W, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    t, r, b, l = (64, 25), (W - 1, 57), (64, 89), (0, 57)
    plank = (150, 108, 60, 255)
    dark = shade(plank, 0.6)
    d.polygon([(64, 31), (W - 7, 57), (64, 83), (7, 57)], fill=shade(plank, 0.5))  # thickness underlay
    d.polygon([t, r, b, l], fill=plank)                                            # deck
    for k in range(-40, 41, 10):                                                   # plank seams
        d.line([(64 + k, 25 + abs(k) // 2), (64 + k, 89 - abs(k) // 2)], fill=dark)
    d.line([t, r], fill=shade(plank, 1.2), width=2)                                # back-right rail
    d.line([t, l], fill=shade(plank, 1.2), width=2)                                # back-left rail
    return img


def main():
    for name, img in [("IsoBush.png", bush()), ("IsoSandbags.png", sandbags()), ("IsoBridge.png", bridge())]:
        path = f"{OUT}/{name}"
        img.save(path)
        print(f"wrote {path} ({img.width}x{img.height})")


if __name__ == "__main__":
    main()
