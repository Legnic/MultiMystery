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
        // 지금 켜져 있는 PlayerInteractor 중 가장 최근 것. 멀티플레이에서 상대 플레이어 복사본은 스폰 직후 곧바로 꺼지는데(NetworkPlayer.Awake),
        // 단순히 "마지막 OnEnable = Active"로 두면 그 순간 내 것이 null로 덮여 모든 안내 문구가 사라진다. 그래서 목록으로 관리한다.
        static readonly System.Collections.Generic.List<PlayerInteractor> enabledList = new System.Collections.Generic.List<PlayerInteractor>();
        public static PlayerInteractor Active { get { return enabledList.Count > 0 ? enabledList[enabledList.Count - 1] : null; } }

        public Transform viewCamera;
        public float reach = 2.6f;
        public LayerMask mask = ~0;
        [Tooltip("화면 중앙에 그릴 조준점 후보들 (비워두면 예전처럼 '+' 글자로 그려진다). 어떤 걸 쓸지/크기/색상은 옵션의 '게임' 탭에서 조절 — " +
                 "OptionsMenuController.crosshairTextures와 순서를 맞출 것.")]
        public Texture2D[] crosshairOptions;
        const float CrosshairBaseSize = 32f;

        Texture2D CurrentCrosshair()
        {
            if (crosshairOptions == null || crosshairOptions.Length == 0) return null;
            int i = Mathf.Clamp(CoopDemo.PlayerOptions.Current.crosshairIndex, 0, crosshairOptions.Length - 1);
            return crosshairOptions[i];
        }

        InteractableDoor focused;
        CandlePickup focusedPickup;
        WorldItem focusedItem;
        CandleSconce focusedSconce;
        InteractableDrawer focusedDrawer;
        IWorldInteractable focusedWorld;   // KJH 도르래 승강기 버튼/창 등
        PortraitTilt focusedPortrait;
        PlayerInventory inventory;
        HeldCandle held;
        string message;
        float messageUntil;
        GUIStyle style, wrapStyle;

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

        void OnEnable() { enabledList.Remove(this); enabledList.Add(this); }
        void OnDisable() { enabledList.Remove(this); }

        public void ShowMessage(string msg)
        {
            ShowMessage(msg, 2f);
        }

        // 표시 시간을 지정하는 버전 (긴 자막용). 기존 호출은 위의 2초 버전이 그대로 처리한다.
        public void ShowMessage(string msg, float seconds)
        {
            message = msg;
            messageUntil = Time.time + seconds;
        }

        void Update()
        {
            focused = null;
            focusedPickup = null;
            focusedItem = null;
            focusedSconce = null;
            focusedDrawer = null;
            focusedPortrait = null;
            focusedWorld = null;
            // 커서가 풀려있다 = 이 플레이어만 일시정지/옵션/소지품 등 메뉴가 떠 있는 상태. 조준·상호작용(E/R)도 멈춘다 (상대 플레이어는 무관).
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (viewCamera == null) return;
            if (PortraitTilt.Current != null) return; // 액자를 살펴보는 동안은 그쪽이 입력(Q/E/F)을 가져간다
            if (RaycastIgnoringSelf(out RaycastHit hit))
            {
                var c = hit.collider;
                focusedPickup = c.GetComponentInParent<CandlePickup>();
                if (focusedPickup == null) focusedSconce = c.GetComponentInParent<CandleSconce>();
                if (focusedPickup == null && focusedSconce == null) focusedDrawer = c.GetComponentInParent<InteractableDrawer>();
                if (focusedPickup == null && focusedSconce == null && focusedDrawer == null)
                {
                    var world = c.GetComponentInParent<IWorldInteractable>();
                    if (world != null && world.CanInteract) focusedWorld = world;
                    if (focusedWorld == null) focusedPortrait = c.GetComponentInParent<PortraitTilt>();
                    if (focusedWorld == null && focusedPortrait == null) focused = c.GetComponentInParent<InteractableDoor>();
                }
                // items lying around (keys, letters...) win over whatever they rest on
                var wi = c.GetComponentInParent<WorldItem>();
                if (wi != null && wi.IsPresent && wi.item != null)
                {
                    focusedItem = wi;
                    focusedPickup = null; focusedSconce = null; focusedDrawer = null; focusedWorld = null; focusedPortrait = null; focused = null;
                }
            }

            var kb = Keyboard.current;
            if (kb == null) return;
            if ((MultiplayerMenu.Typing || GUIUtility.keyboardControl != 0) && Cursor.lockState != CursorLockMode.Locked) return; // typing in a menu text box
            var candles = CandleNetworkSync.Instance;
            if (kb.rKey.wasPressedThisFrame && candles != null && !PistolLoader.WantsR)   // R loads the pistol while you carry it unloaded
            {
                string swapMsg = candles.RequestSwapHeld(inventory);
                if (!string.IsNullOrEmpty(swapMsg)) ShowMessage(swapMsg);
            }
            if (!kb.eKey.wasPressedThisFrame) return;

            if (focusedItem != null)
            {
                var items = ItemNetworkSync.Instance;
                if (items != null) { string im = items.RequestPickup(focusedItem, inventory); if (!string.IsNullOrEmpty(im)) ShowMessage(im); }
                return;
            }
            if (focusedWorld != null) { focusedWorld.Interact(gameObject); return; }
            if (focusedPortrait != null) { focusedPortrait.BeginView(this); return; }
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
                string blocked = focused.CheckBlocked(transform.position, inventory);
                if (blocked != null) ShowMessage(blocked);
                else sync.RequestInteract(focused, transform.position);
            }
            else
            {
                string msg = focused.Interact(transform.position, inventory);
                if (!string.IsNullOrEmpty(msg)) ShowMessage(msg);
            }
        }

        // looking straight down (e.g. standing on a desk) the ray used to hit our own body first
        bool RaycastIgnoringSelf(out RaycastHit best)
        {
            best = default;
            var hits = Physics.RaycastAll(viewCamera.position, viewCamera.forward, reach, mask, QueryTriggerInteraction.Ignore);
            float d = float.MaxValue;
            bool any = false;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance < d) { d = h.distance; best = h; any = true; }
            }
            return any;
        }

        string Prompt()
        {
            if (PortraitTilt.Current != null) return PortraitTilt.Current.ViewPrompt;
            if (focusedItem != null) return focusedItem.Prompt;
            if (focusedWorld != null) return focusedWorld.CanInteract ? focusedWorld.InteractPrompt : null;
            if (focusedPortrait != null) return "[E] 그림 살펴보기";
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
            if (focused != null) return focused.locked ? (focused.CanUnlockWith(inventory) ? "[E] " + focused.keyItem.NameOrFallback + "(으)로 열기" : "[E] 문 (잠김)") : (focused.IsOpen ? "[E] 문 닫기" : "[E] 문 열기");
            return null;
        }

        void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            var v = UiScale.Apply();
            float w = v.x, h = v.y;
            GUI.color = Color.white;
            // 게임 중(커서가 잠겨 있을 때)에만 보인다 — 일시정지/소지품/옵션 등에서는 대신 마우스 커서가 보인다.
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var tex = CurrentCrosshair();
                if (tex != null)
                {
                    float size = CrosshairBaseSize * CoopDemo.PlayerOptions.Current.crosshairSize;
                    GUI.color = CoopDemo.PlayerOptions.Current.crosshairColor;
                    GUI.DrawTexture(new Rect(w / 2f - size / 2f, h / 2f - size / 2f, size, size), tex);
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.Label(new Rect(w / 2f - 10f, h / 2f - 14f, 20f, 28f), "+", style);
                }
            }

            string text = Time.time < messageUntil ? message : Prompt();
            if (text == null) return;
            // long messages wrap onto more lines instead of being cut off
            if (wrapStyle == null) wrapStyle = new GUIStyle(style) { wordWrap = true };
            float bw = Mathf.Min(w - 40f, 820f);
            float bh = Mathf.Max(32f, wrapStyle.CalcHeight(new GUIContent(text), bw));
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(w / 2f - bw / 2f + 1f, h * 0.62f + 2f, bw, bh), text, wrapStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(w / 2f - bw / 2f, h * 0.62f, bw, bh), text, wrapStyle);
        }
    }
}
