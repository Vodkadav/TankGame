"""Generate placeholder arena preview thumbnails for the Select Map screen.

Deterministic (seeded) PIL art, committed as a public-domain placeholder per the project's
art-asset convention (see docs/credits/assets.md) — to be replaced by real in-game screenshots.
Writes 480x360 PNGs into client/src/Presentation/Arena/previews/.

Run: py scripts/gen_arena_previews.py
"""
import os
import random
from PIL import Image, ImageDraw

W, H = 480, 360
OUT = os.path.join("client", "src", "Presentation", "Arena", "previews")


def _vertical_gradient(top, bottom):
    img = Image.new("RGB", (W, H), top)
    px = img.load()
    for y in range(H):
        t = y / (H - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3))
        for x in range(W):
            px[x, y] = c
    return img


def _block(draw, x, y, w, h, fill):
    draw.rectangle([x, y, x + w, y + h], fill=fill, outline=(30, 26, 20), width=3)


def desert_war():
    rng = random.Random(1)
    img = _vertical_gradient((214, 188, 132), (180, 150, 96))  # dusty sand
    d = ImageDraw.Draw(img)
    # scattered building blocks
    palette = [(168, 78, 64), (110, 116, 120), (150, 122, 78), (96, 104, 70)]
    for _ in range(9):
        w = rng.randint(40, 80)
        h = rng.randint(40, 80)
        x = rng.randint(10, W - w - 10)
        y = rng.randint(10, H - h - 10)
        _block(d, x, y, w, h, rng.choice(palette))
    # a few bushes
    for _ in range(7):
        x = rng.randint(10, W - 30)
        y = rng.randint(10, H - 30)
        d.ellipse([x, y, x + 22, y + 18], fill=(70, 104, 56))
    # two tanks
    d.rectangle([90, 250, 126, 282], fill=(70, 170, 80), outline=(20, 20, 20), width=3)
    d.rectangle([360, 90, 396, 122], fill=(200, 70, 70), outline=(20, 20, 20), width=3)
    return img


def cliffs_and_valleys():
    img = _vertical_gradient((150, 170, 190), (96, 120, 110))  # cooler, hillier sky
    d = ImageDraw.Draw(img)
    # three stacked elevation bands (plateaus), back to front, each higher and darker
    bands = [
        (0, 250, W, H, (96, 116, 72)),       # valley floor
        (60, 180, W - 30, 270, (120, 138, 84)),  # mid plateau
        (150, 120, W - 90, 210, (146, 160, 98)),  # high plateau
    ]
    for x0, y0, x1, y1, col in bands:
        d.rectangle([x0, y0, x1, y1], fill=col, outline=(54, 60, 38), width=4)
    # a ramp wedge connecting valley to mid plateau
    d.polygon([(60, 270), (150, 270), (60, 200)], fill=(120, 100, 70), outline=(54, 48, 34))
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    desert_war().save(os.path.join(OUT, "DesertWar.png"))
    cliffs_and_valleys().save(os.path.join(OUT, "CliffsAndValleys.png"))
    print("wrote DesertWar.png + CliffsAndValleys.png to", OUT)


if __name__ == "__main__":
    main()
