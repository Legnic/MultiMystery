using LGH;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gim004
{
    // 2층 서고 ReadingTable 위의 온실 기록일지. 바라보고 E → 화면에 펼쳐짐, E / Esc / Q / 우클릭 → 덮기.
    // 한 화면에 왼쪽 "식물 리스트"(사진 1~4번, 숫자만), 오른쪽 "배치 기록"(문장 4줄)이 같이 보인다.
    // 테이블에 고정되어 들고 나갈 수 없다 (2026-10-22 결정: 서고 밖 반출 금지).
    //
    // 읽는 동안: 내 플레이어 이동·시점만 멈춘다(LGH PaperNote와 같은 방식). 네트워크·상대 플레이어는 그대로.
    //  커서는 잠근 채로 둔다 → 커서가 풀리면 LGH 메뉴가 일시정지 화면을 띄우기 때문.
    //  Esc로 덮을 때는 이동 스크립트를 한 프레임 뒤에 다시 켠다 → 같은 프레임의 Esc가 일시정지까지 열지 않게.
    //
    // 내용(제목·사진·문장)은 전부 Gim004Config에서 가져온다. 사진은 Tools/KJH/GIM-004/Capture Journal Photos가 실제 화분 모델을 찍어 만든다.
    // 모델 교체: 자식 "Visual"(Gim004_JournalVisual 프리팹)만 바꾸면 된다. 콜라이더는 루트에 있다.
    public class Gim004Journal : MonoBehaviour, IWorldInteractable
    {
        [Tooltip("기록일지 내용이 담긴 설정 (비우면 씬의 Gim004Controller 설정)")]
        [SerializeField] Gim004Config config;
        [SerializeField] string prompt = "[E] 온실 기록일지 펼쳐보기";
        [Tooltip("펼친 책 그림 (비우면 Resources/KJH_OpenBook). 왼쪽 쪽에 사진, 오른쪽 쪽에 글")]
        [SerializeField] Texture2D bookTexture;

        public static Gim004Journal Current { get; private set; }
        public bool IsOpen { get { return Current == this; } }

        FirstPersonController lockedMove;
        int openedFrame;
        int reenableFrame = -1;
        [System.NonSerialized] GUIStyle titleStyle, headingStyle, bodyStyle, numberStyle, hintStyle;

        Gim004Config Cfg
        {
            get
            {
                if (config != null) return config;
                return Gim004Controller.Instance != null ? Gim004Controller.Instance.Config : null;
            }
        }

        // ---------------- IWorldInteractable ----------------

        public bool CanInteract { get { return Cfg != null && (Current == null || Current == this); } }
        public string InteractPrompt { get { return IsOpen ? "[E] 기록일지 덮기" : prompt; } }

        public void Interact(GameObject interactor)
        {
            if (IsOpen) { if (Time.frameCount != openedFrame) Close(false); return; }
            if (Current != null || PaperNote.Current != null) return;
            Current = this;
            openedFrame = Time.frameCount;
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
            if (!IsOpen) return;
            var cfg = Cfg;
            if (cfg == null) return;
            GUI.depth = -60;
            MakeStyles();
            // LGH UI와 같은 방식: 높이 640 기준 가상 화면에 그리고 실제 해상도에 맞춰 확대
            Vector2 v = UiScale.Apply();
            GUI.color = Color.white; GUI.contentColor = Color.white; GUI.backgroundColor = Color.white;

            // 배경 어둡게
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(new Rect(0, 0, v.x, v.y), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 펼친 책 그림 (그림 비율 유지). 왼쪽 쪽 = 사진, 오른쪽 쪽 = 제목 + 배치 기록 + 안내
            var book = OpenBookArt.Fit(v.x, v.y);
            GUI.DrawTexture(book, OpenBookArt.Get(bookTexture), ScaleMode.StretchToFill);
            var left = OpenBookArt.LeftPage(book);
            var right = OpenBookArt.RightPage(book);

            DrawPhotos(left, cfg);

            GUI.Label(new Rect(right.x, right.y, right.width, 28), cfg.journalTitle, titleStyle);
            var record = new Rect(right.x, right.y + 34f, right.width, right.height - 34f - 24f);
            DrawRecord(record, cfg);

            GUI.Label(new Rect(right.x, right.yMax - 20, right.width, 20), "E / Esc / 우클릭: 덮기", hintStyle);
            GUI.matrix = Matrix4x4.identity;
        }

        // 왼쪽 쪽: 사진 4장 (2×2), 사진 아래에는 숫자만
        void DrawPhotos(Rect r, Gim004Config cfg)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 22), cfg.journalLeftHeading, headingStyle);
            float gridTop = r.y + 28f;
            float cellW = r.width * 0.5f, cellH = (r.yMax - gridTop) * 0.5f;
            const float numberH = 20f, gap = 8f;
            for (int i = 0; i < 4; i++)
            {
                int col = i % 2, row = i / 2;
                var cell = new Rect(r.x + col * cellW, gridTop + row * cellH, cellW, cellH);
                float side = Mathf.Min(cell.width - gap * 2f, cell.height - numberH - gap * 2f);
                var photo = new Rect(cell.center.x - side * 0.5f, cell.y + gap, side, side);

                // 사진 테두리 (흰 인화지 느낌)
                GUI.color = new Color(0.98f, 0.97f, 0.94f);
                GUI.DrawTexture(new Rect(photo.x - 4, photo.y - 4, photo.width + 8, photo.height + 8), Texture2D.whiteTexture);
                GUI.color = Color.white;
                var tex = cfg.photos != null && i < cfg.photos.Length ? cfg.photos[i] : null;
                if (tex != null) GUI.DrawTexture(photo, tex, ScaleMode.ScaleAndCrop);
                else
                {
                    GUI.color = new Color(0.4f, 0.4f, 0.4f);
                    GUI.DrawTexture(photo, Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(photo, "(사진 없음)", hintStyle);
                }
                GUI.Label(new Rect(cell.x, photo.yMax + 4, cell.width, numberH), (i + 1).ToString(), numberStyle);
            }
        }

        // 오른쪽 쪽: 배치 기록 문장
        void DrawRecord(Rect r, Gim004Config cfg)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 22), cfg.journalRightHeading, headingStyle);
            var body = new Rect(r.x + 6, r.y + 40f, r.width - 12, r.height - 46f);
            string text = (cfg.placementRecord ?? "").Replace("\r", "");
            // 문장 사이를 띄워서 한 줄씩 읽기 쉽게
            text = text.Replace("\n", "\n\n");
            FitText(bodyStyle, text, body, 17);
            GUI.Label(body, text, bodyStyle);
        }

        static void FitText(GUIStyle style, string text, Rect area, int startSize)
        {
            var content = new GUIContent(text);
            int size = startSize;
            style.fontSize = size;
            while (size > 10 && style.CalcHeight(content, area.width) > area.height)
            {
                size--;
                style.fontSize = size;
            }
        }

        void MakeStyles()
        {
            if (titleStyle != null) return;
            var ink = new Color(0.22f, 0.14f, 0.08f);
            titleStyle = Style(20, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            headingStyle = Style(15, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            bodyStyle = Style(17, FontStyle.Normal, TextAnchor.UpperLeft, ink);
            bodyStyle.wordWrap = true;
            numberStyle = Style(16, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            hintStyle = Style(12, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.42f, 0.35f, 0.27f));
        }

        static GUIStyle Style(int size, FontStyle fs, TextAnchor anchor, Color color)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = anchor };
            s.normal.textColor = color;
            s.hover.textColor = color;
            return s;
        }
    }
}
