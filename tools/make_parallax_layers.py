"""Derive the parallax backdrop layers from the single painted sea backdrop.

`Assets/Resources/Art/fishing-world-backdrop.png` is one 1536x1024 painting holding the whole
scene: sky, waterfront town, wave line, water column and seabed. This script cuts it into the
horizontally-tiling layers that `SeaParallax` scrolls at different speeds, so the depth cues come
from the original art instead of from a new, mismatched art pass.

Run:  python tools/make_parallax_layers.py
Out:  Assets/Resources/Art/parallax/*.png
"""

import os
import numpy as np
from PIL import Image, ImageFilter

SRC = "Assets/Resources/Art/fishing-world-backdrop.png"
OUT = "Assets/Resources/Art/parallax"

# The painting's own landmarks, in source pixels.
WAVE_TOP, WAVE_BOT = 338, 392          # the foam/wave band that reads as the water line
CROP_X0, CROP_X1 = 64, 1472            # drop the painted vignette at the left/right edges
FEATHER = 160                          # cross-fade width used to close the tiling seam

rng = np.random.default_rng(7)


def load():
    return np.asarray(Image.open(SRC).convert("RGB")).astype(np.float32) / 255.0


def deflicker_columns(band):
    """Flatten the painting's left/right vignette so a tiled copy has no dark bands."""
    col = band.mean(axis=(0, 2))                                  # per-column brightness
    smooth = np.convolve(np.pad(col, 96, mode="edge"), np.ones(193) / 193, "same")[96:-96]
    gain = np.clip(col.mean() / np.maximum(smooth, 1e-3), 0.55, 1.9)
    return np.clip(band * gain[None, :, None], 0, 1)


def seamless(band):
    """Cross-fade the right edge back over the left one so the tile wraps invisibly."""
    w = band.shape[1] - FEATHER
    ramp = np.linspace(0, 1, FEATHER, dtype=np.float32)[None, :, None]
    out = band[:, :w].copy()
    out[:, :FEATHER] = band[:, :FEATHER] * ramp + band[:, w:w + FEATHER] * (1 - ramp)
    return out


def crop(a, y0, y1):
    return deflicker_columns(a[y0:y1, CROP_X0:CROP_X1])


def smoothstep(x):
    x = np.clip(x, 0, 1)
    return x * x * (3 - 2 * x)


def sky_alpha(band):
    """Silhouette mask for the waterfront: the sky is bright cream, the structures are dark."""
    lum = band @ np.float32([.299, .587, .114])
    hi, lo = np.percentile(lum, 88), np.percentile(lum, 8)
    return smoothstep((hi - lum) / max(hi - lo, 1e-3))


def water_alpha(band, floor=0.030, span=0.11):
    """Silhouette mask underwater: everything that is not the row's teal water colour."""
    # Per-row water colour = the most common (median) colour of that row; rock/kelp are outliers.
    base = np.median(band, axis=1, keepdims=True)
    dist = np.linalg.norm(band - base, axis=2)
    return smoothstep((dist - floor) / span)


def solidify(alpha, blur_px=2.5, gain=1.7, gamma=0.62):
    """The painting renders rock and kelp as speckled texture, so a raw colour-distance mask comes
    out as noise. Blur it into masses, then push the midtones up so they read as solid shapes."""
    a = Image.fromarray((np.clip(alpha, 0, 1) * 255).astype(np.uint8))
    a = np.asarray(a.filter(ImageFilter.GaussianBlur(blur_px))).astype(np.float32) / 255.0
    return np.clip((a * gain) ** gamma, 0, 1)


def floor_ramp(h, start=0.55):
    """1.0 at the bottom of the band, fading out above it — keeps the seabed opaque underfoot."""
    y = np.linspace(0, 1, h, dtype=np.float32)
    return smoothstep((y - start) / (1 - start))[:, None]   # column vector, broadcasts over width


def fade_top(alpha, px):
    """Ramp the top rows to zero. Without it a silhouette that touches the top of its band ends in a
    hard horizontal line across the water once the strip is placed on screen."""
    a = alpha.copy()
    n = min(px, a.shape[0])
    a[:n] *= smoothstep(np.linspace(0, 1, n, dtype=np.float32))[:, None]
    return a


def tint(band, colour, amount):
    return band * (1 - amount) + np.float32(colour)[None, None, :] * amount


def blur(arr_rgba, radius):
    im = Image.fromarray(arr_rgba)
    return np.asarray(im.filter(ImageFilter.GaussianBlur(radius)))


def save(name, rgb, alpha=None, scale=1.0, blur_px=0.0):
    rgb = np.clip(rgb, 0, 1)
    if alpha is None:
        alpha = np.ones(rgb.shape[:2], dtype=np.float32)
    rgba = np.dstack([rgb, np.clip(alpha, 0, 1)])
    rgba = (rgba * 255).astype(np.uint8)
    if blur_px > 0:
        rgba = blur(rgba, blur_px)
    im = Image.fromarray(rgba, "RGBA")
    if scale != 1.0:
        im = im.resize((int(im.width * scale), int(im.height * scale)), Image.LANCZOS)
    path = os.path.join(OUT, name + ".png")
    im.save(path)
    print(f"{name:16s} {im.width}x{im.height}")


def main():
    os.makedirs(OUT, exist_ok=True)
    a = load()

    # ---- above water -----------------------------------------------------------------
    # Sky: taken from the cloud stretch between the two pier clusters, then widened, so no
    # building edges bleed into the layer that is supposed to sit furthest back.
    sky = deflicker_columns(a[24:244, 500:1080])
    sky = seamless(np.repeat(sky, 3, axis=1)[:, :1408])
    # The strip only covers the horizon band. Extend it upward with its own top row, darkened, so the
    # layer fills the whole above-water area without being stretched 3x vertically in game.
    # Average the top rows and smooth them along x first — repeating a single painted row verbatim
    # turns its per-column noise into vertical streaks once it is stretched over 250px.
    top = sky[:16].mean(axis=0)                                   # (width, 3)
    k = np.ones(201, np.float32) / 201
    top = np.stack([np.convolve(np.pad(top[:, c], 100, "wrap"), k, "same")[100:-100] for c in range(3)], 1)
    cap = np.repeat(top[None, :, :], 250, axis=0)
    cap *= np.linspace(0.42, 1.0, 250, dtype=np.float32)[:, None, None]
    save("sky", np.vstack([cap, sky]))

    # Far town: the whole waterfront, cut to a silhouette, pushed toward the sky colour and
    # blurred a touch so it reads as distance rather than as a second copy of the near piers.
    town = crop(a, 92, WAVE_TOP + 32)
    ta = sky_alpha(town)
    town_far = tint(town, (0.62, 0.60, 0.50), 0.42)
    save("town-far", seamless(town_far), seamless(ta[..., None])[..., 0], scale=0.75, blur_px=1.6)

    # Near piers: only the posts/decking band right above the water, darkened for contrast.
    piers = crop(a, 236, WAVE_TOP + 26)
    pa = sky_alpha(piers) ** 0.8
    save("piers-near", seamless(tint(piers, (0.10, 0.13, 0.14), 0.30)), seamless(pa[..., None])[..., 0])

    # Wave line: opaque strip, scrolls 1:1 with the world so the boat sits in it.
    save("surface", seamless(crop(a, WAVE_TOP - 14, WAVE_BOT + 26)))

    # ---- below water -----------------------------------------------------------------
    # Water body: the column's vertical gradient with the horizontal detail averaged out, so it
    # can sit still behind everything without any tiling artefact.
    col = a[WAVE_BOT:1010, CROP_X0:CROP_X1].mean(axis=1)              # (rows, 3)
    col = np.clip(col * 1.06, 0, 1)
    save("water", np.repeat(col[:, None, :], 64, axis=1))

    # Deep murk: the rock band, blurred to shapeless silhouettes, for the slowest underwater layer.
    murk = crop(a, 620, 960)
    ma = solidify(water_alpha(murk, 0.045, 0.14), blur_px=7.0, gain=2.2, gamma=0.8) * 0.62
    ma = fade_top(np.maximum(ma, floor_ramp(murk.shape[0], 0.78) * 0.62), 90)
    save("murk-deep", seamless(tint(murk, (0.04, 0.12, 0.12), 0.58)), seamless(ma[..., None])[..., 0],
         scale=0.6)

    # Seabed: rocks + coral silhouette along the floor.
    bed = crop(a, 655, 1016)
    ba = solidify(water_alpha(bed), blur_px=3.0, gain=1.9, gamma=0.55)
    ba = fade_top(np.maximum(ba, floor_ramp(bed.shape[0], 0.62)), 70)   # floor solid, top blends into water
    save("seabed", seamless(tint(bed, (0.06, 0.14, 0.13), 0.18)), seamless(ba[..., None])[..., 0])

    # Kelp: the tall strands only — they start well above the rock line, so cut the band high and
    # keep just the olive pixels (kelp is warmer than both the water and the grey rock).
    kelp = crop(a, 400, 960)
    warmth = kelp[..., 0] + kelp[..., 1] - 2 * kelp[..., 2]
    ka = solidify(water_alpha(kelp, 0.022, 0.09) * smoothstep((warmth + 0.02) / 0.10),
                  blur_px=2.0, gain=2.4, gamma=0.5)
    ka = fade_top(ka, 80)
    save("kelp-near", seamless(tint(kelp, (0.10, 0.12, 0.06), 0.15)), seamless(ka[..., None])[..., 0])

    # Motes: procedural drifting specks for the closest underwater layer (nothing to derive here).
    h, w = 512, 1248
    m = np.zeros((h, w), np.float32)
    ys, xs = rng.integers(0, h, 260), rng.integers(0, w, 260)
    m[ys, xs] = rng.uniform(0.35, 1.0, 260)
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.6)))
    m = np.clip(m.astype(np.float32) / 255.0 * 3.4, 0, 1)
    m[:, :FEATHER] *= np.linspace(0, 1, FEATHER, dtype=np.float32)[None, :]   # fade the seam out
    m = fade_top(m, 110)
    save("motes", np.ones((h, w, 3), np.float32) * 0.86, m)


if __name__ == "__main__":
    main()
