"""Generate the night-time safe-zone halo drawn around each harbour.

A soft elliptical lantern glow: brightest at the quay, falling off to nothing at the edge of the
dock radius, with a slightly firmer rim so the boundary the fish will not cross is legible rather
than a vague smudge.

Run:  python tools/make_safe_halo.py
Out:  Assets/Resources/Art/UI/Gameplay/safe-halo.png
"""

import os
import numpy as np
from PIL import Image

OUT = "Assets/Resources/Art/UI/Gameplay/safe-halo.png"
W, H = 512, 512


def main():
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    # A plain CIRCLE. It used to multiply the y term by .78, which stretches the ellipse to +-328px
    # inside a 512px canvas — so the top and bottom were sliced off by the canvas edge and the result
    # read as squashed vertically, the opposite of the intent. The on-screen aspect is set by the
    # RectTransform (haloHeightMul in the Inspector); the sprite has no business also squashing it.
    r = np.hypot((xx - (W - 1) / 2) / (W / 2), (yy - (H - 1) / 2) / (H / 2))

    body = np.clip(1 - r, 0, 1) ** 2.2 * .55          # the fill
    rim = np.exp(-((r - .88) / .07) ** 2) * .5        # the boundary line itself
    alpha = np.clip(body + rim, 0, 1)
    alpha[r > 1] = 0

    # Warm lantern light, cooling slightly toward the rim so it sits in the teal water.
    rgb = np.zeros((H, W, 3), np.float32)
    rgb[..., 0] = .98 - .18 * r
    rgb[..., 1] = .84 - .10 * r
    rgb[..., 2] = .55 + .12 * r

    rgba = (np.dstack([np.clip(rgb, 0, 1), alpha]) * 255).astype(np.uint8)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(OUT)
    print(f"{OUT}  {W}x{H}")


if __name__ == "__main__":
    main()
