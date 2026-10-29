using LGH;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FloorMap
{
    // 벽에 걸린 층별 지도 액자. 평소에도 액자 안에 지도가 보이고, 바라보고 E → 화면 가운데로 확대되어 펼쳐진다.
    // 닫기: E / Esc / Q / 우클릭.
    //
    // 보는 동안: 내 플레이어 이동·시점만 멈춘다(Gim004Journal·LGH PaperNote와 같은 방식). 네트워크·상대 플레이어는 그대로.
    //  커서는 잠근 채로 둔다 → 커서가 풀리면 LGH 메뉴가 일시정지 화면을 띄우기 때문.
    //  Esc로 닫을 때는 이동 스크립트를 한 프레임 뒤에 다시 켠다 → 같은 프레임의 Esc가 일시정지까지 열지 않게.
    //
    // 구성(Tools/KJH/FloorMap/Build Prefabs가 만듦): 루트(이 컴포넌트 + 조준용 BoxCollider)
    //  └ Frame(Prop_PortraitFrame, 가로로 눕힘) └ Map(지도 그림 Quad)
    // 지도 그림을 바꾸려면 mapTexture 칸과 Map의 머티리얼 그림을 같이 바꾸면 된다.
    public class FloorMapBoard : MonoBehaviour, IWorldInteractable
    {
        [Tooltip("확대해서 보여 줄 지도 그림")]
        [SerializeField] Texture2D mapTexture;
        [SerializeField] string prompt = "[E] 지도 보기";
        [Tooltip("확대 화면에서 그림이 차지하는 최대 비율 (화면 기준)")]
        [Range(0.5f, 1f)] [SerializeField] float screenFill = 0.92f;
        [Tooltip("펼쳐지는 시간(초)")]
        [SerializeField] float openSeconds = 0.25f;

        public static FloorMapBoard Current { get; private set; }
        public bool IsOpen { get { return Current == this; } }

        FirstPersonController lockedMove;
        int openedFrame;
        int reenableFrame = -1;
        float openedAt;
        [System.NonSerialized] GUIStyle hintStyle;

        // ---------------- IWorldInteractable ----------------

        public bool CanInteract { get { return mapTexture != null && (Current == null || Current == this); } }
        public string InteractPrompt { get { return IsOpen ? "[E] 지도 닫기" : prompt; } }

        public void Interact(GameObject interactor)
        {
            if (IsOpen) { if (Time.frameCount != openedFrame) Close(false); return; }
            if (Current != null || PaperNote.Current != null) return;
            Current = this;
            openedFrame = Time.frameCount;
            openedAt = Time.unscaledTime;
            lockedMove = interactor != null ? interactor.GetComponentInParent<FirstPersonController>() : null;
            if (lockedMove == null && interactor != null) lockedMove = interactor.GetComponentInChildren<FirstPersonController>();
            if (lockedMove != null) lockedMove.enabled = false;
        }

        void Close(bool delayReenable)
        {
            if (Current != this) return;
            Current = null;
            if (lockedMove == null) return;
            if (delayReenable) reenableFrame = Time.frameCount + 1;
            else { lockedMove.enabled = true; lockedMove = null; }
        }

        void OnDisable()
        {
            Close(false);
            if (lockedMove != null) { lockedMove.enabled = true; lockedMove = null; }
        }

        void Update()
        {
            if (reenableFrame >= 0 && Time.frameCount >= reenableFrame)
            {
                reenableFrame = -1;
                if (lockedMove != null) { lockedMove.enabled = true; lockedMove = null; }
            }
            if (!IsOpen) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool close = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame);
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) close = true;
            if (close) Close(true);
        }

        // ---------------- 화면 ----------------

        void OnGUI()
        {
            if (!IsOpen || mapTexture == null) return;
            GUI.depth = -60;
            float t = openSeconds > 0f ? Mathf.Clamp01((Time.unscaledTime - openedAt) / openSeconds) : 1f;
            float ease = 1f - (1f - t) * (1f - t);

            // 그림은 실제 화면 픽셀 기준으로 그린다 (글자가 작은 지도라 확대 행렬 없이 원본 해상도로)
            GUI.matrix = Matrix4x4.identity;
            float sw = Screen.width, sh = Screen.height;
            GUI.color = new Color(0f, 0f, 0f, 0.75f * ease);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

            // 그림 비율을 유지한 채 화면에 맞추고, 펼쳐지는 동안 작게 시작해 커진다
            float aspect = (float)mapTexture.width / mapTexture.height;
            float maxW = sw * screenFill, maxH = (sh - 40f * UiScale.Factor) * screenFill;
            float w = maxW, h = w / aspect;
            if (h > maxH) { h = maxH; w = h * aspect; }
            float s = Mathf.Lerp(0.55f, 1f, ease);
            w *= s; h *= s;
            var pic = new Rect((sw - w) * 0.5f, (sh - h) * 0.5f - 12f * UiScale.Factor, w, h);

            // 액자 느낌의 어두운 테두리
            float border = 6f * UiScale.Factor;
            GUI.color = new Color(0.25f, 0.16f, 0.09f, ease);
            GUI.DrawTexture(new Rect(pic.x - border, pic.y - border, pic.width + border * 2f, pic.height + border * 2f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, ease);
            GUI.DrawTexture(pic, mapTexture, ScaleMode.StretchToFill);
            GUI.color = Color.white;

            // 안내 문구 (LGH UI와 같은 크기 기준)
            Vector2 v = UiScale.Apply();
            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                hintStyle.normal.textColor = new Color(0.9f, 0.86f, 0.78f);
            }
            GUI.Label(new Rect(0, v.y - 30f, v.x, 22f), "E / Esc / 우클릭: 닫기", hintStyle);
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
