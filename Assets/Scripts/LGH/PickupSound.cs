using System.Collections.Generic;
using UnityEngine;

namespace LGH
{
    // Plays the pickup sound for an item that just vanished from the world.
    // When a late joiner receives the whole item state at once, many items vanish in one frame: then stay quiet.
    public class PickupSound : MonoBehaviour
    {
        static PickupSound runner;
        static readonly List<WorldItem> queued = new List<WorldItem>();

        public static void Queue(WorldItem item)
        {
            if (!Application.isPlaying || item == null) return;
            if (runner == null) { var go = new GameObject("LGH_PickupSound"); DontDestroyOnLoad(go); runner = go.AddComponent<PickupSound>(); }
            queued.Add(item);
        }

        void LateUpdate()
        {
            if (queued.Count == 1 && queued[0] != null) LghSfx.At(queued[0].PickupClip, queued[0].transform.position, 0.8f, 8f);
            queued.Clear();
        }
    }
}
