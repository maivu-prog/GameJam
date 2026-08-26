using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RustyFishing
{
    /// <summary>
    /// Loads Assets/Resources/GameData/game-data.json BEFORE the scene loads and overrides the
    /// hard-coded defaults in GameCatalog, so a Google Sheet (synced via tools/sheet_sync.py) drives
    /// fish / ports / obstacles / zones / tuning / economy. If the file is missing or invalid the
    /// built-in defaults are kept.
    /// </summary>
    public static class GameDataLoader
    {
        public static bool Loaded { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load() => LoadNow();

        /// <summary>
        /// Re-read game-data.json into GameCatalog. Normally fires once before the scene loads; exposed so
        /// the Tuning Inspector can pull authored values back in from the Editor without entering Play.
        /// </summary>
        public static void LoadNow()
        {
            var ta = Resources.Load<TextAsset>("GameData/game-data");
            if (ta == null) { Debug.Log("[GameData] no game-data.json in Resources/GameData — using built-in defaults"); return; }
            Dictionary<string, object> root;
            try { root = (Dictionary<string, object>)MiniJson.Parse(ta.text); }
            catch (Exception e)
            {
                Debug.LogError("[GameData] game-data.json is not valid JSON, keeping built-in defaults: " + e);
                return;
            }

            int failed = Apply(root);
            Loaded = true;
            if (failed > 0)
                Debug.LogError($"[GameData] {failed} section(s) of game-data.json FAILED to load and kept " +
                               "their built-in defaults. The game is not running on the numbers in that file.");
            Debug.Log($"[GameData] loaded game-data.json: {GameCatalog.Fish.Count} fish, {GameCatalog.Ports.Count} ports, {GameCatalog.Obstacles.Count} obstacles, {SeaMap.Zones.Count} zones x {SeaMap.Bands.Count} bands, {MissionBook.Count} missions");
        }

        static float F(object o) => o == null ? 0f : (float)(double)o;
        static string S(object o) => o as string ?? "";
        static bool B(object o) => o is bool b && b;
        static Dictionary<string, object> D(object o) => (Dictionary<string, object>)o;

        /// <summary>
        /// Apply each section independently and return how many threw.
        ///
        /// One try/catch around the whole file used to mean a single mistyped value discarded EVERY
        /// tuned number in it -- costs, fish, zones, the lot -- and the only sign was one warning line
        /// in the editor log while the game quietly ran on the built-in defaults. Section by section, a
        /// bad row costs you that section and nothing else, and it is reported as an error.
        /// </summary>
        static int Apply(Dictionary<string, object> root)
        {
            int failed = 0;
            failed += Section(root, "fish", o => ApplyFish((List<object>)o));
            failed += Section(root, "obstacles", o => ApplyObstacles((List<object>)o));
            failed += Section(root, "ports", o => ApplyPorts((List<object>)o));   // after fish (prices keyed by fish id)
            failed += Section(root, "bands", o => ApplyBands((List<object>)o));   // before zones: shelfDepth defaults to the deepest band
            failed += Section(root, "zones", o => ApplyZones((List<object>)o));
            failed += Section(root, "tuning", o => { ApplyStatics(D(o)); ApplyStockTuning(D(o)); });
            failed += Section(root, "economy", o => ApplyStatics(D(o)));
            failed += Section(root, "upgrades", o => ApplyUpgrades((List<object>)o));
            // No "quests" block: the old questline was replaced by MissionBook, which is authored in code
            // rather than in JSON. Unknown keys are ignored here, so an older game-data.json still loads.
            return failed;
        }

        static int Section(Dictionary<string, object> root, string key, Action<object> apply)
        {
            if (!root.TryGetValue(key, out var value)) return 0;
            try { apply(value); return 0; }
            catch (Exception e)
            {
                Debug.LogError($"[GameData] section \"{key}\" failed to load, keeping its defaults: {e}");
                return 1;
            }
        }

        static void ApplyFish(List<object> rows)
        {
            GameCatalog.Fish.Clear();
            foreach (var row in rows)
            {
                var r = D(row);
                GameCatalog.Fish.Add(new FishDef(S(r["id"]), S(r["name"]), S(r["art"]),
                    F(r["hp"]), F(r["speed"]), F(r["size"]), F(r["aspect"]), F(r["minDepth"]), F(r["maxDepth"]), F(r["value"]), F(r["weight"]),
                    r.TryGetValue("rarity", out var rv) ? F(rv) : 1f,
                    r.TryGetValue("atk", out var av) ? F(av) : 0f,
                    r.TryGetValue("chasePx", out var cv) ? F(cv) : 0f,
                    r.TryGetValue("attackEvery", out var ev) ? F(ev) : 4f,
                    r.TryGetValue("maxAlive", out var mv) ? Mathf.RoundToInt(F(mv)) : 0,
                    // Zone gate. Absent in older data files, so it defaults to the whole map rather than
                    // to nothing -- a missing key must not silently delete a species from the sea.
                    r.TryGetValue("minZone", out var z0) ? Mathf.RoundToInt(F(z0)) : 1,
                    r.TryGetValue("maxZone", out var z1) ? Mathf.RoundToInt(F(z1)) : 9));
            }
        }

        static void ApplyObstacles(List<object> rows)
        {
            GameCatalog.Obstacles.Clear();
            foreach (var row in rows)
            {
                var r = D(row);
                GameCatalog.Obstacles.Add(new ObstacleDef(S(r["id"]), S(r["name"]), F(r["damage"]), F(r["safeSpeedMin"]), F(r["safeSpeedMax"]), S(r["art"])));
            }
        }

        static void ApplyPorts(List<object> rows)
        {
            GameCatalog.Ports.Clear();
            float maxX = 0;
            foreach (var row in rows)
            {
                var r = D(row);
                var p = new PortDef(S(r["id"]), S(r["name"]), F(r["x"]), B(r["upgrades"]),
                                    !r.TryGetValue("repair", out var rp) || B(rp), S(r["art"]));   // default: repairs allowed
                foreach (var f in GameCatalog.Fish)
                    p.prices[f.id] = r.TryGetValue("price_" + f.id, out var pv) ? F(pv) : 1f;
                // obs_<obstacleId> columns = how many of that obstacle spawn in this port's approach.
                foreach (var o in GameCatalog.Obstacles)
                    if (r.TryGetValue("obs_" + o.id, out var ov)) p.obstacleCounts[o.id] = Mathf.RoundToInt(F(ov));
                GameCatalog.Ports.Add(p);
                if (p.x > maxX) maxX = p.x;
            }
            GameCatalog.SeaLength = maxX + 8; // make sure the boat can reach the farthest dock
            GameCatalog.GenerateObstacleField();
        }

        // Zones run left to right (1..N) and are bounded by the ports, so no maxX is stored here — only
        // how each zone behaves. Same for bands, top to bottom.
        static void ApplyZones(List<object> rows)
        {
            SeaMap.Zones.Clear();
            int i = 1;
            foreach (var row in rows)
            {
                var r = D(row);
                SeaMap.Zones.Add(new SeaZoneDef(
                    r.TryGetValue("index", out var iv) ? Mathf.RoundToInt(F(iv)) : i,
                    r.TryGetValue("density", out var dv) ? F(dv) : 1f,
                    r.TryGetValue("rarityBias", out var rb) ? F(rb) : 0f,
                    r.TryGetValue("difficulty", out var df) ? F(df) : 1f,
                    r.TryGetValue("shelfDepth", out var sd) ? F(sd) : SeaMap.DeepestU,
                    r.TryGetValue("flee", out var zf) ? F(zf) : 1f,
                    r.TryGetValue("evilHp", out var eh) ? F(eh) : 1f,
                    r.TryGetValue("gapMul", out var gm) ? F(gm) : 1f));
                i++;
            }
        }

        static void ApplyBands(List<object> rows)
        {
            SeaMap.Bands.Clear();
            foreach (var row in rows)
            {
                var r = D(row);
                SeaMap.Bands.Add(new BandDef(S(r["id"]), F(r["top"]), F(r["bottom"]),
                    r.TryGetValue("density", out var dv) ? F(dv) : 1f,
                    r.TryGetValue("rarityBias", out var rb) ? F(rb) : 0f,
                    r.TryGetValue("difficulty", out var df) ? F(df) : 1f,
                    r.TryGetValue("flee", out var bf) ? F(bf) : 1f));
            }
        }

        // FishStock keeps its own statics rather than living on GameCatalog, so the reflection pass in
        // ApplyStatics cannot see them. Four keys is not worth a second reflection surface.
        static void ApplyStockTuning(Dictionary<string, object> kv)
        {
            if (kv.TryGetValue("StockDepletionPerCatch", out var a)) FishStock.DepletionPerCatch = F(a);
            if (kv.TryGetValue("StockRegenPerSecond", out var b)) FishStock.RegenPerSecond = F(b);
            if (kv.TryGetValue("StockMin", out var c)) FishStock.MinStock = F(c);
            if (kv.TryGetValue("StockSleepRegen", out var e)) FishStock.SleepRegen = F(e);

            // int[] — ApplyStatics only handles single floats and ints.
            if (kv.TryGetValue("NewShipCosts", out var ns) && ns is List<object> list && list.Count > 0)
            {
                var costs = new int[list.Count];
                for (int i = 0; i < list.Count; i++) costs[i] = Mathf.RoundToInt(F(list[i]));
                GameCatalog.NewShipCosts = costs;
            }
        }

        // Set public static float/int fields on GameCatalog by name (skips const/readonly and unknown keys).
        static void ApplyStatics(Dictionary<string, object> kv)
        {
            foreach (var pair in kv)
            {
                var fi = typeof(GameCatalog).GetField(pair.Key, BindingFlags.Public | BindingFlags.Static);
                if (fi == null || fi.IsLiteral || fi.IsInitOnly) continue;
                if (fi.FieldType == typeof(float)) fi.SetValue(null, F(pair.Value));
                else if (fi.FieldType == typeof(int)) fi.SetValue(null, Mathf.RoundToInt(F(pair.Value)));
            }
        }

        // Upgrades: one row per level ({id, branch, level, cost, milestone?}); grouped into per-branch
        // cost arrays. A row flagged "milestone": true also becomes that branch's milestone level, so the
        // levels and the marks on them stay in one list instead of two that can drift apart.
        static void ApplyUpgrades(List<object> rows)
        {
            var byBranch = new Dictionary<string, SortedDictionary<int, int>>();
            var marks = new Dictionary<string, SortedSet<int>>();
            foreach (var row in rows)
            {
                var r = D(row);
                string branch = S(r["branch"]);
                if (branch == "") continue;
                int level = Mathf.RoundToInt(F(r["level"]));
                int cost = Mathf.RoundToInt(F(r["cost"]));
                if (!byBranch.TryGetValue(branch, out var lv)) { lv = new SortedDictionary<int, int>(); byBranch[branch] = lv; }
                lv[level] = cost;
                // B(), not F(): F casts to double, and a JSON bool throws InvalidCastException there.
                // One such value aborts the whole Apply() and the game silently runs on C# defaults.
                if (r.TryGetValue("milestone", out var m) && B(m))
                {
                    if (!marks.TryGetValue(branch, out var set)) { set = new SortedSet<int>(); marks[branch] = set; }
                    set.Add(level);
                }
            }
            if (byBranch.Count == 0) return;
            GameCatalog.UpgradeCosts.Clear();
            foreach (var kv in byBranch)
                GameCatalog.UpgradeCosts[kv.Key] = new List<int>(kv.Value.Values).ToArray();
            if (marks.Count == 0) return;
            GameCatalog.UpgradeMilestones.Clear();
            foreach (var kv in marks)
            {
                var arr = new int[kv.Value.Count];
                kv.Value.CopyTo(arr);
                GameCatalog.UpgradeMilestones[kv.Key] = arr;
            }
        }
    }
}
