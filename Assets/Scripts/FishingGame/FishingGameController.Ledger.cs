using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    /// <summary>
    /// Drives the mission UI. Owns no layout and builds nothing — the panel is authored by hand in the
    /// scene and dragged into <see cref="MissionLedgerView"/>; this file only decides WHAT should be on it.
    ///
    /// Design rules from the brief, and why each is here:
    ///   · ONE tracked mission. More than one and the sea note stops being glanceable.
    ///   · The note shrinks itself after a few seconds and expands on touch, so it never competes with
    ///     steering or the depth gauge.
    ///   · Progress never pauses the game and never opens a modal — a stamp presses in, the count changes,
    ///     and the player keeps sailing.
    ///   · Completion is a red ink stamp, not sparkles, to match the parchment the harbour is drawn on.
    ///
    /// With no view assigned the missions still run and still pay out; there is simply no window into them.
    /// </summary>
    public sealed partial class FishingGameController
    {
        bool ledgerOpen;

        [Tooltip("Indicator cạnh/ trên nút MISSIONS — hiện khi có nhiệm vụ CHƯA nhận (mới hoặc đã xem). Tuỳ chọn.")]
        [SerializeField] Image missionWarnIcon;
        float missionBtnPopT;   // pop animation on the MISSIONS button when it opens the Ledger

        /// <summary>
        /// The sea note is OFF by default — the player brings it out with the toggle. When shown it is
        /// COMPACT (one line, current objective only — see TrackerFocusLine), so a multi-task mission still
        /// fits a phone. Tapping it opens the full Ledger.
        ///     [toggle] hide ⇄ note ──[tap the note]──▶ full Ledger
        /// </summary>
        bool trackerShown = false;
        float stampTimer;
        const float StampSeconds = .8f;

        // =============================================================================================
        //  Setup
        // =============================================================================================
        void SetupMissions()
        {
            EnsureMissionAssigned();
            if (missionButton != null) BindClick(missionButton, ShowTrackerFromButton);   // opens the NOTE, not the full log
            if (missionView != null)
            {
                // Tapping the note is the way UP to the full panel — not a way to resize the note.
                missionView.BindButtons(ClaimFromLedger, TrackOrAccept, CloseLedger, OpenLedger, ToggleTracker);
                missionView.SetOpen(false);
                missionView.SetProgressStamp(false);
            }
            UpdateMissionUI();
        }

        // =============================================================================================
        //  Open / close
        // =============================================================================================
        public void OpenLedger()
        {
            missionBtnPopT = 0.22f;   // pop the button on press, even if the view is not wired
            if (missionView == null) return;
            EnsureMissionAssigned();
            ledgerOpen = true;
            trackerShown = false;      // "see more" transitions the note into the full log — hide the note
            if (AtSea) everCheckedMissionSea = true;   // retires the "check missions at sea" tutorial step
            if (save != null && !save.Data.missionSeen) { save.Data.missionSeen = true; save.Store(); }   // opened = seen
            missionView.SetOpen(true);
            UpdateMissionUI();
        }

        public void CloseLedger()
        {
            ledgerOpen = false;
            if (missionView != null) missionView.SetOpen(false);
        }

        void ClaimFromLedger()
        {
            ClaimMission();
            // Stay open: the next mission's briefing is the pay-off for reading, and closing the panel
            // would hide the hand-off the whole flow is built around.
            UpdateMissionUI();
        }

        /// <summary>Show or hide the sea note. Wired to the SAFE/DANGER board (and, once accepted, the TRACK button).</summary>
        void ToggleTracker() { trackerShown = !trackerShown; everToggledTrack = true; UpdateMissionUI(); }

        /// <summary>The one TRACK button doubles as ACCEPT: it accepts an offered mission, else toggles the note.</summary>
        void TrackOrAccept() { if (MissionOffered) AcceptMission(); else ToggleTracker(); }

        /// <summary>MISSIONS button: bring out the sea note (a mini-briefing). Tapping its "see more" opens the
        /// full Ledger; tapping anywhere outside the note closes it.</summary>
        void ShowTrackerFromButton()
        {
            EnsureMissionAssigned();
            trackerShown = true;
            trackerGrace = 0.2f;                 // the tap that opened it must not immediately close it
            missionBtnPopT = 0.22f;
            if (AtSea) everShowedNoteSea = true;
            UpdateMissionUI();
        }
        void CloseTracker() { trackerShown = false; UpdateMissionUI(); }

        // Tap anywhere outside the sea note (and not on the full Ledger) closes the note.
        float trackerGrace;
        void TickTrackerAutoClose(float dt)
        {
            if (trackerGrace > 0f) trackerGrace -= dt;
            if (!trackerShown || ledgerOpen || missionView == null || trackerGrace > 0f) return;
            var rt = missionView.TrackerRootRect;
            if (rt == null) return;
            var p = UnityEngine.InputSystem.Pointer.current;
            if (p == null || !p.press.wasPressedThisFrame) return;
            Vector2 pos = p.position.ReadValue();
            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, pos, null)) CloseTracker();
        }

        /// <summary>MISSIONS button feedback: no breathing — an offered (not-yet-accepted) mission simply
        /// shows the indicator icon.</summary>
        void TickMissionButton()
        {
            if (missionButton != null)
            {
                if (missionBtnPopT > 0f)
                {
                    missionBtnPopT -= Time.unscaledDeltaTime;
                    float k = Mathf.Clamp01(1f - missionBtnPopT / 0.22f);
                    missionButton.transform.localScale = Vector3.one * MissionLedgerView.PopScale(k);
                    if (missionBtnPopT <= 0f) missionButton.transform.localScale = Vector3.one;
                }
                else if (missionButton.transform.localScale != Vector3.one)
                    missionButton.transform.localScale = Vector3.one;   // no breathing anymore
            }
            if (missionWarnIcon != null)
            {
                bool offered = MissionOffered;
                if (missionWarnIcon.gameObject.activeSelf != offered) missionWarnIcon.gameObject.SetActive(offered);
            }
        }

        // =============================================================================================
        //  Paint
        // =============================================================================================
        void UpdateMissionUI()
        {
            if (missionView == null) return;
            var m = CurrentMission;

            // The sea note (a mini-briefing with a "see more" to the full log) shows for ANY mission — offered
            // or accepted — but only when the player brings it out with the MISSIONS button; it never auto-pops.
            missionView.PaintTracker(m != null && trackerShown,
                                     m != null ? m.title.ToUpperInvariant() : "",
                                     TrackerFocusLine(m));

            if (!ledgerOpen) return;

            // Who is standing there: the giver briefs a live mission, but the harbour's own keeper stamps
            // one that is ready to hand in — that is who the player is actually facing.
            var speaker = MissionReady && currentPort != null
                        ? MissionBook.NpcAtHarbor(currentPort.id) ?? MissionBook.Npc(m?.giver)
                        : MissionBook.Npc(m?.giver);

            var model = new LedgerModel {
                portrait = speaker != null ? RuntimeUI.Sprite("Characters/Narrative/" + speaker.portrait) : null,
                npcName = speaker != null ? speaker.name : "",
                npcRole = speaker != null ? speaker.role : "",
                tracking = trackerShown,
            };

            if (m == null)
            {
                model.dialogue = Speech(new[] { "The bell has stopped.",
                                                "Something below knows your name." });
                model.title = "NO ACTIVE MISSION";
                model.description = "Every page has been found.";
                missionView.Paint(model);
                return;
            }

            // Ready-to-hand-in shows the pay-off lines; otherwise the briefing.
            model.dialogue = Speech(MissionClaimableHere ? m.completeDialogue : m.startDialogue);
            model.title = m.title.ToUpperInvariant();
            model.description = m.description;
            model.objectives = ObjectiveBlock(m, false);
            model.reward = m.rewardCoins.ToString();
            // Just the number — MissionLedgerView draws the coin beside it, and falls back to
            // "60c" when no icon is wired up so the reward never reads as a unitless figure.

            var port = GameCatalog.Ports.Find(p => p.id == m.completionHarbor);
            string where = port != null ? port.name : m.completionHarbor;
            model.whereLine = MissionReady ? $"Report to {where}" : $"Hand in at {where}";

            model.showStamp = MissionReady;
            model.showClaim = MissionClaimableHere;
            model.showAccept = MissionOffered;   // ACCEPT button visible only while offered-not-accepted
            missionView.Paint(model);
        }

        /// <summary>
        /// One speaker's lines as a single block: no quote marks, no blank line between them, and never
        /// more than three lines however many the mission author wrote. The cap is the important part —
        /// the dialogue box is a fixed height in the scene, so a fourth line does not shrink the text, it
        /// pushes straight through whatever sits underneath.
        /// </summary>
        const int MaxSpeechLines = 3;
        static string Speech(string[] lines)
        {
            if (lines == null || lines.Length == 0) return "";
            int n = Mathf.Min(lines.Length, MaxSpeechLines);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(lines[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The objective list, one line each with its tally. A finished line is dimmed, not ticked.
        /// </summary>
        const string DoneAlpha = "<alpha=#66>";   // TMP rich text, ~40% opacity
        const string LiveAlpha = "<alpha=#ff>";

        /// <summary>
        /// The compact sea-note line: the FIRST unfinished objective only, plus its tally and — when the
        /// mission has more than one task — a dim "(2/3)" so the player knows there is more without it eating
        /// a second line. When everything is met it becomes a short "report in" nudge. One line, always.
        /// </summary>
        string TrackerFocusLine(MissionDef m)
        {
            if (m == null) return "";
            for (int i = 0; i < m.objectives.Count; i++)
            {
                var o = m.objectives[i];
                int done = Mathf.Min(ProgressAt(i), o.count);
                if (done >= o.count) continue;   // this one is finished — move focus to the next
                var sb = new System.Text.StringBuilder();
                sb.Append(o.label);
                if (o.count > 1) sb.Append("   ").Append(done).Append('/').Append(o.count);
                if (m.objectives.Count > 1)
                    sb.Append("   ").Append(DoneAlpha).Append('(').Append(i + 1).Append('/')
                      .Append(m.objectives.Count).Append(')').Append(LiveAlpha);
                return sb.ToString();
            }
            return DoneAlpha + "All done — report in" + LiveAlpha;
        }

        string ObjectiveBlock(MissionDef m, bool terse)
        {
            if (m == null) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < m.objectives.Count; i++)
            {
                var o = m.objectives[i];
                int done = Mathf.Min(ProgressAt(i), o.count);
                bool full = done >= o.count;
                if (sb.Length > 0) sb.Append('\n');
                // Dim a finished line instead of marking it. A checkbox costs a fixed indent on
                // EVERY row, including the unfinished ones that actually need the reading room,
                // and it is a second thing to parse for what contrast already tells the eye.
                sb.Append(full ? DoneAlpha : LiveAlpha);
                sb.Append(o.label);
                // A one-of objective reads better as done/not-done than as "1/1".
                if (o.count > 1) sb.Append("   ").Append(done).Append('/').Append(o.count);
                else if (full && terse) sb.Append("   done");
            }
            return sb.ToString();
        }

        // =============================================================================================
        //  Stamp feedback
        // =============================================================================================
        /// <summary>
        /// A short ink press on the sea note. Never pauses, never blocks input — and never drags the note
        /// out on its own: with the note hidden there is nothing for the stamp to land on, so it is skipped.
        /// </summary>
        void StampProgress()
        {
            if (missionView == null || !trackerShown) return;
            stampTimer = StampSeconds;
            missionView.SetProgressStamp(true);
        }

        /// <summary>The claim press, in the harbour — the READY stamp is already visible, so re-press it.</summary>
        void PlayClaimStamp()
        {
            if (missionView == null) return;
            stampTimer = StampSeconds;
        }

        /// <summary>
        /// Driven from Update BEFORE its harbour early-out, so the stamp still animates and the note still
        /// collapses while the player is standing in port.
        /// </summary>
        void TickMissionUI(float dt)
        {
            TickMissionButton();
            TickTrackerAutoClose(dt);
            if (missionView == null) return;

            if (stampTimer > 0f)
            {
                stampTimer -= dt;
                float k = Mathf.Clamp01(stampTimer / StampSeconds);
                // Presses down fast, holds, then lifts off — the shape of a stamp hitting paper.
                float scale = Mathf.Lerp(1f, 1.6f, k * k);
                var press = missionView.ProgressStampTransform;
                if (press != null) press.localScale = Vector3.one * scale;
                var ready = missionView.ReadyStampTransform;
                if (ready != null && ready.gameObject.activeSelf) ready.localScale = Vector3.one * scale;

                if (stampTimer <= 0f)
                {
                    missionView.SetProgressStamp(false);
                    if (press != null) press.localScale = Vector3.one;
                    if (ready != null) ready.localScale = Vector3.one;
                }
            }
        }
    }
}
