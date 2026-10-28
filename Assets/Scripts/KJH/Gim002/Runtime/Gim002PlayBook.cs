using UnityEngine;
using UnityEngine.InputSystem;

namespace Gim002
{
    // 1층 인형극 놀이책 (OBJ_PLAYBOOK). 바라보고 E → 그 자리에서 바로 펼쳐 읽는다 (손에 들지 않는다).
    // 2026-10-28: 예전엔 공용 들기 시스템으로 집은 뒤 F로 펼쳤는데, 들 필요 없이 바로 읽는 방식으로 변경 (게스트룸 제조법 책과 같은 방식).
    //  - 쪽 구성: 안내 1쪽 + 장면 3쪽 (문구는 전부 Gim002Config / 장면 데이터에서 가져옴)
    //  - A/← 이전 쪽, D/→ 다음 쪽. E·F·Q·Esc로 덮는다. 읽는 동안 이동·시점을 멈춘다.
    //  - 각자 화면에서만 보므로 네트워크 동기화가 필요 없다.
    public class Gim002PlayBook : MonoBehaviour, IWorldInteractable
    {
        [Tooltip("안내 문구에 쓸 이름")]
        [SerializeField] string displayName = "인형극 놀이책";
        [Tooltip("책 덮기 키 (E·Q·Esc로도 덮을 수 있다)")]
        [SerializeField] Key readKey = Key.F;
        [Tooltip("책 제목 글자 크기 (높이 640 화면 기준)")]
        [SerializeField] int titleFontSize = 22;
        [Tooltip("본문 글자 크기 (높이 640 화면 기준). 문장이 길어 넘치면 자동으로 더 작아진다")]
        [SerializeField] int bodyFontSize = 17;
        [Tooltip("펼친 책 그림 (비우면 Resources/KJH_OpenBook). 글은 오른쪽 쪽에 쓴다")]
        [SerializeField] Texture2D bookTexture;

        bool reading;
        int openedFrame = -1;
        int page;
        LGH.FirstPersonController lockedMove;
        // NonSerialized: 에디터가 스크립트를 다시 불러올 때 GUIStyle을 "빈 스타일"로 채워 넣어서,
        // null 검사로는 "이미 만들었다"고 착각해 정렬·글자 크기 설정이 통째로 빠지는 문제가 있었다.
        [System.NonSerialized] GUIStyle titleStyle, bodyStyle, hintStyle;

        static Gim002Config Cfg { get { return Gim002Controller.Instance != null ? Gim002Controller.Instance.Config : null; } }
        int PageCount { get { return Cfg != null ? 1 + Cfg.scenes.Count : 1; } }

        // ---------------- IWorldInteractable ----------------

        public bool CanInteract { get { return isActiveAndEnabled; } }

        public string InteractPrompt { get { return reading ? "[E] 덮기" : "[E] " + displayName + " 읽기"; } }

        public void Interact(GameObject interactor)
        {
            if (reading) { SetReading(false); return; }
            page = 0;
            SetReading(true);
        }

        // 읽는 동안 이동을 멈춘다(게스트룸 제조법 책을 읽을 때와 같은 방식) — 책을 보면서 걸어다니면
        // 화면이 흔들려 글이 잘 안 보이는 문제를 막는다.
        void SetReading(bool value)
        {
            if (reading == value) return;
            reading = value;
            if (value)
            {
                openedFrame = Time.frameCount;
                lockedMove = LGH.PlayerInteractor.Active != null ? LGH.PlayerInteractor.Active.GetComponent<LGH.FirstPersonController>() : null;
                if (lockedMove != null) lockedMove.enabled = false;
            }
            else
            {
                if (lockedMove != null) lockedMove.enabled = true;
                lockedMove = null;
            }
        }

        void Update()
        {
            if (!reading) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (openedFrame == Time.frameCount) return;   // 펼친 그 프레임의 키 입력으로 바로 덮이지 않게
            if (kb[readKey].wasPressedThisFrame || kb.qKey.wasPressedThisFrame) { SetReading(false); return; }
            // 쪽 넘기기는 게스트룸 제조법 책과 같은 방식: A/← 이전 쪽, D/→ 다음 쪽 (2026-10-26 통일)
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) page = Mathf.Max(0, page - 1);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) page = Mathf.Min(PageCount - 1, page + 1);
            if (kb.escapeKey.wasPressedThisFrame) SetReading(false);
        }

        void OnDisable() { SetReading(false); }

        void OnGUI()
        {
            if (!reading) return;
            MakeStyles();
            // LGH UI와 같은 방식: 높이 640 기준 가상 화면에 그리고 GUI.matrix로 실제 해상도에 맞춰 확대한다.
            // → 창 크기와 글자 크기의 비율이 해상도와 상관없이 항상 같다.
            Vector2 v = LGH.UiScale.Apply();
            float w = v.x, h = v.y;

            // 펼친 책 그림 (그림 비율 유지, 화면 가로 90%·세로 86% 안에 맞춤). 글은 전부 오른쪽 쪽에 쓴다.
            var book = OpenBookArt.Fit(w, h);
            GUI.color = Color.white;
            GUI.DrawTexture(book, OpenBookArt.Get(bookTexture), ScaleMode.StretchToFill);
            var rect = OpenBookArt.RightPage(book);

            string title, body;
            PageText(page, out title, out body);
            const float titleH = 34f, hintH = 22f;
            GUI.Label(new Rect(rect.x, rect.y, rect.width, titleH), title, titleStyle);

            // 본문 영역: 제목 아래 ~ 쪽 번호 위. 문장이 길어도 이 안에 다 들어오도록 글씨 크기를 줄여 맞춘다.
            var bodyRect = new Rect(rect.x, rect.y + titleH + 8f, rect.width, rect.height - (titleH + 8f) - (hintH + 8f));
            FitBody(body, bodyRect);
            GUI.Label(bodyRect, body, bodyStyle);

            GUI.Label(new Rect(rect.x, rect.yMax - hintH, rect.width, hintH),
                (page > 0 ? "A ◀ ←   " : "") + (page + 1) + " / " + PageCount + (page < PageCount - 1 ? "   → ▶ D" : "") + "     [E] 덮기", hintStyle);
            GUI.matrix = Matrix4x4.identity;
        }

        // 본문이 영역 높이를 넘지 않을 때까지 글씨를 줄인다 (기본 bodyFontSize에서 시작, 최소 10)
        void FitBody(string body, Rect area)
        {
            var content = new GUIContent(body);
            int size = bodyFontSize;
            bodyStyle.fontSize = size;
            while (size > 10 && bodyStyle.CalcHeight(content, area.width) > area.height)
            {
                size--;
                bodyStyle.fontSize = size;
            }
        }

        // 쪽 번호 → 제목과 본문
        void PageText(int p, out string title, out string body)
        {
            var cfg = Cfg;
            if (cfg == null) { title = "놀이책"; body = ""; return; }
            if (p == 0) { title = "인형극 놀이책"; body = cfg.bookIntro; return; }
            var scene = cfg.GetScene(p);
            title = "장면 " + p;
            body = scene != null ? scene.sentence : "";
        }

        void MakeStyles()
        {
            if (titleStyle != null) return;
            // 글자 크기는 높이 640 가상 화면 기준 (실제 화면에서는 UiScale 배율만큼 커진다)
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = titleFontSize, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(0.3f, 0.18f, 0.1f);
            bodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = bodyFontSize, wordWrap = true };
            bodyStyle.normal.textColor = new Color(0.2f, 0.15f, 0.1f);
            hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            hintStyle.normal.textColor = new Color(0.45f, 0.4f, 0.35f);
            // 화면 가운데(잠긴 커서 자리)가 본문 위라 hover 색(기본 흰색)으로 그려져 글자가 안 보이던 문제 → hover도 같은 색
            titleStyle.hover.textColor = titleStyle.normal.textColor;
            bodyStyle.hover.textColor = bodyStyle.normal.textColor;
            hintStyle.hover.textColor = hintStyle.normal.textColor;
        }
    }
}
