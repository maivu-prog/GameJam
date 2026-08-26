"""Generate the cast-timer ring that wraps the fishing joystick.

A plain annulus, white, with soft inner/outer edges. It is drawn twice in game: once at low alpha as
the full track, and once as a Radial360 Filled image that empties as the line runs out. Keeping it
white means the fill colour can be driven entirely from code (calm -> red near the end).

Run:  python tools/make_timer_ring_sprite.py
Out:  Assets/Resources/Art/UI/Gameplay/timer-ring.png
"""

import os
import numpy as np
from PIL import Image

OUT = "Assets/Resources/Art/UI/Gameplay/timer-ring.png"
SIZE = 512
INNER, OUTER = 0.815, 0.935      # ring radii, as a fraction of the half-size
FEATHER = 0.016                  # edge softness, same units


def main():
    n = SIZE
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    c = (n - 1) / 2
    r = np.hypot(x - c, y - c) / (n / 2)

    # Smooth step in at INNER and out at OUTER; the product is the ring.
    inner = np.clip((r - INNER) / FEATHER, 0, 1)
    outer = np.clip((OUTER - r) / FEATHER, 0, 1)
    alpha = inner * outer
    alpha = alpha * alpha * (3 - 2 * alpha)      # smoothstep, so the edges are not linear ramps

    rgba = np.dstack([np.ones((n, n, 3), np.float32), alpha])
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray((rgba * 255).astype(np.uint8), "RGBA").save(OUT)
    print(f"{OUT}  {n}x{n}  ring {INNER}..{OUTER}")


if __name__ == "__main__":
    main()
