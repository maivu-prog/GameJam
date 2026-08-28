using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    /// <summary>
    /// The one-line hint strip, authored BY HAND in the scene. Owns no layout and creates nothing —
    /// build the little banner however it should look, drag the pieces in, and the game only writes text
    /// and toggles it on and off.
    ///
    /// Deliberately ONE line with no button and no "Next". A hint appears because the player is standing
    /// in the situation it describes, and leaves the moment they do the thing. Nothing is ever blocked and
    /// nothing has to be dismissed, so a player who already knows the game never has to acknowledge it.
    ///
    /// Leave the component unassigned on the controller and the hints simply never show; the game is
    /// unaffected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialHintView : MonoBehaviour
    {
        [Header("Khung gợi ý")]
        [Tooltip("Cả dải gợi ý. Game bật/tắt object này.")]
        [SerializeField] GameObject root;
        [Tooltip("Dòng chữ hướng dẫn. Nên bật Auto Size để câu dài không tràn.")]
        [SerializeField] TMP_Text label;
        [Tooltip("Icon nhỏ bên trái, nếu thiết kế có. Không bắt buộc.")]
        [SerializeField] Image icon;

        [Header("Hiện ra")]
        [Tooltip("Giây để mờ dần hiện ra và mờ dần biến mất. Để 0 là bật/tắt tức thì.")]
        [SerializeField] float fadeSeconds = .25f;

        CanvasGroup group;
        string current = "";
        float alpha;

        /// <summary>
        /// Visibility is driven by CanvasGroup alpha, NOT by SetActive.
        ///
        /// The obvious way round is a trap: `root` is usually the very object this component sits on, so
        /// deactivating it disables the component too, Update stops running, and the strip can never fade
        /// back in. It hides itself once and stays hidden for the rest of the session.
        ///
        /// Alpha has none of that problem, and with blocksRaycasts off an invisible strip is already inert.
        /// </summary>
        void Awake()
        {
            if (root == null) return;
            ConfigureLayout();
            group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
            // A hint must never eat a touch: the player is being told to press something, and the strip
            // often sits right over the control it is talking about.
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;
            // Only safe when root is something else -- see above.
            if (root != gameObject && !root.activeSelf) root.SetActive(true);
        }

        void ConfigureLayout()
        {
            if (root.transform is RectTransform rootRect)
            {
                rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(.5f, .5f);
                rootRect.anchoredPosition = new Vector2(0f, -390f);
                rootRect.sizeDelta = new Vector2(900f, 140f);
            }

            var background = root.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.08f, 0.045f, 0.055f, .82f);
                background.raycastTarget = false;
            }

            if (label == null) return;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(.5f, .5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(-48f, -20f);
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = 24f;
            label.fontSizeMax = 42f;
            label.raycastTarget = false;
        }

        /// <summary>Pass the hint text, or null/empty for "nothing to say right now".</summary>
        public void Show(string text, Sprite art = null)
        {
            if (root == null)
            {
                if (GameCatalog.debugStorage && !string.IsNullOrEmpty(text))
                    Debug.LogWarning("[HINT] co goi y muon hien nhung o 'root' chua duoc keo vao: " + text, this);
                return;
            }
            if (text == current) return;
            current = text ?? "";
            if (label != null && !string.IsNullOrEmpty(current)) label.text = current;
            if (icon != null)
            {
                if (art != null) icon.sprite = art;
                icon.gameObject.SetActive(art != null);
            }
        }

        void Update()
        {
            if (root == null) return;
            bool want = !string.IsNullOrEmpty(current);
            float target = want ? 1f : 0f;
            alpha = fadeSeconds > 0f
                  ? Mathf.MoveTowards(alpha, target, Time.deltaTime / fadeSeconds)
                  : target;
            if (group != null) group.alpha = alpha;
        }
    }
}
