using UnityEngine;
using UnityEngine.InputSystem;

namespace LGH
{
    // Put on the Player. Aim and press E to:
    //   - open/close a door or a drawer
    //   - pick up a candle lying around
    //   - put a candle into a wall holder (from the inventory, or the one in your hand), or take it back out
    // and R to swap the candle in your hand for one from the inventory.
    // When a multiplayer game is running the request goes to the host (DoorNetworkSync / CandleNetworkSync)
    // so both players see the same thing.
    public class PlayerInteractor : MonoBehaviour
    {
        public static PlayerInteractor Active { get; private set; }

        public Transform viewCamera;
        public float reach = 2.6f;
        public LayerMask mask = ~0;

        InteractableDoor focused;
        CandlePickup focusedPickup;
        CandleSconce focusedSconce;
        InteractableDrawer focusedDrawer;
        PlayerInventory inventory;
        HeldCandle held;
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
            inventory = GetComponent<PlayerInventory>();
            held = GetComponentInChildren<HeldCandle>(true);
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
            focusedPickup = null;
            focusedSconce = null;
            focusedDrawer = null;
            if (viewCamera == null) return;
            if (Physics.Raycast(viewCamera.position, viewCamera.forward, out RaycastHit hit, reach, mask, QueryTriggerInteraction.Ignore))
            {
                var c = hit.collider;
                focusedPickup = c.GetComponentInParent<CandlePickup>();
                if (focusedPickup == null) focusedSconce = c.GetComponentInParent<CandleSconce>();
                if (focusedPickup == null && focusedSconce == null) focusedDrawer = c.GetComponentInParent<InteractableDrawer>();
                if (focusedPickup == null && focusedSconce == null && focusedDrawer == null) focused = c.GetComponentInParent<InteractableDoor>();
            }

            var kb = Keyboard.current;
            if (kb == null) return;
            var candles = CandleNetworkSync.Instance;
            if (kb.rKey.wasPressedThisFrame && candles != null)
            {
                string swapMsg = candles.RequestSwapHeld(inventory);
                if (!string.IsNullOrEmpty(swapMsg)) ShowMessage(swapMsg);
            }
            if (!kb.eKey.wasPressedThisFrame) return;

            if (focusedPickup != null || focusedSconce != null || focusedDrawer != null)
            {
                if (candles == null) return;
                string msg = focusedPickup != null ? candles.RequestPickup(focusedPickup, inventory)
                           : focusedSconce != null ? candles.RequestSconce(focusedSconce, inventory)
                           : candles.RequestDrawer(focusedDrawer);
                if (!string.IsNullOrEmpty(msg)) ShowMessage(msg);
                return;
            }
            if (focused == null) return;

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

        string Prompt()
        {
            if (focusedPickup != null) return "[E] 양초 줍기";
            if (focusedSconce != null)
            {
                if (focusedSconce.IsLit) return "[E] 양초 빼기";
                int n = inventory != null ? inventory.Candles : 0;
                if (n > 0) return "[E] 양초 끼우기  (가진 양초 " + n + "개)";
                if (held != null && held.IsBurning) return "[E] 들고 있는 양초 걸기";
                return "빈 양초대 — 양초가 필요하다";
            }
            if (focusedDrawer != null) return focusedDrawer.IsOpen ? "[E] 서랍 닫기" : "[E] 서랍 열기";
            if (focused != null) return focused.locked ? "[E] 문 (잠김)" : (focused.IsOpen ? "[E] 문 닫기" : "[E] 문 열기");
            return null;
        }

        void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            var v = UiScale.Apply();
            float w = v.x, h = v.y;
            GUI.color = Color.white;
            GUI.Label(new Rect(w / 2f - 10f, h / 2f - 14f, 20f, 28f), "+", style);

            string text = Time.time < messageUntil ? message : Prompt();
            if (text == null) return;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(w / 2f - 299f, h * 0.62f + 2f, 600f, 32f), text, style);
            GUI.color = Color.white;
            GUI.Label(new Rect(w / 2f - 300f, h * 0.62f, 600f, 32f), text, style);
        }
    }
}
