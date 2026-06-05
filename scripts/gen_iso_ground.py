#!/usr/bin/env python3
"""Generate a seamless iso ground tile (no per-tile border).

The PixVoxel Desert tile has a darker rim, so a field of them reads as a grid of squares. This draws
a flat sandy diamond with only fine per-pixel noise and hard edges, so neighbouring cells tessellate
into one seamless ground. Same 128x90 layout / diamond position as the other tiles, so the ground
TileMapLayer setup is unchanged. Deterministic. Programmer-generated placeholder — public domain.

Usage: python scripts/gen_iso_ground.py
"""

from PIL import Image, ImageDraw

W, H = 128, 90
DIAMOND = [(64, 25), (127, 57), (64, 89), (0, 57)]  # same diamond as the other iso tiles
SAND = (226, 208, 150)
OUT = "client/src/Presentation/Arena/IsoGroundSeamless.png"


def main():
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(img).polygon(DIAMOND, fill=SAND + (255,))  # hard-edged diamond — tessellates clean

    # Deterministic fine noise so the flat fill is not sterile (a simple hash, no RNG state).
    px = img.load()
    for y in range(H):
        for x in range(W):
            if px[x, y][3] == 255:
                n = (((x * 131 + y * 977 + x * y * 17) ^ (x + y * 53)) % 13) - 6   # -6..+6, scattered
                r, g, b, a = px[x, y]
                px[x, y] = (max(0, min(255, r + n)), max(0, min(255, g + n)), max(0, min(255, b + n)), a)

    img.save(OUT)
    print(f"wrote {OUT} ({img.width}x{img.height})")


if __name__ == "__main__":
    main()
