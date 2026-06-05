#!/usr/bin/env python3
"""Prepare the game-ready bullet sprite from the Kenney "Top-down Tanks Remastered" pack (CC0).

Scales the sand bullet down to projectile size and rotates it to face EAST (the game's rotation-0
convention; Kenney art faces north), writing KenneyBullet.png into the Projectile presentation
folder. ProjectileView rotates the sprite to the shot's travel direction, so the elongated bullet
points the way it flies.

Source: https://kenney.nl/assets/top-down-tanks-remastered  (CC0 1.0, no attribution required).
Re-runnable; pass the pack's "PNG/Retina" folder as argv[1] to override the default path.
"""
import os
import sys

from PIL import Image

DEFAULT_SRC = r"C:\programmering\TankGame\kenney_top-down-tanks-remastered\PNG\Retina"
SRC = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SRC
DST = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "client",
                                   "src", "Presentation", "Projectile"))

LONG = 20  # the bullet's long side, in pixels


def main():
    bullet = Image.open(os.path.join(SRC, "bulletSand2.png")).convert("RGBA")
    w, h = bullet.size
    s = LONG / max(w, h)
    bullet = bullet.resize((max(1, round(w * s)), max(1, round(h * s))), Image.LANCZOS)
    # PIL rotate is counter-clockwise; -90 turns the north-facing bullet to face east.
    bullet.rotate(-90, expand=True).save(os.path.join(DST, "KenneyBullet.png"))
    print(f"wrote KenneyBullet.png to {DST}")


if __name__ == "__main__":
    main()
