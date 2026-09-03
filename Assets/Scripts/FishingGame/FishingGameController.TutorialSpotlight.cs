using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    // Coach-mark overlay for the opening tutorial: a dark mask around a HOLE cut over the control the player
    // must use, a pulsing highlight frame on that hole, and a pointer that shows the gesture (a pulsing tap,
    // or a side-to-side drag for the FISH dial). It reads the current sequence hint step and lights up its
    // target. Purely visual — nothing blocks input (raycastTarget off everywhere), so the player can always
    // press. Built from solid quads with a runtime 1x1 white sprite, so it has no art dependency.
    public sealed partial class FishingGameController
    {
        Sprite tutWhite;
        RectTransform spotRoot;
        Image spotTop, spotBot, spotLeft, spotRight;   // dark mask, four quads around the hole
        Image spotMid;                                 // extra mask quad BETWEEN two holes (two-target steps)
        Image ringT, ringB, ringL, ringR;              // bright frame on hole A
        Image ring2T, ring2B, ring2L, ring2R;          // bright frame on hole B (two-target steps)
        Image spotPointer;                             // gesture pointer (tap pulse / drag slide)
        Image[] spotTrail;                             // comet tail along the drag gesture path
        bool spotBuilt;

        [System.Serializable] public sealed class TutorialTargetOverride { public string hintId; public RectTransform target; }
        [Header("Tutorial — hand-placed highlight targets (optional)")]
        [Tooltip("Đặt object highlight tay trong scene rồi map theo hintId ở đây; có thì code NHƯỜNG, dùng object của bạn " +
                 "thay cho target tự resolve. hintId: steer_out / cast / steer_hook / dock_back / sell / set_sail.")]
        [SerializeField] TutorialTargetOverride[] tutorialTargets;

        void SetupTutorialSpotlight()
        {
            if (canvas == null) return;
            tutWhite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
            tutWhite.name = "TutWhite";

            var go = new GameObject("TutorialSpotlight", typeof(RectTransform));
            spotRoot = (RectTransform)go.transform;
            spotRoot.SetParent(canvas.transform, false);
            spotRoot.anchorMin = Vector2.zero; spotRoot.anchorMax = Vector2.one;
            spotRoot.offsetMin = spotRoot.offsetMax = Vector2.zero;
            // Its own sub-canvas so the mask layers by sortingOrder, not sibling index — the hint text sits on
            // a higher-order canvas below and is never buried by the dark quads.
            var spotCanvas = spotRoot.gameObject.AddComponent<Canvas>();
            spotCanvas.overrideSorting = true; spotCanvas.sortingOrder = 3000;

            spotTop = SpotImg("MaskTop"); spotBot = SpotImg("MaskBot");
            spotLeft = SpotImg("MaskLeft"); spotRight = SpotImg("MaskRight");
            spotMid = SpotImg("MaskMid");
            ringT = SpotImg("RingTop"); ringB = SpotImg("RingBot");
            ringL = SpotImg("RingLeft"); ringR = SpotImg("RingRight");
            ring2T = SpotImg("Ring2Top"); ring2B = SpotImg("Ring2Bot");
            ring2L = SpotImg("Ring2Left"); ring2R = SpotImg("Ring2Right");
            // Trail dots go UNDER the pointer (created first), then the pointer on top.
            var trailSprite = MakeTouchSprite(48);
            spotTrail = new Image[14];
            for (int i = 0; i < spotTrail.Length; i++)
            {
                spotTrail[i] = SpotImg("Trail" + i);
                spotTrail[i].sprite = trailSprite;
                spotTrail[i].rectTransform.sizeDelta = new Vector2(46, 46);
                spotTrail[i].enabled = false;
            }
            spotPointer = SpotImg("Pointer");
            spotPointer.sprite = MakeTouchSprite(96);   // soft ring + core, drawn in code (no PNG)
            spotPointer.rectTransform.sizeDelta = new Vector2(96, 96);

            // Put the hint text on its OWN higher-order canvas so the mask can never cover it.
            if (hintView != null)
            {
                var hc = hintView.GetComponent<Canvas>();
                if (hc == null) hc = hintView.gameObject.AddComponent<Canvas>();
                hc.overrideSorting = true; hc.sortingOrder = 3100;
            }

            spotRoot.gameObject.SetActive(false);
            spotBuilt = true;
        }

        // A round touch cursor drawn straight into a texture: an anti-aliased ring with a faint core, so the
        // gesture pointer reads as a finger tap rather than a hard square. No art dependency.
        static Sprite MakeTouchSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f, rOuter = c, rRing = c * 0.80f, rCore = c * 0.52f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float core = d <= rCore ? Mathf.Lerp(0.45f, 0.12f, d / rCore) : 0f;
                    float ring = 1f - Mathf.Clamp01(Mathf.Abs(d - rRing) / 3f);   // ~3px AA ring
                    float a = Mathf.Max(core, ring);
                    if (d > rOuter) a = 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            sp.name = "TouchCursor";
            return sp;
        }

        static readonly Color FrameColor = new Color(1f, 0.914f, 0.627f);   // #FFE9A0, matching the other project

        Image SpotImg(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(spotRoot, false);
            var im = go.AddComponent<Image>();
            im.sprite = tutWhite; im.type = Image.Type.Simple; im.raycastTarget = false;
            return im;
        }

        // First unfinished SEQUENCE step whose situation is active right now — that is the step to spotlight.
        // Mirrors the sequence right-of-way in TickHints: only the first pending step may be active.
        string ActiveHintId()
        {
            if (hints == null) return null;
            for (int i = 0; i < hints.Count; i++)
            {
                var h = hints[i];
                if (!h.sequence || Learned(h.id)) continue;
                return h.Show() ? h.id : null;   // the first pending step gates everything after it
            }
            return null;
        }

        // Target control(s) + gesture + mask darkness for a step. gesture: 0 = tap-pulse, 2 = horizontal
        // figure-8. `second` lets a step highlight TWO controls (the hole spans both). Returns null primary
        // when the step has no on-screen target (informational lines get no spotlight).
        RectTransform TutTarget(string id, out RectTransform second, out RectTransform pointerRect, out int gesture, out float maskA)
        {
            second = null; pointerRect = null; gesture = 0; maskA = 0.9f;
            RectTransform primary;
            switch (id)
            {
                case "steer_out":                       // highlight BOTH arrows, point at RIGHT (press it to head out)
                    second = AsRect(right); pointerRect = AsRect(right); primary = AsRect(left); break;
                case "cast":                            // the dial is a joystick — figure-8 drag gesture
                    gesture = 2; primary = AsRect(joystick); break;
                case "steer_hook":                      // highlight the FISH, no dark mask, NO cursor (gesture 1
                    maskA = 0f; gesture = 1; primary = NearestFishRect(); break;   // = none, so it can't cover the fish)
                case "dock_back":                       // sail home: arrow while at sea, then the DOCK button
                    maskA = 0.5f;
                    primary = GameCatalog.AtPort(boatX) != null
                        ? AsRect(utilityButton != null && utilityButton.gameObject.activeInHierarchy
                                 ? (Component)utilityButton : dockButton)
                        : AsRect(left);                 // home is toward the left (x decreasing)
                    break;
                case "sell":                            // point the cursor at the first SELL button, not the whole list
                    primary = FirstSellButtonRect() ?? marketList;
                    pointerRect = primary;
                    break;
                case "accept_mission":                  // 3-stage: MISSIONS → note's see-more → ACCEPT
                    if (ledgerOpen && missionView != null && missionView.TrackButtonRect != null)
                        primary = missionView.TrackButtonRect;
                    else if (trackerShown && missionView != null && missionView.TrackerTapButtonRect != null)
                        primary = missionView.TrackerTapButtonRect;
                    else primary = AsRect(missionButton);
                    pointerRect = primary;
                    break;
                case "close_ledger":                    // point at the Ledger's Back-to-Harbour (close) button
                    primary = missionView != null ? missionView.CloseButtonRect : null;
                    pointerRect = primary;
                    break;
                case "sea_open_note":                   // at sea: highlight MISSIONS, lighter mask so the sea shows
                    maskA = 0.55f; primary = AsRect(missionButton); pointerRect = primary; break;
                case "sea_see_more":                    // the note's "see more" (Tracker Tap) button
                    maskA = 0.55f; primary = missionView != null ? missionView.TrackerTapButtonRect : null; pointerRect = primary; break;
                case "sea_track":                       // the TRACK/UNTRACK button inside the open ledger
                    primary = missionView != null ? missionView.TrackButtonRect : null; pointerRect = primary; break;
                case "sea_close":                       // the Back-to-Harbour (close) button
                    primary = missionView != null ? missionView.CloseButtonRect : null; pointerRect = primary; break;
                case "set_sail": primary = AsRect(sailButton); break;
                default: return null;                   // dock_intro / freshness are info-only
            }
            // A hand-placed highlight target in the scene wins — the code yields to the designer's object.
            var ov = TutOverride(id);
            if (ov != null) { second = null; return ov; }
            return primary;
        }

        RectTransform TutOverride(string id)
        {
            if (tutorialTargets == null) return null;
            for (int i = 0; i < tutorialTargets.Length; i++)
            {
                var t = tutorialTargets[i];
                if (t != null && t.target != null && t.hintId == id && t.target.gameObject.activeInHierarchy)
                    return t.target;
            }
            return null;
        }

        static RectTransform AsRect(Component c) => c != null ? (RectTransform)c.transform : null;

        // The SELL button on the first market row (rows are built per-fish at runtime), for the sell step.
        RectTransform FirstSellButtonRect()
        {
            for (int i = 0; i < marketRows.Count; i++)
            {
                if (marketRows[i] == null) continue;
                var row = marketRows[i].GetComponent<MarketRow>();
                if (row != null && row.SellButtonRect != null && row.SellButtonRect.gameObject.activeInHierarchy)
                    return row.SellButtonRect;
            }
            return null;
        }

        // The catchable fish nearest the boat, for the "drain its health" step's highlight.
        RectTransform NearestFishRect()
        {
            FishActor best = null; float bestD = float.MaxValue;
            for (int i = 0; i < fish.Count; i++)
            {
                var f = fish[i];
                if (f == null || f.Leaving || f.Locked || !f.Visible || f.Rect == null) continue;
                float d = Mathf.Abs(f.HomeX - boatX);
                if (d < bestD) { bestD = d; best = f; }
            }
            return best != null ? best.Rect : null;
        }

        void TickTutorialSpotlight()
        {
            if (!spotBuilt || spotRoot == null) return;

            string activeId = ActiveHintId();
            var primary = TutTarget(activeId, out var second, out var pointerRect, out int gesture, out float maskA);
            float minH = activeId == "steer_hook" ? 0.17f : 0.06f;   // taller frame around a fish
            if (primary == null || !primary.gameObject.activeInHierarchy)
            {
                if (spotRoot.gameObject.activeSelf) spotRoot.gameObject.SetActive(false);
                return;
            }
            if (!spotRoot.gameObject.activeSelf) spotRoot.gameObject.SetActive(true);
            // Layering is by sub-canvas sortingOrder (mask 3000 / hint 3100), not sibling order.

            // Each target's hole in spotRoot-normalised [0,1] space (render-mode agnostic). A two-target step
            // (steer_out) keeps them as TWO separate holes with the DOCK dial between them staying dark.
            Rect rr = spotRoot.rect;
            bool two = second != null && second.gameObject.activeInHierarchy;
            NormRect(primary, minH, out float ax0, out float ay0, out float ax1, out float ay1);
            float bx0 = 0, by0 = 0, bx1 = 0, by1 = 0;
            if (two)
            {
                NormRect(second, minH, out bx0, out by0, out bx1, out by1);
                if (ax0 > bx0)   // keep hole A on the left, B on the right
                {
                    float t;
                    t = ax0; ax0 = bx0; bx0 = t; t = ax1; ax1 = bx1; bx1 = t;
                    t = ay0; ay0 = by0; by0 = t; t = ay1; ay1 = by1; by1 = t;
                }
            }
            // Union rect, used only for the pointer fallback + gesture amplitude below.
            float nx0 = two ? Mathf.Min(ax0, bx0) : ax0;
            float nx1 = two ? Mathf.Max(ax1, bx1) : ax1;
            float ny0 = two ? Mathf.Min(ay0, by0) : ay0;
            float ny1 = two ? Mathf.Max(ay1, by1) : ay1;

            var mc = new Color(0f, 0f, 0f, maskA);
            float ringA = 0.5f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
            var rc = new Color(FrameColor.r, FrameColor.g, FrameColor.b, ringA);

            if (two)
            {
                Frame(spotTop, 0, ny1, 1, 1, mc);
                Frame(spotBot, 0, 0, 1, ny0, mc);
                Frame(spotLeft, 0, ny0, ax0, ny1, mc);
                Frame(spotMid, ax1, ny0, bx0, ny1, mc); spotMid.enabled = true;   // keep the DOCK dial dark
                Frame(spotRight, bx1, ny0, 1, ny1, mc);
                DrawFrame(ringT, ringB, ringL, ringR, ax0, ay0, ax1, ay1, rc);
                DrawFrame(ring2T, ring2B, ring2L, ring2R, bx0, by0, bx1, by1, rc);
                SetRing2(true);
            }
            else
            {
                Frame(spotTop, 0, ny1, 1, 1, mc);
                Frame(spotBot, 0, 0, 1, ny0, mc);
                Frame(spotLeft, 0, ny0, nx0, ny1, mc);
                Frame(spotRight, nx1, ny0, 1, ny1, mc);
                spotMid.enabled = false;
                DrawFrame(ringT, ringB, ringL, ringR, nx0, ny0, nx1, ny1, rc);
                SetRing2(false);
            }

            // Pointer position: a step can point at a SPECIFIC control (e.g. steer_out points at the RIGHT
            // arrow) rather than the centre of the whole highlighted area; otherwise it sits at the hole centre.
            float cx = (nx0 + nx1) * .5f, cy = (ny0 + ny1) * .5f;
            if (pointerRect != null && pointerRect.gameObject.activeInHierarchy)
            {
                RectBoundsLocal(pointerRect, out Vector2 pmn, out Vector2 pmx);
                cx = Mathf.Clamp01(((pmn.x + pmx.x) * .5f - rr.xMin) / rr.width);
                cy = Mathf.Clamp01(((pmn.y + pmx.y) * .5f - rr.yMin) / rr.height);
            }
            // gesture 2 = horizontal figure-8 (∞) drag; else = tap pulse.
            var prt = spotPointer.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(cx, cy);
            prt.pivot = new Vector2(.5f, .5f);
            if (gesture == 2)
            {
                float t = Time.unscaledTime * 2.4f;
                float ax = (nx1 - nx0) * rr.width * 0.34f;
                float ay = (ny1 - ny0) * rr.height * 0.26f;
                prt.anchoredPosition = new Vector2(Mathf.Sin(t) * ax, Mathf.Sin(t * 2f) * ay);  // ∞ path
                prt.localScale = Vector3.one;
                spotPointer.enabled = true;
                // Comet tail: earlier phases of the same ∞ path, fading out behind the pointer.
                for (int i = 0; i < spotTrail.Length; i++)
                {
                    float ph = t - (i + 1) * 0.16f;
                    var trt = spotTrail[i].rectTransform;
                    trt.anchorMin = trt.anchorMax = new Vector2(cx, cy); trt.pivot = new Vector2(.5f, .5f);
                    trt.anchoredPosition = new Vector2(Mathf.Sin(ph) * ax, Mathf.Sin(ph * 2f) * ay);
                    float a = 0.5f * (1f - (i + 1f) / (spotTrail.Length + 1f));
                    spotTrail[i].color = new Color(FrameColor.r, FrameColor.g, FrameColor.b, a);
                    spotTrail[i].enabled = true;
                }
            }
            else if (gesture == 0)
            {
                prt.anchoredPosition = Vector2.zero;
                float s = 0.85f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.5f));
                prt.localScale = new Vector3(s, s, 1f);
                spotPointer.enabled = true;
                HideTrail();
            }
            else { spotPointer.enabled = false; HideTrail(); }   // steer_hook: highlight the fish, no gesture dot
            if (spotPointer.enabled) spotPointer.color = new Color(FrameColor.r, FrameColor.g, FrameColor.b, 0.95f);
        }

        void HideTrail()
        {
            if (spotTrail == null) return;
            for (int i = 0; i < spotTrail.Length; i++) if (spotTrail[i] != null) spotTrail[i].enabled = false;
        }

        // Min/max corners of a UI rect, expressed in spotRoot-local space.
        void RectBoundsLocal(RectTransform target, out Vector2 mn, out Vector2 mx)
        {
            var c = new Vector3[4];
            target.GetWorldCorners(c);
            Vector3 a = spotRoot.InverseTransformPoint(c[0]);
            Vector3 b = spotRoot.InverseTransformPoint(c[2]);
            mn = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
            mx = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        // Set an image to fill a normalised sub-rect of spotRoot.
        void Frame(Image im, float ax, float ay, float bx, float by, Color col)
        {
            var rt = im.rectTransform;
            rt.anchorMin = new Vector2(ax, ay);
            rt.anchorMax = new Vector2(bx, by);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            im.color = col;
        }

        // A target's padded hole in normalised [0,1] space, with a minimum size so a small target (a fish)
        // still gets a clearly visible frame.
        void NormRect(RectTransform t, float minH, out float x0, out float y0, out float x1, out float y1)
        {
            RectBoundsLocal(t, out Vector2 mn, out Vector2 mx);
            Rect rr = spotRoot.rect; float padX = 26f, padY = 26f;
            x0 = Mathf.Clamp01((mn.x - padX - rr.xMin) / rr.width);
            x1 = Mathf.Clamp01((mx.x + padX - rr.xMin) / rr.width);
            y0 = Mathf.Clamp01((mn.y - padY - rr.yMin) / rr.height);
            y1 = Mathf.Clamp01((mx.y + padY - rr.yMin) / rr.height);
            const float minW = 0.12f;
            if (x1 - x0 < minW) { float m = (x0 + x1) * .5f; x0 = Mathf.Clamp01(m - minW * .5f); x1 = Mathf.Clamp01(m + minW * .5f); }
            if (y1 - y0 < minH) { float m = (y0 + y1) * .5f; y0 = Mathf.Clamp01(m - minH * .5f); y1 = Mathf.Clamp01(m + minH * .5f); }
        }

        // Four bright bars around a normalised rect (the pulsing highlight frame).
        void DrawFrame(Image t, Image b, Image l, Image r, float x0, float y0, float x1, float y1, Color col)
        {
            const float tx = 0.004f, ty = 0.004f;
            Frame(t, x0, y1 - ty, x1, y1, col);
            Frame(b, x0, y0, x1, y0 + ty, col);
            Frame(l, x0, y0, x0 + tx, y1, col);
            Frame(r, x1 - tx, y0, x1, y1, col);
        }

        void SetRing2(bool on)
        {
            ring2T.enabled = on; ring2B.enabled = on; ring2L.enabled = on; ring2R.enabled = on;
        }
    }
}
