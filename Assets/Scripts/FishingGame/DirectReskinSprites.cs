using System;
using System.Collections.Generic;
using UnityEngine;

namespace RustyFishing
{
    /// <summary>
    /// Resolves legacy art keys to Sprite sub-assets imported directly from ReskinArt.
    /// No texture is cropped, tinted, composited, or regenerated at runtime.
    /// </summary>
    public static class DirectReskinSprites
    {
        readonly struct Source
        {
            public readonly string ResourcePath;
            public readonly string SpriteName;

            public Source(string resourcePath, string spriteName)
            {
                ResourcePath = "Art/ReskinArt/" + resourcePath;
                SpriteName = spriteName;
            }
        }

        static readonly Dictionary<string, Source> Exact = new(StringComparer.OrdinalIgnoreCase)
        {
            // Harbor UI: blank source sprites keep runtime text separate from the bitmap.
            ["UI/Harbor/action-button"] = S("Freebuttons/LongButtons2", "LongButtons2_2"),
            ["UI/Harbor/primary-button"] = S("Freebuttons/LongButtons2", "LongButtons2_3"),
            ["UI/Harbor/market-card"] = S("Freebuttons/UiCozyFree", "UiCozyFree_100"),
            ["UI/Harbor/small-card"] = S("Freebuttons/UiCozyFree", "UiCozyFree_101"),
            ["UI/Harbor/harbor-sign"] = S("Freebuttons/UiCozyFree", "UiCozyFree_100"),
            ["UI/Harbor/clock-face"] = S("Freebuttons/LittleButtons", "LittleButtons_2"),
            ["UI/Harbor/coin-icon"] = S("Freebuttons/UiCozyFree", "UiCozyFree_27"),
            ["UI/Harbor/fish-icon"] = S("4 Icons/Icons_06", "Icons_06_0"),

            // Fishing HUD and controls.
            ["UI/Gameplay/clock-panel"] = S("Freebuttons/UiCozyFree", "UiCozyFree_102"),
            ["UI/Gameplay/counter-panel"] = S("Freebuttons/UiCozyFree", "UiCozyFree_101"),
            ["UI/Gameplay/reel-banner"] = S("Freebuttons/UiCozyFree", "UiCozyFree_100"),
            ["UI/Gameplay/safety-plaque"] = S("Freebuttons/UiCozyFree", "UiCozyFree_101"),
            ["UI/Gameplay/safety-plaque-warning"] = S("Freebuttons/UiCozyFree", "UiCozyFree_100"),
            ["UI/Gameplay/fishing-dial"] = S("Freebuttons/LittleButtons", "LittleButtons_2"),
            ["UI/Gameplay/speedometer"] = S("Freebuttons/LittleButtons", "LittleButtons_3"),
            ["UI/Gameplay/timer-ring"] = S("Freebuttons/LittleButtons", "LittleButtons_4"),
            ["UI/Gameplay/safe-halo"] = S("Freebuttons/LittleButtons", "LittleButtons_5"),
            ["UI/Gameplay/fishing-joystick-base-option-c"] = S("Freebuttons/LittleButtons", "LittleButtons_2"),
            ["UI/Gameplay/fishing-joystick-idle-button-option-c"] = S("Freebuttons/UiCozyFree", "UiCozyFree_0"),
            ["UI/Gameplay/fishing-joystick-idle-button-option-c-blank"] = S("Freebuttons/LittleButtons", "LittleButtons_2"),
            ["UI/Gameplay/left-control"] = S("Freebuttons/UiCozyFree", "UiCozyFree_44"),
            ["UI/Gameplay/right-control"] = S("Freebuttons/UiCozyFree", "UiCozyFree_45"),
            ["UI/Gameplay/hook-icon"] = S("4 Icons/Icons_01", "Icons_01_0"),
            ["UI/Gameplay/rope-rivet"] = S("Freebuttons/LittleButtons", "LittleButtons_0"),
            ["UI/Gameplay/depth-ruler"] = S("Boat-harbos-water/Pier_Tiles", "Pier_Tiles_0"),
            ["UI/Gameplay/bubble"] = S("Freebuttons/LittleButtons", "LittleButtons_10"),
            ["UI/Gameplay/day-night-clock-face-option-d"] = S("Freebuttons/LittleButtons", "LittleButtons_2"),
            ["UI/Gameplay/day-night-clock-needle-option-d-needle"] = S("Freebuttons/UiCozyFree", "UiCozyFree_44"),
            ["UI/Gameplay/day-night-clock-needle-option-d-pivot"] = S("Freebuttons/LittleButtons", "LittleButtons_0"),
            ["UI/Gameplay/day-night-clock-safe-needle-option-d"] = S("Freebuttons/UiCozyFree", "UiCozyFree_45"),
            ["UI/Gameplay/HealthBar/fish-health-bar-frame"] = S("Freebuttons/LongButtons2", "LongButtons2_0"),
            ["UI/Gameplay/HealthBar/fish-health-bar-fill"] = S("Freebuttons/LongButtons2", "LongButtons2_2"),
            ["UI/Gameplay/HealthBar/health-bar-frame"] = S("Freebuttons/LongButtons2", "LongButtons2_1"),
            ["UI/Gameplay/HealthBar/health-bar-fill-red"] = S("Freebuttons/LongButtons2", "LongButtons2_3"),

            // Mission and mock-up UI.
            ["UI/Missions/mission-tracker-note"] = S("Freebuttons/UiCozyFree", "UiCozyFree_100"),
            ["UI/Missions/mission-complete-stamp"] = S("Freebuttons/UiCozyFree", "UiCozyFree_14"),
            ["UI/MockupElements/Controls/button-red-wide"] = S("Freebuttons/LongButtons2", "LongButtons2_0"),
            ["UI/MockupElements/Controls/button-teal-small"] = S("Freebuttons/LongButtons", "LongButtons_2"),
            ["UI/MockupElements/Controls/icon-gear-round"] = S("Freebuttons/UiCozyFree", "UiCozyFree_11"),
            ["UI/MockupElements/Controls/icon-upgrade-round"] = S("Freebuttons/UiCozyFree", "UiCozyFree_17"),
            ["UI/MockupElements/Controls/status-pip-empty"] = S("Freebuttons/LittleButtons", "LittleButtons_0"),
            ["UI/MockupElements/Controls/status-pip-filled"] = S("Freebuttons/LittleButtons", "LittleButtons_2"),
            ["UI/MockupElements/Icons/anchor-upgrade-icon"] = S("4 Icons/Icons_01", "Icons_01_0"),
            ["UI/MockupElements/Icons/boat-repair-icon"] = S("Boat-harbos-water/Boat", "Boat_0"),
            ["UI/MockupElements/Icons/coin-icon"] = S("Freebuttons/UiCozyFree", "UiCozyFree_27"),
            ["UI/MockupElements/Icons/fish-item-blue"] = S("4 Icons/Icons_06", "Icons_06_0"),
            ["UI/MockupElements/Icons/fish-post-stamp-icon"] = S("Freebuttons/UiCozyFree", "UiCozyFree_14"),
            ["UI/MockupElements/Icons/set-sail-boat-icon"] = S("Boat-harbos-water/Boat", "Boat_0"),
            ["UI/MockupElements/Panels/fish-market-row-small-1x3"] = S("Freebuttons/UiCozyFree", "UiCozyFree_101"),
            ["UI/MockupElements/Panels/fish-market-table-large-3x3"] = S("Freebuttons/UiCozyFree", "UiCozyFree_100"),
            ["UI/MockupElements/Panels/market-parchment-panel-tall"] = S("Freebuttons/UiCozyFree", "UiCozyFree_102"),
            ["UI/MockupElements/Panels/primary-button-red-wide"] = S("Freebuttons/LongButtons2", "LongButtons2_0"),
            ["UI/MockupElements/Panels/upgrade-card-panel"] = S("Freebuttons/UiCozyFree", "UiCozyFree_101"),

            // Ship upgrade screen.
            ["UI/ShipUpgrade/OptionA/button-upgrade-red"] = S("Freebuttons/LongButtons2", "LongButtons2_0"),
            ["UI/ShipUpgrade/OptionA/panel-stat-comparison"] = S("Freebuttons/UiCozyFree", "UiCozyFree_101"),
            ["UI/ShipUpgrade/OptionA/panel-upgrade-detail"] = S("Freebuttons/UiCozyFree", "UiCozyFree_100"),
            ["UI/ShipUpgrade/OptionA/sign-shipyard-blank"] = S("Freebuttons/UiCozyFree", "UiCozyFree_100"),
            ["UI/ShipUpgrade/OptionA/hotspot-neutral"] = S("Freebuttons/LittleButtons", "LittleButtons_0"),
            ["UI/ShipUpgrade/OptionA/hotspot-selected"] = S("Freebuttons/LittleButtons", "LittleButtons_2"),
            ["UI/ShipUpgrade/OptionA/tier-pip-empty"] = S("Freebuttons/LittleButtons", "LittleButtons_0"),
            ["UI/ShipUpgrade/OptionA/tier-pip-filled"] = S("Freebuttons/LittleButtons", "LittleButtons_2"),
            ["UI/ShipUpgrade/OptionA/icon-coin"] = S("Freebuttons/UiCozyFree", "UiCozyFree_27"),
            ["UI/ShipUpgrade/OptionA/icon-engine"] = S("4 Icons/Icons_18", "Icons_18_0"),
            ["UI/ShipUpgrade/OptionA/icon-hold"] = S("Storage/Icons_16", "Icons_16_0"),
            ["UI/ShipUpgrade/OptionA/icon-hook"] = S("4 Icons/Icons_01", "Icons_01_0"),
            ["UI/ShipUpgrade/OptionA/icon-hull"] = S("Boat-harbos-water/Boat", "Boat_0"),
            ["UI/ShipUpgrade/OptionA/engine-upgrade-illustration"] = S("4 Icons/Icons_18", "Icons_18_0"),
            ["UI/ShipUpgrade/OptionA/hold-upgrade-illustration"] = S("Storage/Icons_16", "Icons_16_0"),
            ["UI/ShipUpgrade/OptionA/hook-upgrade-illustration"] = S("4 Icons/Icons_01", "Icons_01_0"),
            ["UI/ShipUpgrade/OptionA/hull-upgrade-illustration"] = S("Boat-harbos-water/Boat", "Boat_0"),
            ["UI/ShipUpgrade/OptionA/ship-on-stands"] = S("Boat-harbos-water/Boat", "Boat_0"),
            ["UI/ShipUpgrade/OptionA/shipyard-background"] = S("Boat-harbos-water/Fishing_hut", "Fishing_hut_0"),
            ["UI/ShipUpgrade/OptionA/shipyard-ground-foreground"] = S("Boat-harbos-water/Pier_Tiles", "Pier_Tiles_0"),
        };

        static readonly Dictionary<string, Sprite> Cache = new(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> Missing = new(StringComparer.OrdinalIgnoreCase);

        static Source S(string path, string name) => new(path, name);

        public static Sprite Load(string legacyPath)
        {
            if (string.IsNullOrWhiteSpace(legacyPath)) return null;
            legacyPath = legacyPath.Replace('\\', '/').TrimStart('/');
            if (Cache.TryGetValue(legacyPath, out var cached)) return cached;
            if (!TryResolve(legacyPath, out var source)) return null;

            var sprites = Resources.LoadAll<Sprite>(source.ResourcePath);
            Sprite result = null;
            foreach (var sprite in sprites)
            {
                if (string.Equals(sprite.name, source.SpriteName, StringComparison.OrdinalIgnoreCase))
                {
                    result = sprite;
                    break;
                }
            }

            if (result == null && sprites.Length == 1) result = sprites[0];
            if (result == null) result = Resources.Load<Sprite>(source.ResourcePath);
            if (result != null) Cache[legacyPath] = result;
            else if (Missing.Add(legacyPath))
                Debug.LogError($"Direct reskin sprite is missing: {legacyPath} -> {source.ResourcePath} [{source.SpriteName}]");
            return result;
        }

        public static bool HasMapping(string legacyPath) => TryResolve(legacyPath, out _);

        static bool TryResolve(string path, out Source source)
        {
            if (Exact.TryGetValue(path, out source)) return true;

            if (path.StartsWith("fish/species/", StringComparison.OrdinalIgnoreCase))
            {
                var name = path.Substring("fish/species/".Length);
                var rotten = name.EndsWith("-rotten", StringComparison.OrdinalIgnoreCase);
                if (rotten) name = name.Substring(0, name.Length - "-rotten".Length);
                var species = new[] { "sardine", "mackerel", "bream", "red_snapper", "black_grouper", "piranha", "barracuda", "lanternfish", "ghost_tuna" };
                var index = Array.FindIndex(species, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    var group = index % 6 + 1;
                    var state = rotten ? "Death" : "Idle";
                    source = S($"Octopus and Jellyfish/{group}/{state}", $"{state}_0");
                    return true;
                }
                var band = name.Equals("anglerfish", StringComparison.OrdinalIgnoreCase) ? "BandA" :
                    name.Equals("night_shark", StringComparison.OrdinalIgnoreCase) ? "BandB" : "BandC";
                var bossState = rotten ? "Death" : "Idle";
                source = S($"Bosses/{band}/{bossState}", $"{bossState}_0");
                return true;
            }

            if (path.StartsWith("Characters/Narrative/", StringComparison.OrdinalIgnoreCase))
            {
                source = path.Contains("drowned", StringComparison.OrdinalIgnoreCase) || path.Contains("keeper", StringComparison.OrdinalIgnoreCase)
                    ? S("Character/old man idle state no line-Sheet", "old man idle state no line-Sheet_0")
                    : path.Contains("elias", StringComparison.OrdinalIgnoreCase) || path.Contains("silas", StringComparison.OrdinalIgnoreCase)
                        ? S("Bosses/BandA/Idle", "Idle_0")
                        : S("Bosses/BandB/Idle", "Idle_0");
                return true;
            }

            if (path.StartsWith("progression/boat-", StringComparison.OrdinalIgnoreCase))
            { source = S("Boat-harbos-water/Boat", "Boat_0"); return true; }
            if (path.StartsWith("progression/harbors-", StringComparison.OrdinalIgnoreCase))
            { source = S("Boat-harbos-water/Fishing_hut", "Fishing_hut_0"); return true; }
            if (path.StartsWith("progression/hook-rarity-options/", StringComparison.OrdinalIgnoreCase))
            { source = S("Boat-harbos-water/Fish-rod", "Fish-rod_0"); return true; }
            if (path.Equals("progression/obstacles-0", StringComparison.OrdinalIgnoreCase))
            { source = S("Obstacle/Grass1", "Grass1_0"); return true; }
            if (path.Equals("progression/obstacles-1", StringComparison.OrdinalIgnoreCase))
            { source = S("Obstacle/Icons_13", "Icons_13_0"); return true; }
            if (path.Equals("progression/obstacles-2", StringComparison.OrdinalIgnoreCase))
            { source = S("Obstacle/Stay", "Stay_0"); return true; }

            if (path.Equals("fishing-world-backdrop", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("whispering-harbor", StringComparison.OrdinalIgnoreCase))
            { source = S("Boat-harbos-water/Fishing_hut", "Fishing_hut_0"); return true; }
            if (path.Equals("rusty-fishing-title-logo", StringComparison.OrdinalIgnoreCase))
            { source = S("Character/fisherman", "fisherman_0"); return true; }
            if (path.Equals("sea-monster-encounter", StringComparison.OrdinalIgnoreCase))
            { source = S("Bosses/BandC/Idle", "Idle_0"); return true; }

            if (path.StartsWith("parallax/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("World/Parallax/", StringComparison.OrdinalIgnoreCase))
            {
                source = path.Contains("shore", StringComparison.OrdinalIgnoreCase) || path.Contains("seabed", StringComparison.OrdinalIgnoreCase)
                    ? S("Boat-harbos-water/Pier_Tiles", "Pier_Tiles_0")
                    : S("Boat-harbos-water/Water", "Water_0");
                return true;
            }
            if (path.StartsWith("World/BandOverlays/", StringComparison.OrdinalIgnoreCase))
            { source = S("Boat-harbos-water/Water", "Water_0"); return true; }
            if (path.StartsWith("World/Kraken/", StringComparison.OrdinalIgnoreCase))
            {
                var state = path.Contains("slam", StringComparison.OrdinalIgnoreCase) ? "Attack4" :
                    path.Contains("swipe", StringComparison.OrdinalIgnoreCase) ? "Attack2" :
                    path.Contains("wrap", StringComparison.OrdinalIgnoreCase) ? "Attack3" :
                    path.Contains("attack", StringComparison.OrdinalIgnoreCase) ? "Attack1" : "Idle";
                source = S($"Bosses/BandC/{state}", $"{state}_0");
                return true;
            }

            source = default;
            return false;
        }
    }
}
