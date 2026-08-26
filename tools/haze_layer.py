"""Push a parallax layer back in depth by hazing it toward the sky behind it.

Aerial perspective: distant things lose contrast and drift toward the colour of the air between you
and them. Here that "air" is the `world-base` layer, sampled row by row, so the top of a layer hazes
toward the pale sky and the bottom toward the teal water — which a flat brightness/contrast tweak
cannot do.

The first run copies the untouched PNG to `../ArtSource/Parallax-originals/`, and every run reads
that copy, so re-running with a different strength never compounds.

Run:  python tools/haze_layer.py shore-near 0.32 0.88
      python tools/haze_layer.py <layer> [blend 0..1] [alpha multiplier]
"""

import os
import shutil
import sys

import numpy as np
from PIL import Image

LAYERS = "Assets/Resources/Art/World/Parallax"
BACKUP = "../ArtSource/Parallax-originals"
ATMOSPHERE = "world-base"


def source(name):
    """The pristine PNG, copied aside on first use so this script is always re-runnable."""
    os.makedirs(BACKUP, exist_ok=True)
    live = os.path.join(LAYERS, name + ".png")
    kept = os.path.join(BACKUP, name + ".png")
    if not os.path.exists(kept):
        shutil.copy2(live, kept)
        print(f"backed up original -> {kept}")
    return kept


def main():
    name = sys.argv[1] if len(sys.argv) > 1 else "shore-near"
    blend = float(sys.argv[2]) if len(sys.argv) > 2 else 0.32
    alpha_mul = float(sys.argv[3]) if len(sys.argv) > 3 else 0.88

    im = Image.open(source(name)).convert("RGBA")
    a = np.asarray(im).astype(np.float32) / 255.0
    rgb, alpha = a[..., :3], a[..., 3]

    # Row-by-row colour of what sits behind this layer.
    base = np.asarray(Image.open(source(ATMOSPHERE)).convert("RGB")).astype(np.float32) / 255.0
    if base.shape[0] != rgb.shape[0]:
        base = np.asarray(Image.open(source(ATMOSPHERE)).convert("RGB")
                          .resize((rgb.shape[1], rgb.shape[0]), Image.LANCZOS)).astype(np.float32) / 255.0
    haze = base.mean(axis=1, keepdims=True)          # (rows, 1, 3)

    out = rgb * (1 - blend) + haze * blend
    # Distance also flattens contrast, so ease the darkest values up a little more than the light ones.
    lum = out @ np.float32([.299, .587, .114])
    out += (haze - out) * (blend * .45) * (1 - lum)[..., None]

    rgba = np.dstack([np.clip(out, 0, 1), np.clip(alpha * alpha_mul, 0, 1)])
    Image.fromarray((rgba * 255).astype(np.uint8), "RGBA").save(os.path.join(LAYERS, name + ".png"))
    print(f"{name}: blend={blend} alpha x{alpha_mul}")


if __name__ == "__main__":
    main()
