using UnityEngine;

namespace LGH
{
    // An item lying in the world. Look at it and press E to put it in your inventory.
    // Put this on any object with a collider and pick an ItemData.
    // It is never destroyed: picking up just hides it, dropping shows it again somewhere else,
    // so ItemNetworkSync can keep every player's view the same.
    public class WorldItem : MonoBehaviour
    {
        public ItemData item;
        [Min(1)] public int amount = 1;

        public bool IsPresent { get; private set; } = true;
        public int Amount { get; private set; }

        Renderer[] renderers;
        Collider[] colliders;
        Light[] lights;

        void Awake() { Cache(); Amount = amount; }

        void Cache()
        {
            if (renderers != null) return;
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            lights = GetComponentsInChildren<Light>(true);
        }

        public string Prompt
        {
            get
            {
                if (item == null) return null;
                return Amount > 1 ? "[E] " + item.NameOrFallback + " 줍기 (" + Amount + ")" : "[E] " + item.NameOrFallback + " 줍기";
            }
        }

        public void SetState(bool present, int count)
        {
            Cache();
            if (IsPresent && !present) PickupSound.Queue(this);
            IsPresent = present;
            Amount = Mathf.Max(1, count);
            foreach (var r in renderers) if (r != null) r.enabled = present;
            foreach (var c in colliders) if (c != null) c.enabled = present;
            foreach (var l in lights) if (l != null) l.enabled = present;
        }

        // which LCW clip to use when this item is picked up
        public AudioClip PickupClip
        {
            get
            {
                var b = LghSfx.Bank;
                if (b == null || item == null) return null;
                string id = ((item.itemId ?? "") + " " + item.name).ToLowerInvariant();
                bool paper = item.Readable || id.Contains("note") || id.Contains("paper") || id.Contains("letter") || id.Contains("page") || id.Contains("diary") || id.Contains("photo") || id.Contains("map");
                return paper ? b.paperPickup : b.metalPickup;
            }
        }

        public void SetPose(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
