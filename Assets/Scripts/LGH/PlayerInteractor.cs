using UnityEngine;
using UnityEngine.InputSystem;

namespace LGH
{
    // Put on the Player. Aim at a door and press E to open/close it.
    // When a multiplayer game is running the request goes through DoorNetworkSync so both players see the same door.
    public class PlayerInteractor : MonoBehaviour
    {
        public static PlayerInteractor Active { get; private set; }

        public Transform viewCamera;
        public float reach = 2.6f;
        public LayerMask mask = ~0;

        InteractableDoor focused;
        string message;
        float messageUntil;
        GUIStyle style;

        void Awake()
        {
            if (viewCamera == null)
            {
                Camera cam = GetComponentInChildren<Camera>(true);
                if (cam != null) viewCamera = cam.transform;
            }
        }

        void OnEnable() { Active = this; }
        void OnDisable() { if (Active == this) Active = null; }

        public void ShowMessage(string msg)
        {
            message = msg;
            messageUntil = Time.time + 2f;
        }

        void Update()
        {
            focused = null;
            if (viewCamera == null) return;
            if (Physics.Raycast(viewCamera.position, viewCamera.forward, out RaycastHit hit, reach, mask, QueryTriggerInteraction.Ignore))
                focused = hit.collider.GetComponentInParent<InteractableDoor>();

            var kb = Keyboard.current;
            if (focused == null || kb == null || !kb.eKey.wasPressedThisFrame) return;

            var sync = DoorNetworkSync.Instance;
            if (sync != null && sync.IsSpawned)
            {
                string blocked = focused.CheckBlocked(transform.position);
                if (blocked != null) ShowMessage(blocked);
                else sync.RequestInteract(focused, transform.position);
            }
            else
            {
                string msg = focused.Interact(transform.position);
                if (!string.IsNullOrEmpty(msg)) ShowMessage(msg);
            }
        }

        void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            var v = UiScale.Apply();
            float w = v.x, h = v.y;
            GUI.color = Color.white;
            GUI.Label(new Rect(w / 2f - 10f, h / 2f - 14f, 20f, 28f), "+", style);

            string text = null;
            if (Time.time < messageUntil) text = message;
            else if (focused != null) text = focused.locked ? "[E] 문 (잠김)" : (focused.IsOpen ? "[E] 문 닫기" : "[E] 문 열기");
            if (text == null) return;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(w / 2f - 249f, h * 0.62f + 2f, 500f, 32f), text, style);
            GUI.color = Color.white;
            GUI.Label(new Rect(w / 2f - 250f, h * 0.62f, 500f, 32f), text, style);
        }
    }
}
