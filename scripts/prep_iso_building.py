#!/usr/bin/env python3
"""Prepare the PixVoxel Castle as the iso Building tile (a military fortress/tower).

The CellMaterial.Building should read as a military structure and stand clearly taller than a tank
(~2-3 tanks tall, the tank being ~64 px). This scales `color0/Castle_Large_face0_0.png` up and crops
it to its opaque bounds (bottom vertex at the bottom edge), so IsoTerrainView's uniform anchor
(33 - height/2) stands the fortress on its cell. CC0 source; deterministic.

Usage: python scripts/prep_iso_building.py
"""

from PIL import Image

SRC = ("C:/programmering/Assets/visual/isometric/PixVoxel_Wargame/PixVoxel_Wargame/"
       "isometric/color0/Castle_Large_face0_0.png")
OUT = "client/src/Presentation/Arena/IsoBuilding.png"
SCALE = 1.7              # ~108 px source → ~158 px cropped, roughly 2.5 tanks tall


def main():
    src = Image.open(SRC).convert("RGBA")
    b = src.resize((round(src.width * SCALE), round(src.height * SCALE)), Image.NEAREST)
    b = b.crop(b.getbbox())  # tight opaque bounds: bottom vertex at the bottom edge

    # Pad to an even width and centre, so the symmetric fortress's base diamond centres on the cell;
    # IsoTerrainView's uniform anchor (33 - height/2) puts the foot on the cell.
    w = b.width + (b.width % 2)
    canvas = Image.new("RGBA", (w, b.height), (0, 0, 0, 0))
    canvas.alpha_composite(b, ((w - b.width) // 2, 0))
    canvas.save(OUT)
    print(f"wrote {OUT} ({canvas.width}x{canvas.height})")


if __name__ == "__main__":
    main()
