"""Outline + stitch the Blender-rendered cartoon-tank frames into the two committed spritesheets.

scripts/render_cartoon_tank.py writes hull_NN.png + turret_NN.png. This adds the bold black silhouette
outline that gives the cartoon look (a dilated-alpha black halo behind each frame), then — exactly like
pack_iso_tank.py — crops every frame to ONE shared bounding box so the hull and turret layers stay
pixel-aligned when overlaid in-engine, and lays each layer out as a horizontal strip:

    IsoTankHull.png    = FACINGS frames left-to-right
    IsoTankTurret.png  = FACINGS frames left-to-right

Run with the system Python (Pillow):  py scripts/pack_cartoon_tank.py <frames_dir> <out_dir>
A composite preview montage is written to the frames dir for eyeballing (not committed).
"""
import sys
from pathlib import Path
from PIL import Image, ImageFilter

FACINGS = 16
OUTLINE_PX = 3           # outline thickness in source pixels
OUTLINE = (24, 22, 28, 255)


def outlined(im):
    """Add a black silhouette outline behind the sprite (dilate alpha, fill, composite under)."""
    alpha = im.getchannel("A")
    grown = alpha.filter(ImageFilter.MaxFilter(OUTLINE_PX * 2 + 1))
    halo = Image.new("RGBA", im.size, (0, 0, 0, 0))
    halo.paste(OUTLINE, (0, 0), grown)
    halo.alpha_composite(im)
    return halo


def load(frames, layer):
    return [outlined(Image.open(frames / f"{layer}_{i:02d}.png").convert("RGBA")) for i in range(FACINGS)]


def union_bbox(images):
    box = None
    for im in images:
        b = im.getbbox()
        if b is None:
            continue
        box = b if box is None else (
            min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    return box


def strip(images, box):
    crops = [im.crop(box) for im in images]
    w, h = crops[0].size
    sheet = Image.new("RGBA", (w * len(crops), h), (0, 0, 0, 0))
    for i, c in enumerate(crops):
        sheet.paste(c, (i * w, 0))
    return sheet, (w, h)


def main():
    frames = Path(sys.argv[1])
    out = Path(sys.argv[2])
    hull = load(frames, "hull")
    turret = load(frames, "turret")
    box = union_bbox(hull + turret)

    hull_sheet, (fw, fh) = strip(hull, box)
    turret_sheet, _ = strip(turret, box)
    hull_sheet.save(out / "IsoTankHull.png")
    turret_sheet.save(out / "IsoTankTurret.png")

    preview = Image.new("RGBA", (fw * 4, fh), (40, 40, 50, 255))
    for col, t in enumerate((0, 4, 8, 12)):
        cell = hull[0].crop(box).copy()
        cell.alpha_composite(turret[t].crop(box))
        preview.paste(cell, (col * fw, 0))
    preview.save(frames / "preview_overlay.png")
    print(f"PACKED frame={fw}x{fh} hull+turret -> {out}")


main()
