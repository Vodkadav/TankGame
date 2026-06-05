#!/usr/bin/env python3
"""Generate dimensional iso art for the passable terrain: bushes, sandbags, and the bridge.

These read as flat drawings on the floor; this draws them with real height — a rounded leafy bush
mound, a stacked sandbag emplacement, and a planked bridge deck with side rails — shaded top-to-bottom
so they read 3D. Programmer-generated placeholder art — public domain, deterministic.

Every tile is bottom-aligned so its ground contact sits 33 px from the image bottom, matching the
other iso tiles' anchor (the overlay/IsoTerrainView lift the art by 33 - height/2).

Outputs (client/src/Presentation/Arena/): IsoBush.png  IsoSandbags.png  IsoBridge.png

Usage: python scripts/gen_iso_nature.py
"""

from PIL import Image, ImageDraw

W = 128
GROUND = 33  # ground-contact distance from the image bottom
OUT = "client/src/Presentation/Arena"


def shade(c, f):
    return tuple(min(255, int(x * f)) for x in c[:3]) + (c[3] if len(c) > 3 else 255,)


def dome(d, cx, base_y, rx, height, base):
    """A shaded 3D mound: stacked ellipse slices, dark at the foot to light at the crown."""
    slices = max(6, height // 3)
    for i in range(slices):
        t = i / (slices - 1)                      # 0 foot .. 1 crown
        y = base_y - int(t * height)
        r = int(rx * (1.0 - 0.55 * t))            # taper toward the crown
        ry = max(3, int(r * 0.5))
        col = shade(base, 0.65 + 0.55 * t)        # darker low, lighter high
        d.ellipse([cx - r, y - ry, cx + r, y - ry + ry * 2], fill=col)


def bush():
    h = 92
    img = Image.new("RGBA", (W, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    g = (54, 126, 50, 255)
    base_y = h - GROUND
    d.ellipse([28, base_y - 6, 100, base_y + 8], fill=shade(g, 0.4))  # ground shadow
    dome(d, 50, base_y, 26, 36, g)
    dome(d, 82, base_y, 24, 32, g)
    dome(d, 65, base_y - 4, 30, 50, shade(g, 1.05))                   # tall back mound
    return img


def sandbags():
    h = 82
    img = Image.new("RGBA", (W, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    tan = (196, 168, 110, 255)
    base_y = h - GROUND
    d.ellipse([24, base_y - 4, 104, base_y + 8], fill=shade(tan, 0.4))  # ground shadow

    def bag(cx, cy, c):
        d.ellipse([cx - 17, cy - 9, cx + 17, cy + 9], fill=c)
        d.ellipse([cx - 14, cy - 9, cx - 2, cy - 1], fill=shade(c, 1.2))  # highlight

    for cx in (38, 64, 90):                         # bottom row, darker
        bag(cx, base_y - 6, shade(tan, 0.8))
    for cx in (51, 77):                             # middle row
        bag(cx, base_y - 20, tan)
    bag(64, base_y - 34, shade(tan, 1.1))           # top bag
    return img


def bridge():
    h = 90
    img = Image.new("RGBA", (W, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    t, r, b, l = (64, 25), (W - 1, 57), (64, 89), (0, 57)
    plank = (150, 108, 60, 255)
    dark = shade(plank, 0.6)
    rail = shade(plank, 1.15)
    d.polygon([(64, 33), (W - 7, 57), (64, 83), (7, 57)], fill=shade(plank, 0.45))  # thickness underlay
    d.polygon([t, r, b, l], fill=plank)                                             # deck
    for k in range(-40, 41, 9):                                                     # plank seams
        d.line([(64 + k, 25 + abs(k) // 2), (64 + k, 89 - abs(k) // 2)], fill=dark)
    # Raised side rails along the two back edges: posts + a top rail lifted ~10 px.
    for (ax, ay), (bx, by) in [((t[0], t[1]), (l[0], l[1])), ((t[0], t[1]), (r[0], r[1]))]:
        d.line([(ax, ay - 10), (bx, by - 10)], fill=rail, width=2)                  # top rail
        for s in (0.0, 0.5, 1.0):
            px, py = int(ax + (bx - ax) * s), int(ay + (by - ay) * s)
            d.line([(px, py), (px, py - 11)], fill=dark, width=2)                   # post
    return img


def main():
    for name, img in [("IsoBush.png", bush()), ("IsoSandbags.png", sandbags()), ("IsoBridge.png", bridge())]:
        path = f"{OUT}/{name}"
        img.save(path)
        print(f"wrote {path} ({img.width}x{img.height})")


if __name__ == "__main__":
    main()
