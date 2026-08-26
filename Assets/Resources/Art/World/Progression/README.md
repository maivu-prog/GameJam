# Sea progression backgrounds

All production images are `1080 x 1920` and share the same waterline at source row `640`.

| Asset | Runtime zones | Visual progression |
|---|---|---|
| `sea-zones-01-03.png` | Zones 1-3 | Sparse shallow water. The continental shelf fills depth band C, so C1-C3 do not exist. |
| `sea-zones-04-06.png` | Zones 4-6 | Moderate environmental density. The shelf drops and opens C4-C6 as a trench. |
| `sea-zones-07-09.png` | Zones 7-9 | Dense outer-sea environment. Band C is fully open and reads as a deep abyss. |

Fish are intentionally absent from these paintings. Fish density, rarity and difficulty remain runtime data in `SeaMap`.

The three images use the same above-water section. Runtime integration should crossfade only the underwater portion when moving between zone groups. Hull ascension should change the vertical crop/zoom rather than swap to unrelated art:

- Hull tier 1: show A and roughly half of B.
- Hull tier 2: show A and B, teasing the top of C.
- Hull tier 3: show the full A/B/C water column.

Raw ImageGen sources are archived in `ArtSource/SeaProgression`.
