using UnityEngine;
using UnityEngine.InputSystem;

namespace Gim002
{
    // 1층 인형극 놀이책 (OBJ_PLAYBOOK). 공용 들기 시스템으로 집고, 들고 있는 동안 F키로 책을 펼쳐 읽는다.
    //  - 쪽 구성: 안내 1쪽 + 장면 3쪽 (문구는 전부 Gim002Config / 장면 데이터에서 가져옴)
    //  - ←/→ 키로 쪽을 넘긴다. 들고 다니며 피아노 앞에서도 읽을 수 있다.
    //  - 아무 곳에나 내려놓을 수 없고 원래 자리(책장)에만 둘 수 있어서 잃어버리지 않는다.
    // 읽기 키(F)는 R이 이미 LGH의 '양초 바꾸기'에 쓰이고 있어서 F로 정했다(2026-10-20 결정).
    public class Gim002PlayBook : HoldableItem
    {
        [Tooltip("책 펼치기/덮기 키")]
        [SerializeField] Key readKey = Key.F;
        [Tooltip("책 제목 글자 크기 (높이 640 화면 기준)")]
        [SerializeField] int titleFontSize = 22;
        [Tooltip("본문 글자 크기 (높이 640 화면 기준). 문장이 길어 넘치면 자동으로 더 작아진다")]
        [SerializeField] int bodyFontSize = 17;

        bool reading;
        int page;
        // NonSerialized: 에디터가 스크립트를 다시 불러올 때 GUIStyle을 "빈 스타일"로 채워 넣어서,
        // null 검사로는 "이미 만들었다"고 착각해 정렬·글자 크기 설정이 통째로 빠지는 문제가 있었다.
        [System.NonSerialized] GUIStyle titleStyle, bodyStyle, hintStyle;
        static Texture2D paper;

        static Gim002Config Cfg { get { return Gim002Controller.Instance != null ? Gim002Controller.Instance.Config : null; } }
        int PageCount { get { return Cfg != null ? 1 + Cfg.scenes.Count : 1; } }

        protected override void OnReleased() { reading = false; }

        void Update()
        {
            if (!IsHeldLocally) { reading = false; return; }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[readKey].wasPressedThisFrame) reading = !reading;
            if (!reading) return;
            if (kb.leftArrowKey.wasPressedThisFrame) page = Mathf.Max(0, page - 1);
            if (kb.rightArrowKey.wasPressedThisFrame) page = Mathf.Min(PageCount - 1, page + 1);
            if (kb.escapeKey.wasPressedThisFrame) reading = false;
        }

        void OnGUI()
        {
            if (!IsHeldLocally) return;
            MakeStyles();
            // LGH UI와 같은 방식: 높이 640 기준 가상 화면에 그리고 GUI.matrix로 실제 해상도에 맞춰 확대한다.
            // → 창 크기와 글자 크기의 비율이 해상도와 상관없이 항상 같다.
            Vector2 v = LGH.UiScale.Apply();
            float w = v.x, h = v.y;

            if (!reading)
            {
                GUI.Label(new Rect(0, h - 40, w, 24), "[F] 놀이책 읽기", hintStyle);
                GUI.matrix = Matrix4x4.identity;
                return;
            }

            // 책 펼친 화면 (화면 가로 80%, 세로 78%)
            var rect = new Rect(w * 0.1f, h * 0.08f, w * 0.8f, h * 0.78f);
            GUI.color = new Color(1f, 0.97f, 0.88f, 1f);   // 불투명: 뒤 화면이 비쳐 글자를 가리지 않게
            GUI.DrawTexture(rect, paper);
            GUI.color = Color.white;

            string title, body;
            PageText(page, out title, out body);
            const float pad = 28f, titleH = 34f, hintH = 24f;
            GUI.Label(new Rect(rect.x, rect.y + 14f, rect.width, titleH), title, titleStyle);

            // 본문 영역: 제목 아래 ~ 쪽 번호 위. 문장이 길어도 이 안에 다 들어오도록 글씨 크기를 줄여 맞춘다.
            var bodyRect = new Rect(rect.x + pad, rect.y + 14f + titleH + 10f, rect.width - pad * 2f, rect.height - (14f + titleH + 10f) - (hintH + 18f));
            FitBody(body, bodyRect);
            GUI.Label(bodyRect, body, bodyStyle);

            GUI.Label(new Rect(rect.x, rect.yMax - hintH - 10f, rect.width, hintH),
                (page > 0 ? "◀ ←   " : "") + (page + 1) + " / " + PageCount + (page < PageCount - 1 ? "   → ▶" : "") + "     [F] 덮기", hintStyle);
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
            if (paper == null) { paper = new Texture2D(1, 1); paper.SetPixel(0, 0, Color.white); paper.Apply(); }
            if (titleStyle != null) return;
            // 글자 크기는 높이 640 가상 화면 기준 (실제 화면에서는 UiScale 배율만큼 커진다)
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = titleFontSize, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(0.3f, 0.18f, 0.1f);
            bodyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = bodyFontSize, wordWrap = true };
            bodyStyle.normal.textColor = new Color(0.2f, 0.15f, 0.1f);
            hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            hintStyle.normal.textColor = new Color(0.45f, 0.4f, 0.35f);
        }
    }
}
