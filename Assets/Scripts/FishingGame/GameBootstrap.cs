using UnityEngine;

namespace RustyFishing
{
    // DISABLED. This used to auto-spawn a FishingGameController at runtime for empty scenes, but it was
    // stacking a duplicate code-generated GameCanvas on top of the baked scene UI. The game now runs solely
    // from the controller placed in the scene. To re-enable auto-spawn for empty scenes, restore the
    // [RuntimeInitializeOnLoadMethod] method below.
    public static class GameBootstrap
    {
        // static void Start()  // intentionally not registered with [RuntimeInitializeOnLoadMethod]
        // {
        //     if (Object.FindFirstObjectByType<FishingGameController>(FindObjectsInactive.Include) != null) return;
        //     if (Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include) != null) return;
        //     new GameObject("FishingGame").AddComponent<FishingGameController>();
        // }
    }
}
