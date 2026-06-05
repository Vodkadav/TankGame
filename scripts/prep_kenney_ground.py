#!/usr/bin/env python3
"""Prepare the game-ready ground tile from the Kenney "Top-down Tanks Remastered" pack (CC0).

Resizes the seamless sand tile to the 64 px game tile size and writes it into the Arena
presentation folder as GroundSand.png; the scene tiles it across the field (ArenaScene.BuildGround)
and tints it per ArenaTheme.

Source: https://kenney.nl/assets/top-down-tanks-remastered  (CC0 1.0, no attribution required).
Re-runnable; pass the pack's "PNG/Retina" folder as argv[1] to override the default path.
"""
import os
import sys

from PIL import Image

DEFAULT_SRC = r"C:\programmering\TankGame\kenney_top-down-tanks-remastered\PNG\Retina"
SRC = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_SRC
DST = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "client",
                                   "src", "Presentation", "Arena"))

TILE = 64


def main():
    tile = Image.open(os.path.join(SRC, "tileSand1.png")).convert("RGBA")
    tile.resize((TILE, TILE), Image.LANCZOS).save(os.path.join(DST, "GroundSand.png"))
    print(f"wrote GroundSand.png ({TILE}x{TILE}) to {DST}")


if __name__ == "__main__":
    main()
