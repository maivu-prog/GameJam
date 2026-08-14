using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RustyFishing
{
    // The four upgradeable parts. Order MUST match GameCatalog part handling (engine, hull, hook, hold).
    public enum UpgradePart { Engine, Hull, Hook, Hold }

    /// <summary>
    /// One block per part on the shipyard screen. Pick which part via the <see cref="part"/> dropdown,
    /// then wire only the pieces you use — every field is optional. Because each block names its own
    /// part, the order of the array does NOT matter (no parallel-index bookkeeping).
    /// </summary>
    [Serializable]
    public sealed class UpgradePartUI
    {
        public UpgradePart part;          // which part this block controls (dropdown)
        public Button button;             // round icon on the ship — tap to select this part
        public Image iconImage;           // the icon Image on the button (sprite swaps with selection)
        public Sprite normalSprite;       // icon when NOT selected
        public Sprite selectedSprite;     // icon when selected (the glowing one)
        public GameObject highlight;      // optional glow ring — shown only while selected
        public TMP_Text label;            // optional label → "ENGINE II" (updates with level)
        public GameObject readyIndicator; // optional badge — shown when this part can be upgraded & afforded
        public Sprite bigSprite;          // optional big art shown on the detail card while selected
    }
}
