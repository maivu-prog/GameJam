using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    /// <summary>
    /// The storage window, authored BY HAND in the scene. This component owns no layout and creates no
    /// art — you build the panel however it should look, drag each piece into the slots below, and the
    /// game only ever writes text, spawns rows into your content object, and toggles things on and off.
    ///
    /// Same deal as the Title Screen and the Ledger, for the same reason: a panel is a design job, and
    /// generated coordinates never survive contact with real art. Every slot is optional — leave one empty
    /// and that piece is skipped, nothing throws, so it can be wired a little at a time.
    ///
    /// Leave the whole component unassigned on the controller and the game falls back to the old
    /// runtime-built modal, so the basket-full case still works while the panel is half-built.
    ///
    /// The panel serves two situations and knows which it is in:
    ///   - opened by the player (the storage button) — browsing, tossing allowed
    ///   - forced open because the hold overflowed — cannot be closed until it is back under capacity
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoragePanelView : MonoBehaviour
    {
        [Header("Khung")]
        [Tooltip("Cả bảng storage. Game bật/tắt object này. Kéo panel gốc vào đây.")]
        [SerializeField] GameObject root;
        [Tooltip("Nền mờ phía sau, dùng để chặn click ra ngoài. Không bắt buộc.")]
        [SerializeField] GameObject dimmer;

        [Header("Tiêu đề")]
        [Tooltip("Dòng tiêu đề. Bình thường là 'STORAGE', khi tràn khoang đổi thành 'BASKET FULL'.")]
        [SerializeField] TMP_Text title;
        [Tooltip("Dòng phụ, ví dụ 'Storage 19/18 — tap a fish to toss it'.")]
        [SerializeField] TMP_Text subtitle;
        [Tooltip("CHỈ con số, ví dụ '19/18'. Dùng khi bạn muốn tách số ra khỏi dòng phụ.")]
        [SerializeField] TMP_Text countLabel;
        [Tooltip("Tổng giá trị ước tính của cả khoang. CHỈ con số — icon xu đặt riêng cạnh nó.")]
        [SerializeField] TMP_Text totalValueLabel;
        [Tooltip("Thanh fill 0..1 theo độ đầy khoang. Image phải để Type = Filled.")]
        [SerializeField] Image fillBar;

        [Header("Danh sách")]
        [Tooltip("Prefab một hàng, có sẵn component StorageRow.")]
        [SerializeField] StorageRow rowPrefab;
        [Tooltip("Object chứa các hàng — thường là 'Content' bên trong ScrollRect.")]
        [SerializeField] RectTransform rowParent;
        [Tooltip("Hiện khi khoang trống, ví dụ chữ 'Chưa có gì trong khoang'.")]
        [SerializeField] GameObject emptyState;

        [Header("Nút")]
        [Tooltip("Nút đóng. Game tự tắt nó khi khoang đang tràn, vì lúc đó bắt buộc phải vứt bớt.")]
        [SerializeField] Button closeButton;
        [Tooltip("Vứt hết cá đã hỏng. Game tự ẩn khi trong khoang không có con nào hỏng.")]
        [SerializeField] Button tossRottenButton;

        [Header("Cảnh báo tràn khoang")]
        [Tooltip("Hiện KHI VÀ CHỈ KHI khoang vượt sức chứa — ví dụ viền đỏ hoặc dòng 'Vứt bớt đi'.")]
        [SerializeField] GameObject overflowMark;

        readonly List<StorageRow> spawned = new();
        bool mockRowsCleared;

        /// <summary>
        /// Hide anything already sitting under the row parent. Laying a couple of example rows into the
        /// content object is the natural way to design this panel, but they are not fish -- left alone
        /// they stay on screen for ever and read as phantom catches ("I caught one and storage says 3").
        /// Done once, and by deactivating rather than destroying, so the mock-ups survive for next time
        /// you open the scene.
        /// </summary>
        void ClearMockRows()
        {
            if (mockRowsCleared || rowParent == null) return;
            mockRowsCleared = true;
            for (int i = rowParent.childCount - 1; i >= 0; i--)
                rowParent.GetChild(i).gameObject.SetActive(false);
        }

        /// <summary>True when the scene has enough wired for this view to be worth using at all.</summary>
        public bool Usable => root != null && rowPrefab != null && rowParent != null;

        public bool IsOpen => root != null && root.activeSelf;

        public void Bind(Action onClose, Action onTossRotten)
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                if (onClose != null) closeButton.onClick.AddListener(() => onClose());
            }
            if (tossRottenButton != null)
            {
                tossRottenButton.onClick.RemoveAllListeners();
                if (onTossRotten != null) tossRottenButton.onClick.AddListener(() => onTossRotten());
            }
        }

        public void SetVisible(bool on)
        {
            if (root != null) root.SetActive(on);
            if (dimmer != null) dimmer.SetActive(on);
        }

        /// <summary>
        /// Repaint the whole list. Rows are pooled rather than destroyed and rebuilt: tossing one fish
        /// repaints the panel, and rebuilding a 20-row list on every tap would restart any layout or
        /// animation on the rows each time.
        /// </summary>
        public void Show(IReadOnlyList<StorageEntry> entries, int capacity, bool overflowing, Action<int> onToss)
        {
            ClearMockRows();
            int n = entries?.Count ?? 0;
            if (GameCatalog.debugStorage)
            {
                int liveChildren = 0;
                if (rowParent != null)
                    for (int i = 0; i < rowParent.childCount; i++)
                        if (rowParent.GetChild(i).gameObject.activeSelf) liveChildren++;
                Debug.Log($"[KHOANG] ca trong save={n}  suc chua={capacity}  tran={overflowing}  "
                        + $"| hang da tao={spawned.Count}  hang dang hien duoi Content={liveChildren}  "
                        + $"tong con cua Content={(rowParent != null ? rowParent.childCount : 0)}", this);
            }

            if (title != null) title.text = overflowing ? "BASKET FULL" : "STORAGE";
            if (countLabel != null) countLabel.text = $"{n}/{capacity}";
            if (subtitle != null)
                subtitle.text = overflowing
                    ? $"Storage {n}/{capacity} — tap a fish to toss it"
                    : $"Storage {n}/{capacity}";
            if (fillBar != null) fillBar.fillAmount = capacity > 0 ? Mathf.Clamp01((float)n / capacity) : 0f;
            if (overflowMark != null) overflowMark.SetActive(overflowing);
            if (emptyState != null) emptyState.SetActive(n == 0);

            // Closing is refused while over capacity: the fish are already caught, and the only way out
            // is to put some back. Hiding the button says that better than a message would.
            if (closeButton != null) closeButton.gameObject.SetActive(!overflowing);

            int total = 0, rotten = 0;
            for (int i = 0; i < n; i++)
            {
                total += entries[i].price;
                if (entries[i].freshIndex == 2) rotten++;
            }
            if (totalValueLabel != null) totalValueLabel.text = total.ToString();
            if (tossRottenButton != null) tossRottenButton.gameObject.SetActive(rotten > 0);

            while (spawned.Count < n)
            {
                var row = Instantiate(rowPrefab, rowParent);
                row.gameObject.SetActive(true);
                spawned.Add(row);
            }
            if (GameCatalog.debugStorage && rowParent != null)
            {
                // Anything under Content that this component did not create is a leftover from design time.
                int stray = rowParent.childCount - spawned.Count;
                if (stray > 0)
                    Debug.LogWarning($"[KHOANG] Content dang co {stray} object KHONG phai do code tao ra "
                                   + "(hang mau dung tay khi dung layout?). Chung se bi tat, nhung nen xoa "
                                   + "hoac chuyen rowPrefab thanh prefab asset de ngoai Content.", this);
            }
            for (int i = 0; i < spawned.Count; i++)
            {
                bool used = i < n;
                if (spawned[i].gameObject.activeSelf != used) spawned[i].gameObject.SetActive(used);
                if (!used) continue;
                var entry = entries[i];
                int index = entry.index;
                spawned[i].Set(entry.sprite, entry.species, entry.weightKg, entry.freshIndex, entry.price,
                               onToss == null ? null : () => onToss(index));
            }
        }
    }

    /// <summary>One line of the storage list, as the controller hands it over.</summary>
    public struct StorageEntry
    {
        public int index;          // position in save.Data.cargo — what toss needs
        public Sprite sprite;
        public string species;
        public float weightKg;
        public int freshIndex;     // 0 Fresh, 1 Stale, 2 Rotten
        public int price;
    }
}
