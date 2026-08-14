using UnityEngine;

namespace RustyFishing
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start(){if(Object.FindFirstObjectByType<FishingGameController>()==null)new GameObject("FishingGame").AddComponent<FishingGameController>();}
    }
}
