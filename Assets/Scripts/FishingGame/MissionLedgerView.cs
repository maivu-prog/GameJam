using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    /// <summary>Everything the view needs to paint one frame of the Ledger. Filled by the controller.</summary>
    public struct LedgerModel
    {
        public Sprite portrait;
        public string npcName, npcRole, dialogue;
        public string title, description, objectives, whereLine, reward;
        public bool showStamp;      // objectives all met
        public bool showClaim;      // ...and we are standing in the right port
        public bool showAccept;     // mission offered but not yet accepted
        public bool tracking;       // sea note currently shown
    }

    /// <summary>
    /// The mission UI, authored BY HAND in the scene. This component owns no layout and creates nothing —
    /// you build the panel however it should look, drag each piece into the slots below, and the game only
    /// ever writes text and toggles things on and off.
    ///
    /// That is the same deal the Title Screen has, and for the same reason: a panel is a design job, and
    /// generated coordinates never survive contact with real art. Leave a slot empty and that piece is
    /// simply skipped — nothing here throws, so you can wire it up a little at a time and hit Play whenever.
    ///
    /// Leave the whole component off the controller and the mission system still runs; you just have no
    /// window into it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MissionLedgerView : MonoBehaviour
    {
        [Header("Khung LEDGER")]
        [Tooltip("Cả bảng LEDGER. Game bật/tắt object này. Kéo panel gốc vào đây.")]
        [SerializeField] GameObject root;

        [Header("NPC — người giao / nhận nhiệm vụ")]
        [Tooltip("Ô chân dung. Game tự đổi sprite theo NPC đang nói.")]
        [SerializeField] Image portrait;
        [Tooltip("Tên NPC, ví dụ 'Mara Vale'.")]
        [SerializeField] TMP_Text npcName;
        [Tooltip("Chức danh, ví dụ 'Home Harbormaster'.")]
        [SerializeField] TMP_Text npcRole;
        [Tooltip("2–3 câu thoại. Nên bật Auto Size cho TMP để thoại dài không tràn.")]
        [SerializeField] TMP_Text dialogue;

        [Header("Card nhiệm vụ")]
        [Tooltip("Tên nhiệm vụ, ví dụ 'A FISHER'S MORNING'.")]
        [SerializeField] TMP_Text title;
        [Tooltip("Mô tả một dòng.")]
        [SerializeField] TMP_Text description;
        [Tooltip("Danh sách objective, mỗi dòng một mục kèm x/y. Để căn TRÁI và bật Auto Size.")]
        [SerializeField] TMP_Text objectives;
        [Tooltip("Dòng 'Hand in at Home Harbor'.")]
        [SerializeField] TMP_Text whereLine;
        [Tooltip("CHỈ con số tiền thưởng, ví dụ '60'. Icon xu là Image riêng ở ô dưới.")]
        [SerializeField] TMP_Text reward;
        [Tooltip("Icon đồng xu đặt cạnh số thưởng (Art/UI/Harbor/coin-icon). Bỏ trống thì số tự hiện '60c'.")]
        [SerializeField] Image rewardCoinIcon;
        [Tooltip("Con dấu READY. Game bật khi xong hết objective, tắt lúc khác.")]
        [SerializeField] GameObject readyStamp;

        [Header("Nút")]
        [Tooltip("Nút CLAIM. Tự ẩn khi chưa xong hoặc đang đứng sai cảng.")]
        [SerializeField] Button claimButton;
        [Tooltip("Nút TRACK — CŨNG là nút ACCEPT. Chưa nhận: chữ 'ACCEPT' (bấm để nhận). Đã nhận: 'TRACK'/'UNTRACK' bật/tắt note.")]
        [SerializeField] Button trackButton;
        [Tooltip("Chữ trên nút track/accept — game đổi giữa ACCEPT / TRACK / UNTRACK. Bỏ trống nếu không cần đổi.")]
        [SerializeField] TMP_Text trackButtonLabel;
        [SerializeField] Button closeButton;

        // Ba trạng thái, đi thẳng một chiều:
        //   ẨN HẲN  ──[Tracker Toggle]──▶  NOTE NHỎ  ──[bấm vào note]──▶  BẢNG LEDGER
        // Note chỉ có MỘT cỡ, do bạn dựng trong scene — game không đổi cỡ nó bao giờ.
        [Header("Note ngoài biển")]
        [Tooltip("Tờ note nhỏ trên màn biển. Kéo object gốc vào đây. Mặc định ẨN cho tới khi bấm nút toggle.")]
        [SerializeField] GameObject trackerRoot;
        [Tooltip("Tên nhiệm vụ đang theo.")]
        [SerializeField] TMP_Text trackerTitle;
        [Tooltip("Các dòng objective kèm x/y.")]
        [SerializeField] TMP_Text trackerLines;
        [Tooltip("Nút phủ lên tờ note — chạm vào là MỞ BẢNG LEDGER đầy đủ. " +
                 "Gắn Button lên chính object note rồi kéo vào đây.")]
        [SerializeField] Button trackerTapButton;

        [Tooltip("Nút ẨN/HIỆN tờ note — ví dụ bảng SAFE/DANGER ngoài biển. Gắn Button lên object đó " +
                 "rồi kéo vào đây. Nhớ bật Raycast Target trên Image của nó, không thì bấm không ăn. " +
                 "Không cần set OnClick tay — game tự gán.")]
        [SerializeField] Button trackerToggleButton;

        [Header("Con dấu tiến độ (ngoài biển)")]
        [Tooltip("Con dấu nháy ~0.8s mỗi khi có tiến triển. Bỏ trống thì không có hiệu ứng.")]
        [SerializeField] GameObject progressStamp;

        /// <summary>The stamp quad, for the press animation. Null when no stamp was wired.</summary>
        /// <summary>The TRACK/ACCEPT button rect, for the tutorial to point at. Null if not wired.</summary>
        public RectTransform TrackButtonRect => trackButton != null ? (RectTransform)trackButton.transform : null;
        /// <summary>The CLOSE ("Back to Harbour") button rect, for the tutorial to point at. Null if not wired.</summary>
        public RectTransform CloseButtonRect => closeButton != null ? (RectTransform)closeButton.transform : null;
        /// <summary>The sea note's "see more" (Tracker Tap) button rect. Null if not wired.</summary>
        public RectTransform TrackerTapButtonRect => trackerTapButton != null ? (RectTransform)trackerTapButton.transform : null;
        /// <summary>The sea note root rect, for the tap-outside-to-close test. Null if not wired.</summary>
        public RectTransform TrackerRootRect => trackerRoot != null ? (RectTransform)trackerRoot.transform : null;
        public Transform ProgressStampTransform => progressStamp != null ? progressStamp.transform : null;
        public Transform ReadyStampTransform => readyStamp != null ? readyStamp.transform : null;
        public bool IsOpen => root != null && root.activeSelf;

        /// <summary>
        /// Wire the buttons once, at startup. Listeners are cleared first so a second call — a scene reload,
        /// a hot edit — cannot leave the same action stacked twice on one press.
        /// </summary>
        /// <param name="track">Show/hide the sea note. Bound to BOTH the Ledger's TRACK button and the
        /// optional toggle out at sea, so the two never drift out of step.</param>
        /// <param name="tapNote">Open the full Ledger from the note.</param>
        // trackOrAccept: the TRACK button — accepts the mission when it is still offered, otherwise toggles
        // the sea note. boardToggle: the SAFE/DANGER board out at sea, which only ever toggles the note.
        public void BindButtons(Action claim, Action trackOrAccept, Action close, Action tapNote, Action boardToggle)
        {
            Hook(claimButton, claim);
            Hook(trackButton, trackOrAccept);
            Hook(closeButton, close);
            Hook(trackerTapButton, tapNote);
            Hook(trackerToggleButton, boardToggle);
        }

        static void Hook(Button b, Action a)
        {
            if (b == null || a == null) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => a());
        }

        float popT; const float PopSeconds = 0.22f;
        public void SetOpen(bool on) { if (root != null) root.SetActive(on); if (on) popT = PopSeconds; }

        void Update()
        {
            if (popT > 0f && root != null)
            {
                popT -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(1f - popT / PopSeconds);   // 0 → 1
                root.transform.localScale = Vector3.one * PopScale(k);
                if (popT <= 0f) root.transform.localScale = Vector3.one;
            }
            if (trackerPopT > 0f && trackerRoot != null)
            {
                trackerPopT -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(1f - trackerPopT / PopSeconds);
                trackerRoot.transform.localScale = Vector3.one * PopScale(k);
                if (trackerPopT <= 0f) trackerRoot.transform.localScale = Vector3.one;
            }
        }

        // Ease-out-back: grows past 1 then settles, for a little "pop".
        public static float PopScale(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f, from = 0.84f;
            float x = k - 1f;
            return from + (1f - from) * (1f + c3 * x * x * x + c1 * x * x);
        }

        /// <summary>Paint one frame of the panel. Every slot is optional — an empty one is skipped.</summary>
        public void Paint(in LedgerModel m)
        {
            if (portrait != null)
            {
                if (m.portrait != null) portrait.sprite = m.portrait;
                portrait.gameObject.SetActive(m.portrait != null);
            }
            Write(npcName, m.npcName);
            Write(npcRole, m.npcRole);
            Write(dialogue, m.dialogue);

            Write(title, m.title);
            Write(description, m.description);
            Write(objectives, m.objectives);
            Write(whereLine, m.whereLine);
            // Coin icon wired up? Then the number stands alone. Otherwise it has to carry its own unit
            // so the reward never reads as a bare, unitless figure.
            bool hasCoin = rewardCoinIcon != null;
            if (hasCoin) rewardCoinIcon.gameObject.SetActive(!string.IsNullOrEmpty(m.reward));
            Write(reward, hasCoin || string.IsNullOrEmpty(m.reward) ? m.reward : m.reward + "c");

            if (readyStamp != null) readyStamp.SetActive(m.showStamp);
            if (claimButton != null) claimButton.gameObject.SetActive(m.showClaim);
            // The track button doubles as ACCEPT while the mission is still offered.
            if (trackButtonLabel != null) trackButtonLabel.text = m.showAccept ? "ACCEPT" : (m.tracking ? "UNTRACK" : "TRACK");
        }

        bool trackerWasShown;
        float trackerPopT;

        /// <summary>The sea note — one fixed size, shown or not shown. Pops when it first appears.</summary>
        public void PaintTracker(bool show, string titleText, string lines)
        {
            if (trackerRoot != null && trackerRoot.activeSelf != show) trackerRoot.SetActive(show);
            if (show && !trackerWasShown) trackerPopT = PopSeconds;   // pop on appear
            trackerWasShown = show;
            if (!show) return;
            Write(trackerTitle, titleText);
            Write(trackerLines, lines);
        }

        public void SetProgressStamp(bool on) { if (progressStamp != null) progressStamp.SetActive(on); }

        static void Write(TMP_Text t, string s) { if (t != null) t.text = s ?? ""; }

        /// <summary>
        /// Editor convenience: prints which slots are still empty so you are not hunting a blank panel at
        /// runtime wondering which drag you missed. Right-click the component header to run it.
        /// </summary>
        [ContextMenu("Kiểm slot còn trống")]
        void ReportEmptySlots()
        {
            var sb = new System.Text.StringBuilder("[MissionLedgerView] slot chưa kéo: ");
            int n = 0;
            void Check(string name, UnityEngine.Object o) { if (o == null) { sb.Append(name).Append(", "); n++; } }
            Check("root", root); Check("portrait", portrait); Check("npcName", npcName);
            Check("npcRole", npcRole); Check("dialogue", dialogue); Check("title", title);
            Check("description", description); Check("objectives", objectives);
            Check("whereLine", whereLine); Check("reward", reward); Check("readyStamp", readyStamp);
            Check("claimButton", claimButton); Check("trackButton", trackButton);
            Check("closeButton", closeButton); Check("trackerRoot", trackerRoot);
            Check("trackerTitle", trackerTitle);
            Check("trackerLines", trackerLines); Check("trackerTapButton", trackerTapButton);
            Check("trackerToggleButton", trackerToggleButton);
            Check("progressStamp", progressStamp);
            Debug.Log(n == 0 ? "[MissionLedgerView] đủ hết slot." : sb.ToString().TrimEnd(',', ' '), this);
        }
    }
}
