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
        static void Load()
        {
            var ta = Resources.Load<TextAsset>("GameData/game-data");
            if (ta == null) { Debug.Log("[GameData] no game-data.json in Resources/GameData — using built-in defaults"); return; }
            try
            {
                Apply((Dictionary<string, object>)MiniJson.Parse(ta.text));
                Loaded = true;
                Debug.Log($"[GameData] loaded game-data.json: {GameCatalog.Fish.Count} fish, {GameCatalog.Ports.Count} ports, {GameCatalog.Obstacles.Count} obstacles, {GameCatalog.Zones.Count} zones");
            }
            catch (Exception e) { Debug.LogWarning("[GameData] parse failed, keeping defaults: " + e); }
        }

        static float F(object o) => o == null ? 0f : (float)(double)o;
        static string S(object o) => o as string ?? "";
        static bool B(object o) => o is bool b && b;
        static Dictionary<string, object> D(object o) => (Dictionary<string, object>)o;

        static void Apply(Dictionary<string, object> root)
        {
            if (root.TryGetValue("fish", out var fo)) ApplyFish((List<object>)fo);
            if (root.TryGetValue("obstacles", out var oo)) ApplyObstacles((List<object>)oo);
            if (root.TryGetValue("ports", out var po)) ApplyPorts((List<object>)po);   // after fish (prices keyed by fish id)
            if (root.TryGetValue("zones", out var zo)) ApplyZones((List<object>)zo);
            if (root.TryGetValue("tuning", out var to)) ApplyStatics(D(to));
            if (root.TryGetValue("economy", out var eo)) ApplyStatics(D(eo));
            if (root.TryGetValue("upgrades", out var uo)) ApplyUpgrades((List<object>)uo);
        }

        static void ApplyFish(List<object> rows)
        {
            GameCatalog.Fish.Clear();
            foreach (var row in rows)
            {
                var r = D(row);
                GameCatalog.Fish.Add(new FishDef(S(r["id"]), S(r["name"]), S(r["art"]),
                    F(r["hp"]), F(r["speed"]), F(r["size"]), F(r["aspect"]), F(r["minDepth"]), F(r["maxDepth"]), F(r["value"]), F(r["weight"])));
            }
        }

        static void ApplyObstacles(List<object> rows)
        {
            GameCatalog.Obstacles.Clear();
            foreach (var row in rows)
            {
                var r = D(row);
                GameCatalog.Obstacles.Add(new ObstacleDef(S(r["id"]), S(r["name"]), F(r["x"]), F(r["safeSpeed"]), F(r["damage"]), S(r["art"])));
            }
        }

        static void ApplyPorts(List<object> rows)
        {
            GameCatalog.Ports.Clear();
            float maxX = 0;
            foreach (var row in rows)
            {
                var r = D(row);
                var p = new PortDef(S(r["id"]), S(r["name"]), F(r["x"]), B(r["upgrades"]), S(r["art"]));
                foreach (var f in GameCatalog.Fish)
                    p.prices[f.id] = r.TryGetValue("price_" + f.id, out var pv) ? F(pv) : 1f;
                GameCatalog.Ports.Add(p);
                if (p.x > maxX) maxX = p.x;
            }
            GameCatalog.SeaLength = maxX + 8; // make sure the boat can reach the farthest dock
        }

        static void ApplyZones(List<object> rows)
        {
            GameCatalog.Zones.Clear();
            foreach (var row in rows)
            {
                var r = D(row);
                GameCatalog.Zones.Add(new ZoneDef(S(r["name"]), F(r["maxX"]), F(r["spawnRateMul"]), ParseAllows(S(r["allows"]))));
            }
        }

        // "sardine,bream" = whitelist; "!ghost_tuna,!bream" = blacklist (allow everything except those).
        static Func<string, bool> ParseAllows(string spec)
        {
            bool blacklist = false;
            var set = new HashSet<string>();
            foreach (var raw in spec.Split(','))
            {
                var t = raw.Trim();
                if (t.Length == 0) continue;
                if (t[0] == '!') { blacklist = true; set.Add(t.Substring(1)); }
                else set.Add(t);
            }
            if (blacklist) return id => !set.Contains(id);
            return id => set.Contains(id);
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

        static void ApplyUpgrades(List<object> rows)
        {
            if (rows.Count == 0) return;
            var r = D(rows[0]); // costs are uniform across branches
            if (r.TryGetValue("cost1", out var c1) && r.TryGetValue("cost2", out var c2) && r.TryGetValue("cost3", out var c3))
                GameCatalog.UpgradeCosts = new[] { Mathf.RoundToInt(F(c1)), Mathf.RoundToInt(F(c2)), Mathf.RoundToInt(F(c3)) };
        }
    }
}
