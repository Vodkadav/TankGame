#!/usr/bin/env python3
"""Prepare game-ready tank sprites from the Kenney "Top-down Tanks Remastered" pack (CC0).

Reads the *sand* tank body + barrel from an extracted copy of the pack and writes two
game-ready PNGs into the Tank presentation folder:

  KenneyTankBody.png    - the hull, scaled to ~one tile and rotated to face EAST (the game's
                          rotation-0 convention; Kenney art faces north).
  KenneyTankTurret.png  - the gun barrel, scaled by the SAME factor as the hull, re-canvased so
                          its mount sits at the image centre (a centred Sprite2D then pivots about
                          the hull centre), and rotated to face east.

The neutral sand colour is tinted per team at runtime via TankView.ApplyTeamTint (Modulate).

Source: https://kenney.nl/assets/top-down-tanks-remastered  (CC0 1.0, no attribution required).
Re-runnable; pass the pack's "PNG/Retina" folder as argv[1] to override the default path.
Deterministic (LANCZOS resample), committed for provenance like scripts/gen_wall_atlas.py.
"""
import os
import sys

from PIL import Image

DEFAULT_SRC = r"C:\programmering\TankGame\kenney_top-down-tanks-remastered\PNG\Retina"
SRC = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SRC
DST = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "client",
                                   "src", "Presentation", "Tank"))

TILE = 64  # the game tank is roughly one 64 px tile across


def scaled(im, target_long):
    w, h = im.size
    s = target_long / max(w, h)
    return im.resize((max(1, round(w * s)), max(1, round(h * s))), Image.LANCZOS), s


def main():
    body = Image.open(os.path.join(SRC, "tankBody_sand.png")).convert("RGBA")
    body, scale = scaled(body, TILE)
    # PIL rotate is counter-clockwise; -90 turns the north-facing art to face east.
    body.rotate(-90, expand=True).save(os.path.join(DST, "KenneyTankBody.png"))

    barrel = Image.open(os.path.join(SRC, "tankSand_barrel1.png")).convert("RGBA")
    bw, bh = barrel.size
    barrel = barrel.resize((max(1, round(bw * scale)), max(1, round(bh * scale))), Image.LANCZOS)
    bw, bh = barrel.size
    # The barrel's mount is its bottom edge. Pasting it into the TOP half of a double-height
    # canvas puts the mount on the centre row, so the centred sprite pivots about the mount.
    canvas = Image.new("RGBA", (bw, bh * 2), (0, 0, 0, 0))
    canvas.paste(barrel, (0, 0), barrel)
    canvas.rotate(-90, expand=True).save(os.path.join(DST, "KenneyTankTurret.png"))

    print(f"wrote KenneyTankBody.png and KenneyTankTurret.png to {DST} (scale {scale:.3f})")


if __name__ == "__main__":
    main()
