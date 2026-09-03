using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    // A small edge chevron that points the way to go when there is nothing to do where you are: toward the
    // nearest catchable fish (or, once the hold is full, back to the nearest port). The sea got long when the
    // dock spacing doubled, and new players kept getting lost in empty water — this keeps a direction on screen
    // without a full map. It hides itself the moment something catchable is on screen, so it never nags during
    // actual fishing. Reuses the left/right steering-button art so "go this way" reads as "press this arrow".
    public sealed partial class FishingGameController
    {
        Image navArrow;
        const float NavArrowY = 150f, NavArrowEdgeX = 440f;

        void SetupNavArrow()
        {
            var parent = sea != null ? sea : (canvas != null ? canvas.transform : transform);
            navArrow = RuntimeUI.Image(parent, "NavArrow", "UI/Gameplay/right-control",
                                       new Vector2(NavArrowEdgeX, NavArrowY), new Vector2(110f, 110f));
            if (navArrow == null) return;
            navArrow.raycastTarget = false;
            navArrow.transform.SetAsLastSibling();
            navArrow.gameObject.SetActive(false);
        }

        void TickNavArrow()
        {
            if (navArrow == null) return;
            bool atSea = mode == Mode.Sailing || mode == Mode.Night;
            // Not while docked/harbour-bound, not while fishing, and not inside harbour water (the port is
            // right there — no arrow needed).
            if (!atSea || GameCatalog.InSafeZone(boatX)) { HideNavArrow(); return; }

            float? targetX = NavTargetX();
            if (!targetX.HasValue) { HideNavArrow(); return; }

            float dx = targetX.Value - boatX;
            // On-screen already? Then they can see it; say nothing.
            if (Mathf.Abs(dx) * GameCatalog.WorldScrollPpu < GameCatalog.FishCullPx * 0.9f) { HideNavArrow(); return; }

            bool right = dx > 0f;
            if (!navArrow.gameObject.activeSelf) navArrow.gameObject.SetActive(true);
            var s = RuntimeUI.Sprite(right ? "UI/Gameplay/right-control" : "UI/Gameplay/left-control");
            if (s != null) navArrow.sprite = s;
            navArrow.rectTransform.anchoredPosition = new Vector2(right ? NavArrowEdgeX : -NavArrowEdgeX, NavArrowY);
            var c = navArrow.color;
            c.a = 0.5f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 4f));   // gentle pulse
            navArrow.color = c;
        }

        void HideNavArrow()
        {
            if (navArrow != null && navArrow.gameObject.activeSelf) navArrow.gameObject.SetActive(false);
        }

        // Where the arrow points. Hold full -> nearest port to sell. Otherwise -> nearest catchable fish;
        // and if the whole visible sea is barren, fall back to the nearest port so it still says something.
        float? NavTargetX()
        {
            if (save.Data.cargo.Count >= save.Capacity)
                return NearestPortX();

            float bestD = float.MaxValue; float bestX = 0f; bool found = false;
            for (int i = 0; i < fish.Count; i++)
            {
                var f = fish[i];
                if (f == null || f.Leaving || f.Locked) continue;   // gone, or too deep for this hull
                float d = Mathf.Abs(f.HomeX - boatX);
                if (d < bestD) { bestD = d; bestX = f.HomeX; found = true; }
            }
            if (found) return bestX;
            return NearestPortX();
        }

        float? NearestPortX()
        {
            float bestD = float.MaxValue; float bestX = 0f; bool found = false;
            var ports = GameCatalog.Ports;
            for (int i = 0; i < ports.Count; i++)
            {
                float d = Mathf.Abs(ports[i].x - boatX);
                if (d < bestD) { bestD = d; bestX = ports[i].x; found = true; }
            }
            return found ? bestX : (float?)null;
        }
    }
}
