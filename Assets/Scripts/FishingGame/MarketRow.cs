using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    /// <summary>
    /// One market row = ONE caught fish (not grouped). Put this on your row prefab and wire the
    /// pieces it has. Unassigned fields are skipped, so the prefab layout is up to you.
    ///   freshDots: 3 dots, [0]=Fresh [1]=Stale [2]=Rotten — the fish's level lights up, others dim.
    ///   nameLabel: species name, already upper-cased by the caller.
    /// </summary>
    public sealed class MarketRow : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text nameLabel;     // species, e.g. "SILVER SARDINE" — optional like the rest
        [SerializeField] TMP_Text weightLabel;   // e.g. "2.34 Kg"
        [SerializeField] TMP_Text priceLabel;    // sell value of this single fish
        [SerializeField] Image[] freshDots;      // exactly 3: Fresh / Stale / Rotten
        [SerializeField] Button sellButton;

        public void Set(Sprite sprite, string species, float weightKg, int freshIndex, int price, Action onSell)
        {
            if (icon != null && sprite != null) icon.sprite = sprite;
            if (nameLabel != null) nameLabel.text = species;
            if (weightLabel != null) weightLabel.text = $"{weightKg:0.00}";
            if (priceLabel != null) priceLabel.text = price.ToString();
            if (freshDots != null)
                for (int i = 0; i < freshDots.Length; i++)
                    if (freshDots[i] != null)
                        freshDots[i].color = i == freshIndex ? DotColor(freshIndex) : new Color(1, 1, 1, .22f);
            if (sellButton != null)
            {
                sellButton.onClick.RemoveAllListeners();
                sellButton.onClick.AddListener(() => onSell());
                sellButton.interactable = freshIndex != 2; // rotten can't be sold
            }
        }

        static Color DotColor(int i) => i == 0 ? new Color(.45f, .85f, .45f)   // fresh  = green
                                      : i == 1 ? new Color(.90f, .78f, .32f)   // stale  = amber
                                      :          new Color(.86f, .36f, .34f);  // rotten = red
    }
}
