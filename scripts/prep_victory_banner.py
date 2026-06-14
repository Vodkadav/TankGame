"""Prepare the victory-screen banner art from the raw AI template.

Source:  C:/programmering/Assets/visual/ui/victory template2.png (1024x1536 portrait)
Output:  client/src/Presentation/Arena/ui/victory_banner.png

Steps (all deterministic, no randomness):
1. Crop uniform near-white bands off every edge (the source has an un-cropped
   white artifact strip at the bottom).
2. Blank the baked "[PLACEHOLDER]" text on the wood ribbon — letters are
   detected (gold fill + dark outline near gold) and replaced with the row's
   median wood tone plus a fixed integer-hash dither. "IS VICTORIOUS!" and the
   red IS badge are protected and untouched.
3. Blank the baked "KILLS" / "DEATHS" texts inside the two stat pills — each
   pill row is refilled with the median of its own text-free margin pixels, so
   the vertical gradient and top highlight band survive. Pill frames and the
   blue arrow buttons are untouched.

Run: py scripts/prep_victory_banner.py
"""
import os
from PIL import Image, ImageFilter
import numpy as np

SRC = r"C:\programmering\Assets\visual\ui\victory template2.png"
OUT = os.path.join("client", "src", "Presentation", "Arena", "ui", "victory_banner.png")

# Deterministic dither so the fills do not look machine-flat.
def _noise(x, y, amp=6):
    h = (x * 1103515245 + y * 12345 + 1013904223) & 0xFFFFFFFF
    return ((h >> 16) % (2 * amp + 1)) - amp


def crop_white_edges(img):
    """Crop rows/cols at each edge whose pixels are ~uniform near-white."""
    a = np.asarray(img, dtype=np.float32)

    def is_white_row(y):
        return a[y].mean() > 245 and a[y].std() < 12

    def is_white_col(x):
        return a[:, x].mean() > 245 and a[:, x].std() < 12

    h, w = a.shape[:2]
    top = 0
    while top < h - 1 and is_white_row(top):
        top += 1
    bottom = h
    while bottom > top + 1 and is_white_row(bottom - 1):
        bottom -= 1
    left = 0
    while left < w - 1 and is_white_col(left):
        left += 1
    right = w
    while right > left + 1 and is_white_col(right - 1):
        right -= 1
    return img.crop((left, top, right, bottom))


def _dilate(mask_bool, radius):
    """Binary dilation of a numpy bool mask via PIL MaxFilter (odd kernel)."""
    m = Image.fromarray((mask_bool * 255).astype(np.uint8))
    m = m.filter(ImageFilter.MaxFilter(2 * radius + 1))
    return np.asarray(m) > 0


def blank_ribbon_text(a):
    """Erase '[PLACEHOLDER]' from the wood ribbon, leave 'IS VICTORIOUS!'.

    Coordinates are in source space (the white band is bottom-only, so the
    crop does not shift x/y of anything above it).
    """
    x0, y0, x1, y1 = 205, 222, 805, 333          # text window on the plank
    px0, py0, px1 = 456, 304, 564                # protect: red IS badge top

    win = a[y0:y1, x0:x1].astype(np.int32)
    r, g, b = win[:, :, 0], win[:, :, 1], win[:, :, 2]

    gold = (r > 185) & (g > 125) & (b < 150)               # letter fill/bevel
    dark = (r < 115) & (g < 95) & (b < 85)                 # letter outline
    near_gold = _dilate(gold, 7)
    text = gold | (dark & near_gold)
    sky = b > r + 30
    fill_mask = _dilate(text, 2) & ~sky

    # protect the IS badge region (window coords)
    yy, xx = np.mgrid[y0:y1, x0:x1]
    protect = (xx >= px0) & (xx <= px1) & (yy >= py0)
    fill_mask &= ~protect

    # per-row wood tone = median of row pixels that are plain plank
    wood_ok = ~text & ~sky & ~protect
    prev = np.array([150, 95, 45], dtype=np.int32)         # plank fallback
    for j in range(win.shape[0]):
        rowpix = win[j][wood_ok[j]]
        if len(rowpix) >= 30:
            prev = np.median(rowpix, axis=0).astype(np.int32)
        idx = np.where(fill_mask[j])[0]
        for i in idx:
            n = _noise(x0 + i, y0 + j)
            a[y0 + j, x0 + i] = np.clip(prev + n, 0, 255)


def blank_pill_text(a, rect, margins):
    """Refill a pill interior row-by-row from its text-free margin pixels.

    rect    = (x0, y0, x1, y1) pill interior (inside the gold frame)
    margins = list of (mx0, mx1) column spans known to be text-free
    """
    x0, y0, x1, y1 = rect
    for y in range(y0, y1):
        row = a[y, x0:x1].astype(np.int32)
        sample = np.concatenate([a[y, m0:m1] for (m0, m1) in margins]).astype(np.int32)
        bg = np.median(sample, axis=0).astype(np.int32)
        dist = np.abs(row - bg).sum(axis=1)
        for i in np.where(dist > 110)[0]:
            n = _noise(x0 + i, y)
            a[y, x0 + i] = np.clip(bg + n, 0, 255)


def main():
    img = Image.open(SRC).convert("RGB")
    img = crop_white_edges(img)
    a = np.asarray(img, dtype=np.int32).copy()

    blank_ribbon_text(a)
    # KILLS pill: interior x 266..477, y 648..706; margins beside the text
    blank_pill_text(a, (268, 648, 476, 706), [(269, 286), (460, 474)])
    # DEATHS pill: interior x 538..740, y 648..706
    blank_pill_text(a, (540, 648, 740, 706), [(541, 553)])

    out = Image.fromarray(a.astype(np.uint8))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    out.save(OUT)
    print("wrote", OUT, out.size)


if __name__ == "__main__":
    main()
