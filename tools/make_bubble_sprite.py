"""Generate the hook's bubble sprite.

A bubble underwater reads almost entirely as a bright rim plus one specular highlight — the middle
is nearly the colour of the water behind it. So this draws a soft annulus with a faint interior and
a small off-centre highlight, rather than a filled disc, which would look like a pebble.

Run:  python tools/make_bubble_sprite.py
Out:  Assets/Resources/Art/UI/Gameplay/bubble.png
"""

import os
import numpy as np
from PIL import Image

OUT = "Assets/Resources/Art/UI/Gameplay/bubble.png"
SIZE = 128


def main():
    n = SIZE
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    cx = cy = (n - 1) / 2
    r = np.hypot(x - cx, y - cy) / (n / 2)          # 0 at centre, 1 at the edge

    rim = np.exp(-((r - 0.82) / 0.085) ** 2)        # the bright outline
    interior = np.clip(1 - r / 0.86, 0, 1) ** 2.2 * 0.13
    # Specular highlight, up and to the left, the way the scene's light falls in the painting.
    hl = np.exp(-(((x - cx * 0.62) / (n * 0.075)) ** 2 + ((y - cy * 0.60) / (n * 0.075)) ** 2))

    alpha = np.clip(rim * 0.88 + interior + hl * 0.75, 0, 1)
    alpha *= np.clip(1 - (r - 0.94) / 0.06, 0, 1)   # hard-clip anything past the edge so it stays round

    # Cool near-white; the layer tint in game pulls it toward whatever water it is sitting in.
    rgb = np.zeros((n, n, 3), np.float32)
    rgb[..., 0] = 0.82
    rgb[..., 1] = 0.93
    rgb[..., 2] = 0.95

    rgba = (np.dstack([rgb, alpha]) * 255).astype(np.uint8)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(OUT)
    print(f"{OUT}  {n}x{n}")


if __name__ == "__main__":
    main()
