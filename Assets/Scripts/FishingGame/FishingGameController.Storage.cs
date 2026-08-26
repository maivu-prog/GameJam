using System.Collections.Generic;
using UnityEngine;

namespace RustyFishing
{
    // The hold. Two ways in: the player opens it to look, or it forces itself open because the last catch
    // went over capacity. The second case is the one with teeth -- it cannot be dismissed until the hold
    // is back under the limit -- so both paths share one repaint and differ only in whether Close works.
    //
    // Layout lives in the scene (StoragePanelView). If that view is not wired, everything here falls
    // through to the old runtime-built modal in .Harbor.cs, so a half-built panel never blocks play.
    public sealed partial class FishingGameController
    {
        void SetupStorageRefs()
        {
            BindClick(storageButton, ToggleStorage);
            if (storagePanel != null)
            {
                storagePanel.Bind(CloseStorage, TossRotten);
                storagePanel.SetVisible(false);
            }
            RefreshStorageButton();
            if(GameCatalog.debugStorage)
            {
                // What the SAVE already holds before a single cast. A non-zero count here means the hold
                // carried over from an earlier session, not that catching added too much.
                var ids = new List<string>();
                foreach (var c in save.Data.cargo) ids.Add(c.id);
                Debug.Log($"[KHOANG] luc khoi dong: {save.Data.cargo.Count} con trong save "
                        + $"[{string.Join(", ", ids)}]  | panel wired={UseStoragePanel}  "
                        + $"nut wired={storageButton != null}");
            }
        }

        bool UseStoragePanel => storagePanel != null && storagePanel.Usable;

        /// <summary>The count on the button itself, so the hold can be read without opening it.</summary>
        void RefreshStorageButton()
        {
            if (storageButtonCount != null)
                storageButtonCount.text = $"{save.Data.cargo.Count}/{save.Capacity}";
        }

        void ToggleStorage()
        {
            if (inventoryOpen) { CloseStorage(); return; }
            OpenStorage();
        }

        void OpenStorage()
        {
            if (!UseStoragePanel) { OpenInventoryToss(); return; }   // fall back to the generated modal
            inventoryOpen = true;
            storagePanel.SetVisible(true);
            RefreshStorage();
        }

        void CloseStorage()
        {
            // Refuse while overflowing: the fish are already aboard and the only way out is to put some
            // back. The view hides its own close button in that state, but the call can still arrive from
            // a key or a stray click, so the rule lives here too.
            if (save.OverCapacity) { RefreshStorage(); return; }
            inventoryOpen = false;
            if (UseStoragePanel) storagePanel.SetVisible(false);
            else CloseInventory();
            RefreshStorageButton();
        }

        /// <summary>Repaint from the save. Called on open, and after every toss.</summary>
        void RefreshStorage()
        {
            if (!UseStoragePanel) return;
            var rows = new List<StorageEntry>(save.Data.cargo.Count);
            for (int i = 0; i < save.Data.cargo.Count; i++)
            {
                var c = save.Data.cargo[i];
                var def = GameCatalog.GetFish(c.id);
                if (def == null) continue;
                string fresh = save.Freshness(c, AbsHour);
                int freshIndex = fresh == "Fresh" ? 0 : fresh == "Stale" ? 1 : 2;
                float wmul = PlayerSave.W(c);
                rows.Add(new StorageEntry
                {
                    index = i,
                    // Rotten fish have their own art, same as the market list.
                    sprite = RuntimeUI.Sprite("fish/species/" + def.art + (freshIndex == 2 ? "-rotten" : "")),
                    species = def.name.ToUpperInvariant(),
                    weightKg = def.size * GameCatalog.WeightSizeToKg * wmul,
                    freshIndex = freshIndex,
                    // What it is worth HERE if docked, otherwise the plain value -- at sea there is no
                    // port price to quote, and showing a home-port price would be a lie.
                    price = currentPort != null && mode == Mode.Harbor
                            ? save.PriceOf(c, currentPort, AbsHour)
                            : Mathf.RoundToInt(def.value * wmul),
                });
            }
            storagePanel.Show(rows, save.Capacity, save.OverCapacity, TossFromStorage);
            RefreshStorageButton();
        }

        void TossFromStorage(int index)
        {
            if (!save.TossAt(index)) return;
            if (GameCatalog.debugStorage)
                Debug.Log($"[KHOANG] vut con thu {index}, con lai {save.Data.cargo.Count}");
            RefreshStorage();
            // Dropping back under the limit ends the forced-open state, but only that state -- a player
            // who opened the hold themselves stays in it, because they were not finished looking.
            if (!save.OverCapacity && forcedStorage) { forcedStorage = false; CloseStorage(); }
        }

        void TossRotten()
        {
            if (save.Toss(AbsHour) > 0) RefreshStorage();
        }

        /// <summary>The hold just went over. Force the panel open and keep it there.</summary>
        void ForceStorageOpen()
        {
            forcedStorage = true;
            if (!UseStoragePanel) { OpenInventoryToss(); return; }
            inventoryOpen = true;
            storagePanel.SetVisible(true);
            RefreshStorage();
        }
    }
}
