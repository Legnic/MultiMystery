using UnityEngine;

namespace LGH
{
    // A drawer that slides out of its furniture when you press E on it (and back in on the next press).
    // Put it on the moving drawer object; it slides along the parent's forward axis.
    // In multiplayer CandleNetworkSync passes the request to the host and every player calls SetOpen().
    public class InteractableDrawer : MonoBehaviour
    {
        public float openDistance = 0.3f;
        public float speed = 1.4f;   // metres per second

        public bool IsOpen { get; private set; }

        Vector3 closedPos;
        float current;
        bool init;

        void Init()
        {
            if (init) return;
            init = true;
            closedPos = transform.localPosition;
        }

        void Awake() { Init(); }

        public void SetOpen(bool open, bool instant = false)
        {
            Init();
            IsOpen = open;
            if (instant) { current = open ? openDistance : 0f; Apply(); }
        }

        void Update()
        {
            float target = IsOpen ? openDistance : 0f;
            if (Mathf.Approximately(current, target)) return;
            current = Mathf.MoveTowards(current, target, speed * Time.deltaTime);
            Apply();
        }

        void Apply() { transform.localPosition = closedPos + Vector3.forward * current; }
    }
}
