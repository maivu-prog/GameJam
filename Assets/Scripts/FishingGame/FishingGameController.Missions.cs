using System.Collections.Generic;
using UnityEngine;

namespace RustyFishing
{
    /// <summary>
    /// Story mission tracking for "The Bell Below".
    ///
    /// Same discipline as the old questline: every hook is ONE call from the place the event already
    /// happens, so nothing polls and nothing guesses. What changed is where a mission ENDS — objectives can
    /// be met anywhere, but the reward only lands when the player docks at the named port and the giver
    /// stamps it. That is what turns the map into somewhere you travel to on purpose.
    ///
    /// A mission carries one tally per objective, so a two-line mission ("catch three, then come home")
    /// tracks both independently and the second line does not eat the first line's progress.
    /// </summary>
    public sealed partial class FishingGameController
    {
        MissionDef CurrentMission => MissionBook.Get(save.Data.missionId);

        /// <summary>Objectives all met — waiting on the right harbour to hand it in.</summary>
        public bool MissionReady => save != null && save.Data.missionReady;

        /// <summary>Standing in the port that can stamp the live mission.</summary>
        public bool MissionClaimableHere =>
            MissionReady && currentPort != null && CurrentMission != null
            && currentPort.id == CurrentMission.completionHarbor;

        /// <summary>Hand out the opening mission the first time the player is ever in a harbour.</summary>
        void EnsureMissionAssigned()
        {
            if (save == null) return;
            if (!string.IsNullOrEmpty(save.Data.missionId)) return;
            if (save.Data.missionsDone.Count > 0) return;   // chain finished, not un-started
            StartMission(MissionBook.FirstId);
        }

        void StartMission(string id)
        {
            var m = MissionBook.Get(id);
            save.Data.missionId = m != null ? id : "";
            save.Data.missionReady = false;
            save.Data.missionProgress.Clear();
            if (m != null) for (int i = 0; i < m.objectives.Count; i++) save.Data.missionProgress.Add(0);
            save.Store();
        }

        int ProgressAt(int i) =>
            i >= 0 && i < save.Data.missionProgress.Count ? save.Data.missionProgress[i] : 0;

        // ── event hooks ──────────────────────────────────────────────────────────────────────────────
        // Each of these is called from the one place that already knows the event happened. They all funnel
        // into Advance(), which is the only thing that writes progress.

        public void MissionOnCatch(FishDef def, float depthU, int zoneIndex, float weightKg)
        {
            if (def == null) return;
            Advance(ObjectiveKind.CatchSpecies, 1, target: def.id);
            Advance(ObjectiveKind.CatchWeight, 1, target: def.id, value: weightKg);
        }

        /// <summary>
        /// False until Awake has finished. Awake calls OpenHarbor to BUILD the world, which fires the same
        /// docking hook a real arrival does — so without this a save left mid-"return to Home Harbor" would
        /// tick that objective off the instant the game was reopened, and the player would never sail.
        /// </summary>
        bool worldReady;

        /// <summary>Called at the end of Awake, once the bootstrap docking is safely behind us.</summary>
        void MissionsWorldReady() => worldReady = true;

        public void MissionOnDock(PortDef port)
        {
            if (port == null || !worldReady) return;
            Advance(ObjectiveKind.DockAtHarbor, 1, target: port.id);
            float hullPct = save.MaxHp > 0 ? 100f * save.Data.hullHp / save.MaxHp : 0f;
            Advance(ObjectiveKind.ReturnWithHullAbove, 1, value: hullPct);
            if (wasOffshoreAtNight) { Advance(ObjectiveKind.StayOffshoreAtNight, 1); wasOffshoreAtNight = false; }
        }

        /// <summary>
        /// One completed sale. <paramref name="sold"/> is what actually left the hold, snapshotted by the
        /// caller BEFORE the cargo list was emptied — PlayerSave.Sell removes the rows as it goes, so by
        /// the time it returns there is nothing left to inspect.
        /// </summary>
        public void MissionOnSale(PortDef port, int earned, List<CaughtFish> sold, List<string> freshness)
        {
            if (port == null) return;
            if (earned > 0) Advance(ObjectiveKind.EarnCoinsFromSale, earned, harbor: port.id);
            if (sold == null) return;
            for (int i = 0; i < sold.Count; i++)
            {
                Advance(ObjectiveKind.SellSpeciesAtHarbor, 1, target: sold[i].id, harbor: port.id);
                if (freshness != null && i < freshness.Count && freshness[i] == "Fresh")
                    Advance(ObjectiveKind.SellFreshFish, 1, target: sold[i].id);
            }
        }

        public void MissionOnSafeCrossing(string obstacleId) =>
            Advance(ObjectiveKind.CrossObstacleSafely, 1, target: obstacleId);

        public void MissionOnUpgrade(string branch, int level) =>
            Advance(ObjectiveKind.UpgradePart, level, target: branch, snapTo: true);

        /// <summary>
        /// Set while the boat is outside every port radius after dark, and consumed by the next docking.
        /// Kept as a flag rather than an objective tally because the objective is "was out there AND came
        /// back" — the second half only resolves on arrival.
        /// </summary>
        bool wasOffshoreAtNight;

        /// <summary>Called from the sailing tick — cheap enough to run unconditionally.</summary>
        public void MissionNoteOffshore(bool offshore, bool night)
        {
            if (offshore && night) wasOffshoreAtNight = true;
        }

        // ── progression ──────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Credit every objective of the live mission that matches. Filters are all "empty means any", so
        /// an objective with no target counts every event of its kind.
        ///
        /// <paramref name="value"/> is the threshold test (kg, hull percent) — the event only counts when
        /// it clears the objective's bar. <paramref name="snapTo"/> is for level-style objectives where the
        /// amount IS the level reached rather than something to add up.
        /// </summary>
        void Advance(ObjectiveKind kind, int amount, string target = "", string harbor = "",
                     float value = 0f, bool snapTo = false)
        {
            var m = CurrentMission;
            if (m == null || amount <= 0 || save.Data.missionReady) return;

            bool touched = false;
            for (int i = 0; i < m.objectives.Count && i < save.Data.missionProgress.Count; i++)
            {
                var o = m.objectives[i];
                if (o.kind != kind) continue;
                if (!string.IsNullOrEmpty(o.target) && o.target != target) continue;
                if (!string.IsNullOrEmpty(o.harbor) && o.harbor != harbor) continue;
                if (o.threshold > 0f && value < o.threshold) continue;
                if (save.Data.missionProgress[i] >= o.count) continue;   // already satisfied

                save.Data.missionProgress[i] = snapTo
                    ? Mathf.Min(amount, o.count)
                    : Mathf.Min(save.Data.missionProgress[i] + amount, o.count);
                touched = true;
            }
            if (!touched) return;

            // Objectives are ordered but NOT gated: catching the three bream on the way home still counts
            // even if the player docks first. Only the finished/unfinished total matters.
            bool all = true;
            for (int i = 0; i < m.objectives.Count; i++)
                if (ProgressAt(i) < m.objectives[i].count) { all = false; break; }

            if (all)
            {
                save.Data.missionReady = true;
                var at = GameCatalog.Ports.Find(p => p.id == m.completionHarbor);
                Set(message, $"{m.title.ToUpperInvariant()} — ready. Report to {(at != null ? at.name : m.completionHarbor)}.");
            }

            save.Store();
            StampProgress();
            UpdateMissionUI();
        }

        /// <summary>
        /// Hand the mission in. Only legal standing in the completion port — the button is hidden
        /// otherwise, and this re-checks because Claim is public and the harbour can change underneath it.
        /// </summary>
        public void ClaimMission()
        {
            if (!MissionClaimableHere) return;
            var m = CurrentMission;

            save.Data.coins += m.rewardCoins;
            save.Data.missionsDone.Add(m.id);
            StartMission(m.nextId);            // clears progress/ready and writes the save

            var next = CurrentMission;
            Set(message, next != null
                ? $"{m.title} complete!  +{m.rewardCoins}c   Next: {next.title}"
                : $"{m.title} complete!  +{m.rewardCoins}c   The bell has stopped.");

            PlayClaimStamp();
            SetCoins();
            RefreshHarbor();
            UpdateMissionUI();
        }
    }
}
