"""Apply the supplied ReskinArt pixel-art pack to every active fishing-game PNG.

The script deliberately keeps each destination PNG's dimensions and path so Unity
retains the existing .meta GUIDs, scene references, prefab references, and layout.
Run from the Unity project root with the bundled Codex Python runtime.
"""

from __future__ import annotations

from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw, ImageEnhance, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT.parent / "ReskinArt"
ART = ROOT / "Assets" / "Resources" / "Art"
QA = ROOT / "Logs" / "reskin-contact-sheet.png"

PALETTE = {
    "ink": (38, 28, 45, 255),
    "deep": (40, 48, 90, 255),
    "water": (48, 110, 171, 255),
    "foam": (130, 203, 222, 255),
    "cream": (234, 218, 184, 255),
    "paper": (220, 189, 164, 255),
    "wood": (118, 62, 65, 255),
    "red": (157, 64, 70, 255),
    "green": (64, 124, 109, 255),
    "gold": (221, 168, 77, 255),
}


def src(relative: str) -> Image.Image:
    return Image.open(SOURCE / relative).convert("RGBA")


def crop(relative: str, box: tuple[int, int, int, int]) -> Image.Image:
    return src(relative).crop(box)


def frame(relative: str, frame_width: int, index: int = 0) -> Image.Image:
    sheet = src(relative)
    count = max(1, sheet.width // frame_width)
    index %= count
    return sheet.crop((index * frame_width, 0, (index + 1) * frame_width, sheet.height))


def tint(image: Image.Image, color: tuple[int, int, int], strength: float) -> Image.Image:
    base = image.convert("RGBA")
    alpha = base.getchannel("A")
    gray = ImageOps.grayscale(base)
    colored = ImageOps.colorize(gray, black=(18, 16, 24), white=color).convert("RGBA")
    colored.putalpha(alpha)
    return Image.blend(base, colored, max(0.0, min(1.0, strength)))


def nearest_contain(image: Image.Image, size: tuple[int, int], pad: float = 0.08) -> Image.Image:
    w, h = size
    room = (max(1, int(w * (1 - pad * 2))), max(1, int(h * (1 - pad * 2))))
    fitted = ImageOps.contain(image, room, method=Image.Resampling.NEAREST)
    out = Image.new("RGBA", size)
    out.alpha_composite(fitted, ((w - fitted.width) // 2, (h - fitted.height) // 2))
    return out


def nearest_cover(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    return ImageOps.fit(image, size, method=Image.Resampling.NEAREST, centering=(0.5, 0.5))


def nine_slice(tile: Image.Image, size: tuple[int, int], border: int = 4) -> Image.Image:
    """Stretch a small pixel-art tile while keeping its border thickness coherent."""
    w, h = size
    logical_scale = max(1, min(w, h) // 72)
    lw, lh = max(border * 2 + 1, w // logical_scale), max(border * 2 + 1, h // logical_scale)
    tile = tile.convert("RGBA")
    tw, th = tile.size
    b = min(border, tw // 2 - 1, th // 2 - 1)
    logical = Image.new("RGBA", (lw, lh))
    xs = (0, b, tw - b, tw)
    ys = (0, b, th - b, th)
    dx = (0, b, lw - b, lw)
    dy = (0, b, lh - b, lh)
    for yi in range(3):
        for xi in range(3):
            piece = tile.crop((xs[xi], ys[yi], xs[xi + 1], ys[yi + 1]))
            target = (max(1, dx[xi + 1] - dx[xi]), max(1, dy[yi + 1] - dy[yi]))
            piece = piece.resize(target, Image.Resampling.NEAREST)
            logical.alpha_composite(piece, (dx[xi], dy[yi]))
    return logical.resize(size, Image.Resampling.NEAREST)


def panel(size: tuple[int, int], variant: int = 0) -> Image.Image:
    sheet = src("Freebuttons/UiCozyFree.png")
    options = [sheet.crop((8, 168, 96, 253)), sheet.crop((103, 168, 159, 253)), sheet.crop((168, 168, 212, 253))]
    return nine_slice(options[variant % len(options)], size, 8)


def button(size: tuple[int, int], variant: int = 0) -> Image.Image:
    sheet = src("Freebuttons/LongButtons.png" if variant % 2 == 0 else "Freebuttons/LongButtons2.png")
    x = (variant % 4) * 16
    y = ((variant // 2) % 3) * 16
    return nine_slice(sheet.crop((x, y, x + 16, y + 16)), size, 3)


def icon(index: int) -> Image.Image:
    names = ["Icons_01.png", "Icons_02.png", "Icons_03.png", "Icons_06.png", "Icons_18.png"]
    if index < len(names):
        return src(f"4 Icons/{names[index]}")
    storage = ["Icons_16.png", "Icons_17.png", "Icons_19.png", "Icons_20.png"]
    return src(f"Storage/{storage[(index - len(names)) % len(storage)]}")


def draw_pixel_circle(size: tuple[int, int], fill, outline=None, thickness: int = 3) -> Image.Image:
    w, h = size
    logical = max(24, min(128, min(w, h) // max(1, min(w, h) // 96)))
    base = Image.new("RGBA", (logical, logical))
    d = ImageDraw.Draw(base)
    inset = max(2, logical // 18)
    d.ellipse((inset, inset, logical - inset - 1, logical - inset - 1), fill=fill, outline=outline, width=max(1, thickness))
    fitted = base.resize((min(w, h), min(w, h)), Image.Resampling.NEAREST)
    out = Image.new("RGBA", size)
    out.alpha_composite(fitted, ((w - fitted.width) // 2, (h - fitted.height) // 2))
    return out


def environment(size: tuple[int, int], variant: int = 0, underwater: bool = False) -> Image.Image:
    w, h = size
    out = Image.new("RGBA", size, PALETTE["deep"] if underwater else (91, 151, 181, 255))
    d = ImageDraw.Draw(out)
    horizon = int(h * (0.18 if underwater else 0.38))
    if not underwater:
        d.rectangle((0, 0, w, horizon), fill=(123, 180, 196, 255))
        d.rectangle((0, horizon, w, h), fill=PALETTE["water"])
    water = src("Boat-harbos-water/Water.png")
    scale = max(1, min(w, h) // 320)
    water = water.resize((water.width * scale, water.height * scale), Image.Resampling.NEAREST)
    water = tint(water, (55 + variant * 12, 126, 178 - variant * 8), 0.25 + variant * 0.08)
    for y in range(horizon, h, water.height):
        for x in range(-(variant * water.width // 3), w, water.width):
            out.alpha_composite(water, (x, y))
    if underwater:
        shade = Image.new("RGBA", size, (18, 28, 65, min(145, 65 + variant * 25)))
        out = Image.alpha_composite(out, shade)
    return out


def harbor_scene(size: tuple[int, int], variant: int = 0, shipyard: bool = False) -> Image.Image:
    out = environment(size, variant)
    w, h = size
    hut = tint(src("Boat-harbos-water/Fishing_hut.png"), (185, 91 + variant * 18, 94), variant * 0.12)
    pier = src("Boat-harbos-water/Pier_Tiles.png")
    boat = tint(src("Boat-harbos-water/Boat.png"), (210, 120 + variant * 18, 90), variant * 0.15)
    hut_fit = ImageOps.contain(hut, (int(w * 0.88), int(h * 0.52)), Image.Resampling.NEAREST)
    boat_fit = ImageOps.contain(boat, (int(w * 0.42), int(h * 0.18)), Image.Resampling.NEAREST)
    pier_fit = ImageOps.contain(pier, (int(w * 0.72), int(h * 0.32)), Image.Resampling.NEAREST)
    out.alpha_composite(hut_fit, ((w - hut_fit.width) // 2, int(h * (0.18 if shipyard else 0.12))))
    out.alpha_composite(pier_fit, ((w - pier_fit.width) // 2, int(h * 0.60)))
    out.alpha_composite(boat_fit, (int(w * 0.50), int(h * 0.60)))
    return out


def portrait(size: tuple[int, int], index: int) -> Image.Image:
    choices = [
        frame("Character/old man idle state no line-Sheet.png", 60, index),
        frame("Character/cast bobbin Sheet.png", 60, index),
        frame(f"Bosses/Band{'ABC'[index % 3]}/Idle.png", 96, index),
    ]
    bg = panel(size, index)
    fg = nearest_contain(choices[index % len(choices)], size, 0.15)
    bg.alpha_composite(fg)
    return bg


def creature(size: tuple[int, int], index: int, rotten: bool = False, boss: bool = False) -> Image.Image:
    if boss:
        band = "ABC"[index % 3]
        art = frame(f"Bosses/Band{band}/Idle.png", 96, index)
    else:
        group = index % 6 + 1
        art = frame(f"Octopus and Jellyfish/{group}/Idle.png", 48, index)
    if rotten:
        art = tint(ImageEnhance.Contrast(art).enhance(0.75), (117, 119, 83), 0.72)
    return nearest_contain(art, size, 0.08)


def hook(size: tuple[int, int], index: int) -> Image.Image:
    rod = src("Boat-harbos-water/Fish-rod.png")
    colors = [(145, 119, 89), (173, 112, 80), (199, 156, 75), (174, 186, 188), (124, 183, 207), (133, 89, 169), (217, 161, 70), (91, 201, 205)]
    return nearest_contain(tint(rod, colors[index % len(colors)], 0.62), size, 0.12)


def bar(size: tuple[int, int], fill=False, warning=False) -> Image.Image:
    if fill:
        color = PALETTE["red"] if warning else PALETTE["green"]
        out = Image.new("RGBA", size)
        d = ImageDraw.Draw(out)
        inset = max(2, min(size) // 10)
        d.rectangle((inset, inset, size[0] - inset - 1, size[1] - inset - 1), fill=color)
        return out
    return button(size, 1 if warning else 0)


def world_overlay(size: tuple[int, int], band: int) -> Image.Image:
    base = environment(size, band, underwater=True)
    alpha = Image.new("L", size, 0)
    d = ImageDraw.Draw(alpha)
    d.rectangle((0, int(size[1] * 0.22), size[0], size[1]), fill=80 + band * 35)
    base.putalpha(alpha)
    return base


def text_badge(size: tuple[int, int]) -> Image.Image:
    out = panel(size, 0)
    boat = nearest_contain(src("Boat-harbos-water/Boat.png"), (size[0], int(size[1] * 0.55)), 0.18)
    out.alpha_composite(boat, (0, int(size[1] * 0.03)))
    logical = Image.new("RGBA", (192, 40))
    d = ImageDraw.Draw(logical)
    font = ImageFont.load_default()
    label = "RUSTY FISHING"
    box = d.textbbox((0, 0), label, font=font)
    d.text(((192 - (box[2] - box[0])) // 2, 12), label, font=font, fill=PALETTE["cream"], stroke_width=1, stroke_fill=PALETTE["ink"])
    logical = logical.resize((size[0], int(size[1] * 0.28)), Image.Resampling.NEAREST)
    out.alpha_composite(logical, (0, int(size[1] * 0.66)))
    return out


def render(relative: str, size: tuple[int, int]) -> Image.Image:
    p = relative.replace("\\", "/")
    low = p.lower()

    if p == "fishing-world-backdrop.png":
        return harbor_scene(size, 0)
    if p == "rusty-fishing-title-logo.png":
        return text_badge(size)
    if p == "whispering-harbor.png":
        return harbor_scene(size, 2)
    if p == "sea-monster-encounter.png":
        return creature(size, 2, boss=True)

    if low.startswith("characters/narrative/"):
        names = ["drowned", "elias", "keeper", "mara", "nell", "silas"]
        return portrait(size, next((i for i, n in enumerate(names) if n in low), 0))

    if low.startswith("fish/species/"):
        name = Path(p).stem.replace("-rotten", "")
        species = ["sardine", "mackerel", "bream", "red_snapper", "black_grouper", "piranha", "barracuda", "lanternfish", "ghost_tuna", "anglerfish", "night_shark", "kraken"]
        idx = species.index(name) if name in species else 0
        return creature(size, idx, "-rotten" in low, boss=idx >= 9)

    if low.startswith("progression/boat-"):
        idx = int(Path(p).stem[-1])
        return nearest_contain(tint(src("Boat-harbos-water/Boat.png"), (190 + idx * 12, 112 + idx * 18, 84), idx * 0.16), size, 0.08)
    if low.startswith("progression/harbors-"):
        return harbor_scene(size, int(Path(p).stem[-1]))
    if low.startswith("progression/obstacles-"):
        idx = int(Path(p).stem[-1])
        choices = ["Obstacle/Grass1.png", "Obstacle/Icons_13.png", "Obstacle/Stay.png"]
        return nearest_contain(src(choices[idx]), size, 0.1)
    if "progression/hook-rarity-options/" in low:
        return hook(size, int(Path(p).stem.split("-")[0]) - 1)

    if low.startswith("parallax/"):
        if "motes" in low:
            out = Image.new("RGBA", size)
            d = ImageDraw.Draw(out)
            step = max(8, min(size) // 18)
            for y in range(step, size[1], step * 2):
                for x in range((y // step % 3) * step, size[0], step * 3):
                    d.rectangle((x, y, x + max(1, step // 5), y + max(1, step // 5)), fill=(166, 216, 217, 135))
            return out
        return environment(size, 1 if "seabed" in low else 0, underwater="sky" not in low and "surface" not in low)

    if low.startswith("world/parallax/"):
        variant = ["world-base", "horizon-far", "water-surface", "underwater-mid", "underwater-foreground", "shore-near"].index(Path(p).stem)
        if "shore-near" in low:
            return harbor_scene(size, 1)
        return environment(size, variant % 3, underwater="underwater" in low or "world-base" in low)
    if low.startswith("world/bandoverlays/"):
        return world_overlay(size, 1 if "band-b" in low else 2)
    if low.startswith("world/kraken/"):
        if "attack" in low:
            poses = ["Attack1.png", "Attack2.png", "Attack3.png", "Attack4.png"]
            pose = ["thrust", "swipe", "wrap", "slam"]
            i = next((j for j, n in enumerate(pose) if n in low), 0)
            return nearest_contain(frame(f"Bosses/BandC/{poses[i]}", 96, i), size, 0.02)
        return creature(size, 2 if "far" in low else 1, boss=True)

    if "healthbar/" in low:
        return bar(size, fill="fill" in low, warning="red" in low or "fish-" in low)

    if low.startswith("ui/harbor/"):
        stem = Path(p).stem
        if "button" in stem:
            return button(size, 1 if "primary" in stem else 0)
        if stem in {"market-card", "small-card"}:
            return panel(size, 0 if stem == "market-card" else 1)
        if stem == "harbor-sign":
            return panel(size, 2)
        if stem == "clock-face":
            return draw_pixel_circle(size, PALETTE["cream"], PALETTE["wood"], 4)
        if stem == "coin-icon":
            return nearest_contain(icon(4), size, 0.08)
        if stem == "fish-icon":
            return creature(size, 0)

    if low.startswith("ui/gameplay/"):
        stem = Path(p).stem
        if stem in {"clock-panel", "counter-panel", "safety-plaque", "safety-plaque-warning", "reel-banner"}:
            return panel(size, 1 if "warning" in stem else 0)
        if stem in {"fishing-dial", "speedometer", "timer-ring", "safe-halo", "fishing-joystick-base-option-c", "fishing-joystick-idle-button-option-c", "fishing-joystick-idle-button-option-c-blank"}:
            return draw_pixel_circle(size, (73, 86, 102, 210), PALETTE["cream"], 4)
        if stem in {"left-control", "right-control"}:
            out = button(size, 0)
            d = ImageDraw.Draw(out)
            w, h = size
            direction = -1 if stem.startswith("left") else 1
            points = [(w // 2 + direction * w // 5, h // 4), (w // 2 - direction * w // 5, h // 2), (w // 2 + direction * w // 5, h * 3 // 4)]
            d.polygon(points, fill=PALETTE["cream"])
            return out
        if stem == "hook-icon":
            return hook(size, 1)
        if stem in {"rope-rivet", "depth-ruler"}:
            return nearest_cover(src("Boat-harbos-water/Pier_Tiles.png"), size)
        if "needle" in stem:
            out = Image.new("RGBA", size)
            d = ImageDraw.Draw(out)
            d.rectangle((size[0] * 2 // 5, 0, size[0] * 3 // 5, size[1]), fill=PALETTE["red"])
            return out
        if "clock-face" in stem:
            return draw_pixel_circle(size, PALETTE["cream"], PALETTE["wood"], 4)
        if stem == "bubble":
            return draw_pixel_circle(size, (111, 197, 219, 90), (181, 232, 230, 220), 2)

    if low.startswith("ui/missions/"):
        return panel(size, 0) if "tracker" in low else nearest_contain(icon(3), size, 0.08)

    if low.startswith("ui/shipupgrade/optiona/"):
        stem = Path(p).stem
        if stem == "shipyard-background":
            return harbor_scene(size, 1, True)
        if stem == "shipyard-ground-foreground":
            return nearest_cover(src("Boat-harbos-water/Pier_Tiles.png"), size)
        if stem == "ship-on-stands":
            return nearest_contain(src("Boat-harbos-water/Boat.png"), size, 0.12)
        if "panel" in stem or "sign" in stem:
            return panel(size, 0)
        if "button" in stem:
            return button(size, 1)
        if "hotspot" in stem or "pip" in stem:
            return draw_pixel_circle(size, PALETTE["gold"] if "selected" in stem or "filled" in stem else (91, 77, 80, 255), PALETTE["cream"], 3)
        icon_map = {"coin": 4, "engine": 5, "hold": 6, "hook": 7, "hull": 8}
        for key, idx in icon_map.items():
            if key in stem:
                return nearest_contain(hook(size, idx) if key == "hook" else icon(idx), size, 0.08)
        if "illustration" in stem:
            return harbor_scene(size, 0, True)

    if low.startswith("ui/mockupelements/"):
        stem = Path(p).stem
        if "/panels/" in low:
            return button(size, 1) if "button" in stem else panel(size, 0)
        if "/controls/" in low and "button" in stem:
            return button(size, 1 if "red" in stem else 0)
        if "/controls/" in low:
            return nearest_contain(icon(3 if "filled" in stem or "upgrade" in stem else 2), size, 0.08)
        icon_names = ["anchor", "boat", "coin", "fish-item", "fish-post", "set-sail"]
        idx = next((i for i, n in enumerate(icon_names) if n in stem), 0)
        if "fish-item" in stem:
            return creature(size, 0)
        if "boat" in stem or "set-sail" in stem:
            return nearest_contain(src("Boat-harbos-water/Boat.png"), size, 0.08)
        return nearest_contain(icon(idx), size, 0.08)

    # No active art is allowed to retain the old skin. Unknown additions get a pack-derived panel.
    return panel(size, 0)


def save_all() -> list[tuple[str, Image.Image]]:
    previews: list[tuple[str, Image.Image]] = []
    files = sorted(ART.rglob("*.png"))
    for path in files:
        with Image.open(path) as old:
            size = old.size
        relative = path.relative_to(ART).as_posix()
        image = render(relative, size).convert("RGBA")
        if image.size != size:
            raise RuntimeError(f"wrong size for {relative}: {image.size} != {size}")
        image.save(path, optimize=True)
        meta = path.with_suffix(path.suffix + ".meta")
        if meta.exists():
            metadata = meta.read_text(encoding="utf-8")
            metadata = metadata.replace("    filterMode: 1\n", "    filterMode: 0\n", 1)
            meta.write_text(metadata, encoding="utf-8", newline="\n")
        previews.append((relative, image.copy()))
    return previews


def contact_sheet(items: Iterable[tuple[str, Image.Image]]) -> None:
    items = list(items)
    cell_w, cell_h = 220, 180
    cols = 5
    rows = (len(items) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * cell_w, rows * cell_h), (30, 27, 37))
    d = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    for i, (name, image) in enumerate(items):
        x, y = (i % cols) * cell_w, (i // cols) * cell_h
        thumb = ImageOps.contain(image, (cell_w - 16, cell_h - 42), Image.Resampling.NEAREST)
        checker = Image.new("RGB", thumb.size, (67, 64, 75))
        checker.paste(thumb, mask=thumb.getchannel("A"))
        sheet.paste(checker, (x + (cell_w - thumb.width) // 2, y + 22))
        d.text((x + 5, y + 5), name[-34:], font=font, fill=(235, 226, 211))
    QA.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(QA, optimize=True)


def main() -> None:
    if not SOURCE.is_dir():
        raise SystemExit(f"Missing source art directory: {SOURCE}")
    previews = save_all()
    contact_sheet(previews)
    print(f"Reskinned {len(previews)} PNG assets; QA sheet: {QA}")


if __name__ == "__main__":
    main()
