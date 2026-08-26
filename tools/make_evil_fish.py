"""Generate the night-time "evil" creature sprites.

Rather than draw new art in a different hand, these are derived from the existing painted fish: the
body is pushed down to a cold near-silhouette so it reads as a shape in dark water, and the eye —
which every fish sprite already has as a big pale disc near the head — is repainted as a red glow.
The band-C creature is a tentacle cropped straight out of the sea-monster painting.

Run:  python tools/make_evil_fish.py
Out:  Assets/Resources/Art/fish/species/{piranha,night_shark,kraken}.png
"""

import os
import numpy as np
from PIL import Image, ImageFilter

SPECIES = "Assets/Resources/Art/fish/species"
MONSTER = "Assets/Resources/Art/sea-monster-encounter.png"


def load(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.float32) / 255.0


def find_eye(rgb, alpha):
    """The eye is the brightest opaque blob in the front (left) quarter — every sprite faces -x."""
    h, w = alpha.shape
    lum = rgb @ np.float32([.299, .587, .114])
    mask = (alpha > .6).astype(np.float32)
    head = np.zeros_like(lum)
    head[:, : int(w * .3)] = 1.0
    score = lum * mask * head
    score = np.asarray(Image.fromarray((score * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(4)))
    idx = int(np.argmax(score))
    return idx % w, idx // w


def evilify(src, body_tint, eye_radius, darkness=.62, eye_colour=(1.0, .16, .10)):
    a = load(os.path.join(SPECIES, src + ".png"))
    rgb, alpha = a[..., :3], a[..., 3]
    ex, ey = find_eye(rgb, alpha)

    # Body: crush the value range down and pull the hue toward the tint, keeping the painted texture.
    lum = (rgb @ np.float32([.299, .587, .114]))[..., None]
    body = np.float32(body_tint)[None, None, :] * (0.35 + 0.65 * lum)
    out = rgb * (1 - darkness) + body * darkness

    # Eye: a hard red pupil inside a soft glow, drawn over whatever was there.
    h, w = alpha.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.hypot(xx - ex, yy - ey)
    pupil = np.clip(1 - (d - eye_radius * .55) / (eye_radius * .35), 0, 1)
    glow = np.exp(-(d / (eye_radius * 1.5)) ** 2)
    eye = np.clip(pupil + glow * .75, 0, 1)[..., None]
    out = out * (1 - eye) + np.float32(eye_colour)[None, None, :] * eye
    # The glow spills a little past the silhouette, which is what sells it as light rather than paint.
    alpha = np.clip(alpha + glow * .45, 0, 1)
    return np.clip(out, 0, 1), alpha


def save(name, rgb, alpha):
    rgba = (np.dstack([rgb, alpha]) * 255).astype(np.uint8)
    path = os.path.join(SPECIES, name + ".png")
    Image.fromarray(rgba, "RGBA").save(path)
    print(f"{name:14s} {rgba.shape[1]}x{rgba.shape[0]}")


def tentacle(w=420, h=560):
    """Draw the band-C creature: one colossal tentacle.

    The painting does contain tentacles, but the ones in frame are rim-lit rather than dark — they sit
    within ~0.03 luminance of the water around them, so no colour key pulls a clean silhouette out. It
    is more reliable to draw the shape outright and match the palette by hand.

    Built as a distance field around a curled spine: radius tapers to the tip, suckers ride the inside
    of the curl, and a red rim light marks it as the hostile one (a tentacle has no eye to light up).
    """
    t = np.linspace(0, 1, 400)
    # Spine: rises from the bottom, leans out, then curls back over itself.
    cx = w * (.46 + .30 * np.sin(t * 2.4) - .16 * t ** 2)
    cy = h * (.99 - .94 * t + .08 * np.sin(t * 3.2))       # leaves headroom so the tip is not clipped
    radius = (w * .165) * (1 - t) ** .75 + w * .012        # thick base, whip-thin tip

    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    dist = np.full((h, w), 1e9, np.float32)
    near = np.zeros((h, w), np.float32)
    for i in range(len(t)):
        d = np.hypot(xx - cx[i], yy - cy[i]) - radius[i]
        closer = d < dist
        dist = np.where(closer, d, dist)
        near = np.where(closer, t[i], near)                # remember WHERE along the spine, for shading

    alpha = np.clip(-dist / 2.5, 0, 1)
    alpha = alpha * alpha * (3 - 2 * alpha)

    # Body: near-black with a teal bias, lighter along the spine so it reads as a round tube.
    lit = np.clip(1 - np.abs(dist) / (w * .16), 0, 1) ** 1.6
    rgb = np.zeros((h, w, 3), np.float32)
    rgb[..., 0] = .055 + .10 * lit + .05 * near
    rgb[..., 1] = .085 + .15 * lit + .04 * near
    rgb[..., 2] = .095 + .17 * lit + .03 * near

    # Suckers: discs down the inner edge, scaled to the local thickness.
    rng = np.random.default_rng(4)
    for i in range(6, len(t) - 8, 13):
        rr = radius[i] * .30
        if rr < 2.2: continue
        nx, ny = cx[i + 4] - cx[i - 4], cy[i + 4] - cy[i - 4]
        n = np.hypot(nx, ny) + 1e-5
        # +normal puts them on the INSIDE of the curl, where a real tentacle grips.
        rr *= rng.uniform(.82, 1.15)
        px, py = cx[i] + ny / n * radius[i] * .42, cy[i] - nx / n * radius[i] * .42
        d = np.hypot(xx - px, yy - py)
        disc = np.clip(1 - (d - rr) / 2.0, 0, 1) * alpha
        ring = np.clip(1 - np.abs(d - rr) / 2.2, 0, 1) * alpha
        shade = rng.uniform(.75, 1.15)
        rgb += (np.float32([.26, .23, .15])[None, None, :] * disc[..., None] * .5 * shade
                + np.float32([.04, .04, .05])[None, None, :] * ring[..., None] * .55)

    # Painterly grain, then the red rim.
    grain = rng.normal(0, .022, (h, w, 1)).astype(np.float32)
    rgb = rgb + grain * alpha[..., None]
    # Rim only on the outside of the curl, and stronger toward the tip — a light source, not an outline.
    rim = np.clip(1 - np.abs(dist + 1.5) / 3.2, 0, 1) * alpha
    outer = np.clip((xx - np.interp(near, t, cx)) / (w * .10), 0, 1)
    rim *= outer * (.35 + .65 * near)
    rgb = rgb * (1 - rim[..., None] * .8) + np.float32([.86, .15, .09])[None, None, :] * (rim[..., None] * .8)

    save("kraken", np.clip(rgb, 0, 1), alpha)
    print(f"  aspect = {w / h:.3f}  (dat vao FishDef.aspect)")


def main():
    # Band A: a small, fast shoal fish. The barracuda's lean silhouette reads as piranha at size.
    rgb, a = evilify("barracuda", (.30, .10, .12), eye_radius=13)
    save("piranha", rgb, a)

    # Band B: the big one. Ghost tuna has the torpedo body and crescent tail a shark needs.
    rgb, a = evilify("ghost_tuna", (.16, .17, .22), eye_radius=16, darkness=.70)
    save("night_shark", rgb, a)

    # Band C: not a fish at all to look at — a single colossal tentacle. Behaviour is identical to the
    # other hunters; only the skin and the scale change.
    tentacle()


if __name__ == "__main__":
    main()
