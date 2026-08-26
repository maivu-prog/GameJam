# -*- coding: utf-8 -*-
"""Generate the "-rotten" variant of a fish sprite.

The three night species shipped without one. Day fish all have theirs, so a night catch left in the
hold past its twelve hours asked RuntimeUI for a sprite that does not exist and drew nothing.

The recipe is not invented -- it is measured off the existing pairs, which sit at a consistent
hue 0.13 (a sickly yellow-brown), a little over half the original saturation, and the same value.
Run with --check to reproduce a known rotten sprite and print how far off it lands.

    python tools/make_rotten_fish.py --check bream
    python tools/make_rotten_fish.py piranha night_shark kraken
"""
import argparse
import colorsys
import os
import sys

import numpy as np
from PIL import Image

SPECIES_DIR = "Assets/Resources/Art/fish/species"

ROT_HUE = 0.13        # where every existing rotten sprite lands
HUE_PULL = 0.85       # how far toward it, 1 = all the way
SAT_SCALE = 0.58      # measured 0.40 -> 0.24 on the day fish
VAL_SCALE = 1.00      # brightness is left alone


def rot(path):
    """Return the rotten version of an RGBA sprite, as a PIL image."""
    src = Image.open(path).convert("RGBA")
    a = np.asarray(src).astype(np.float32) / 255.0
    rgb, alpha = a[..., :3], a[..., 3]

    # Vectorised RGB->HSV. colorsys is per-pixel and far too slow for a 256x512 sheet.
    mx = rgb.max(-1)
    mn = rgb.min(-1)
    diff = mx - mn
    v = mx
    s = np.where(mx > 0, diff / np.maximum(mx, 1e-6), 0.0)

    h = np.zeros_like(mx)
    nz = diff > 1e-6
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    idx = nz & (mx == r)
    h[idx] = ((g - b)[idx] / diff[idx]) % 6.0
    idx = nz & (mx == g)
    h[idx] = ((b - r)[idx] / diff[idx]) + 2.0
    idx = nz & (mx == b)
    h[idx] = ((r - g)[idx] / diff[idx]) + 4.0
    h = h / 6.0

    # Pull the hue round the shorter way, so blues do not sweep through the whole wheel.
    delta = (ROT_HUE - h + 0.5) % 1.0 - 0.5
    h = (h + delta * HUE_PULL) % 1.0
    s = np.clip(s * SAT_SCALE, 0.0, 1.0)
    v = np.clip(v * VAL_SCALE, 0.0, 1.0)

    i = np.floor(h * 6.0)
    f = h * 6.0 - i
    p, q, t = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    i = i.astype(np.int32) % 6
    # The conditions need the trailing channel axis or they cannot broadcast against the RGB choices.
    sel = [(i == k)[..., None] for k in range(6)]
    out = np.select(
        sel,
        [np.stack([v, t, p], -1), np.stack([q, v, p], -1), np.stack([p, v, t], -1),
         np.stack([p, q, v], -1), np.stack([t, p, v], -1), np.stack([v, p, q], -1)])

    out = np.concatenate([out, alpha[..., None]], -1)
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8), "RGBA")


def mean_hsv(img):
    a = np.asarray(img.convert("RGBA")).astype(np.float32) / 255.0
    m = a[..., 3] > 0.125
    rgb = a[..., :3][m][::37]
    hsv = np.array([colorsys.rgb_to_hsv(*c) for c in rgb])
    return hsv.mean(0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("species", nargs="+")
    ap.add_argument("--check", action="store_true",
                    help="reproduce an EXISTING rotten sprite and report the error instead of writing")
    args = ap.parse_args()

    for name in args.species:
        src = os.path.join(SPECIES_DIR, name + ".png")
        dst = os.path.join(SPECIES_DIR, name + "-rotten.png")
        if not os.path.exists(src):
            print("missing source: %s" % src)
            continue

        made = rot(src)

        if args.check:
            if not os.path.exists(dst):
                print("%-14s no existing rotten sprite to check against" % name)
                continue
            want = mean_hsv(Image.open(dst))
            got = mean_hsv(made)
            print("%-14s want H%.2f S%.2f V%.2f   got H%.2f S%.2f V%.2f   err %.3f/%.3f/%.3f"
                  % (name, want[0], want[1], want[2], got[0], got[1], got[2],
                     abs(want[0] - got[0]), abs(want[1] - got[1]), abs(want[2] - got[2])))
            continue

        if os.path.exists(dst):
            print("%-14s already has one, left alone" % name)
            continue
        made.save(dst)
        print("%-14s wrote %s" % (name, dst))


if __name__ == "__main__":
    sys.exit(main())
