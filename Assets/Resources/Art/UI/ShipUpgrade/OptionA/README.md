# Ship Upgrade UI — Option A

Runtime-ready UI art exported from the approved Interactive Hotspots mockup.

## Assembly order

1. Use `shipyard-ground-foreground.png` as the foreground ground layer. `shipyard-background.png` remains the preserved full-background source.
2. `sign-shipyard-blank.png`, `button-back.png`, `panel-coin-counter.png`
3. `ship-on-stands.png`
4. `hotspot-neutral.png` or `hotspot-selected.png` with a separate component icon
5. `plaque-component-label.png` and TMP labels
6. `panel-upgrade-detail.png`, `engine-upgrade-illustration.png`, tier pips and TMP stats
7. `button-upgrade-red.png` and `button-back-to-harbor.png`

## Upgrade detail illustrations

- `engine-upgrade-illustration.png`
- `hull-upgrade-illustration.png`
- `hold-upgrade-illustration.png`
- `hook-upgrade-illustration.png`

All four use the same `1254 x 1254` transparent canvas, so the detail-card image can swap sprites without changing its RectTransform.

## Unity import

- Texture Type: `Sprite (2D and UI)`
- Sprite Mode: `Single`
- Mesh Type: `Full Rect` for panels/buttons; `Tight` for icons and illustrations
- Alpha Is Transparency: enabled
- Compression: `None` while assembling; use `High Quality` for the final mobile build
- Use TMP for all text. The exported panels and buttons are intentionally blank.
- Use 9-slicing for the wide panels and buttons after setting their borders in Sprite Editor.

## Hotspot composition

Place one of the hotspot frames below one icon:

- `hotspot-neutral.png`
- `hotspot-selected.png`
- `icon-engine.png`, `icon-hull.png`, `icon-hold.png`, or `icon-hook.png`

This keeps selected/normal states reusable without duplicating every component icon.
