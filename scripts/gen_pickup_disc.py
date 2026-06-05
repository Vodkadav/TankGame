#!/usr/bin/env python3
"""Generate the neutral glowing pickup disc — a deterministic, perfectly tintable token.

A white disc with a dark coin outline and a soft white glow halo, on clean transparent alpha. It is
neutral greyscale so PowerupView tints it per kind via Modulate (white core -> the kind's colour, the
dark outline stays dark, the halo glows in the kind's colour). PIL gives the clean alpha and exact
neutrality that base SDXL would not (its disc attempts came out coloured, hazy, and shadowed), so this
is the "generated" half of the hybrid art plan. Committed + deterministic, like gen_wall_atlas.py.

Writes client/src/Presentation/Arena/PickupDisc.png.
"""
import os

from PIL import Image

SIZE = 128
CENTER = SIZE / 2.0
CORE = 50.0      # solid white out to here
OUTLINE = 56.0   # dark coin edge to here
GLOW = 63.0      # soft halo fades to nothing by here

DST = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "client",
                                   "src", "Presentation", "Arena", "PickupDisc.png"))


def main():
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    px = img.load()
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = x + 0.5 - CENTER, y + 0.5 - CENTER
            r = (dx * dx + dy * dy) ** 0.5
            if r < CORE:
                px[x, y] = (255, 255, 255, 255)          # white core -> tints to the kind colour
            elif r < OUTLINE:
                px[x, y] = (40, 40, 40, 255)              # dark coin edge -> stays dark when tinted
            elif r < GLOW:
                a = int(160 * (1.0 - (r - OUTLINE) / (GLOW - OUTLINE)))
                px[x, y] = (255, 255, 255, max(0, a))     # soft glow halo
    img.save(DST)
    print(f"wrote PickupDisc.png ({SIZE}x{SIZE}) to {DST}")


if __name__ == "__main__":
    main()
