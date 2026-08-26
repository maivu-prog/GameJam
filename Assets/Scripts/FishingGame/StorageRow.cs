using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    /// <summary>
    /// One row of the storage list = ONE caught fish, never a stack. Put this on your row prefab and wire
    /// whichever pieces the design actually has; every field is optional and unassigned ones are skipped,
    /// so the prefab layout is entirely yours.
    ///
    /// Same deal as <see cref="MarketRow"/>, and the split is the same: the row knows how to paint itself,
    /// the panel knows how many rows there are, and neither builds any layout.
    ///
    /// freshDots: 3 dots, [0]=Fresh [1]=Stale [2]=Rotten — the fish's own level lights, the rest dim.
    /// </summary>
    public sealed class StorageRow : MonoBehaviour
    {
        [Tooltip("Ảnh loài cá. Game tự đổi sprite, kể cả bản '-rotten' khi cá đã hỏng.")]
        [SerializeField] Image icon;
        [Tooltip("Tên loài, ví dụ 'SILVER SARDINE'.")]
        [SerializeField] TMP_Text nameLabel;
        [Tooltip("Cân nặng, ví dụ '2.1 kg'.")]
        [SerializeField] TMP_Text weightLabel;
        [Tooltip("Độ tươi bằng CHỮ: Fresh / Stale / Rotten. Bỏ trống nếu bạn dùng freshDots.")]
        [SerializeField] TMP_Text freshLabel;
        [Tooltip("Giá bán ước tính của riêng con này.")]
        [SerializeField] TMP_Text priceLabel;
        [Tooltip("3 chấm độ tươi: [0] Fresh, [1] Stale, [2] Rotten. Bỏ trống nếu dùng freshLabel.")]
        [SerializeField] Image[] freshDots;
        [Tooltip("Bấm để vứt con cá này xuống biển. Thường phủ toàn bộ hàng.")]
        [SerializeField] Button tossButton;
        [Tooltip("Hiện khi cá đã Rotten — ví dụ một lớp phủ xám hoặc chữ 'HỎNG'.")]
        [SerializeField] GameObject rottenMark;

        /// <summary>Paint this row. `onToss` is null when tossing is not allowed right now.</summary>
        public void Set(Sprite sprite, string species, float weightKg, int freshIndex, int price, Action onToss)
        {
            if (icon != null && sprite != null) icon.sprite = sprite;
            if (nameLabel != null) nameLabel.text = species;
            if (weightLabel != null) weightLabel.text = $"{weightKg:0.0} kg";
            if (priceLabel != null) priceLabel.text = price > 0 ? $"{price}c" : "—";
            if (freshLabel != null) freshLabel.text = FreshName(freshIndex);
            if (rottenMark != null) rottenMark.SetActive(freshIndex == 2);

            if (freshDots != null)
                for (int i = 0; i < freshDots.Length; i++)
                    if (freshDots[i] != null)
                        freshDots[i].color = i == freshIndex ? DotColor(freshIndex) : new Color(1, 1, 1, .22f);

            if (tossButton != null)
            {
                tossButton.onClick.RemoveAllListeners();
                if (onToss != null) tossButton.onClick.AddListener(() => onToss());
                tossButton.interactable = onToss != null;
            }
        }

        static string FreshName(int i) => i == 0 ? "Fresh" : i == 1 ? "Stale" : "Rotten";

        static Color DotColor(int i) => i == 0 ? new Color(.45f, .85f, .45f)   // fresh  = green
                                      : i == 1 ? new Color(.90f, .78f, .32f)   // stale  = amber
                                      :          new Color(.86f, .36f, .34f);  // rotten = red
    }
}
