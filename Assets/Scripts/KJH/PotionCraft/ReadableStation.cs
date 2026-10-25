using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PotionCraft
{
    // 제조법 책(2층)과 최종 레시피 쪽지(1층). 집을 수도 도르래로 보낼 수도 없는 월드 오브젝트다.
    // 상호작용하면 "읽는 상태"가 된다 (이동/시점 잠금 + 화면에 종이 표시). 책은 E/D/→ 다음 쪽, A/← 이전 쪽, Q/Esc로 닫는다.
    // 읽는 내용은 데이터(RecipeBookData / FinalRecipeData)에서 가져오고, 네트워크 동기화는 필요 없다 (각자 화면에서만 본다).
    public class ReadableStation : MonoBehaviour, IWorldInteractable
    {
        public enum Kind { Book, Note }

        [SerializeField] private Kind kind = Kind.Book;

        public static ReadableStation Current { get; private set; }

        private int spread;
        private LGH.FirstPersonController lockedMove;
        // GUIStyle은 직렬화 가능한 타입이라, 에디터 재컴파일 때 빈 기본값으로 채워지면 null 검사로 만든 스타일이 건너뛰어진다 → 직렬화 제외.
        [System.NonSerialized] private GUIStyle titleStyle, bodyStyle, hintStyle, imageHintStyle;

        public bool CanInteract { get { return PotionCraftSync.Instance != null; } }

        public string InteractPrompt
        {
            get
            {
                if (Current != this) return kind == Kind.Book ? "[E] 제조법 책 보기" : "[E] 쪽지 보기";
                return kind == Kind.Book ? "[E] 다음 쪽   [A] 이전 쪽   [Q] 닫기" : "[Q] 닫기";
            }
        }

        private int SpreadCount
        {
            get
            {
                var book = PotionCraftSync.Instance != null ? PotionCraftSync.Instance.BookData : null;
                int pages = book != null && book.pages != null ? book.pages.Length : 0;
                // 그림 책은 한 번에 한 쪽, 글 책은 펼침면(2쪽)씩 넘긴다.
                if (book != null && book.UsesImages) return pages;
                return Mathf.Max(1, (pages + 1) / 2);
            }
        }

        public void Interact(GameObject interactor)
        {
            if (Current == this)
            {
                // 읽는 중에 E를 다시 누르면 다음 쪽, 마지막 쪽이면 닫는다.
                if (kind == Kind.Book && spread < SpreadCount - 1) spread++;
                else Close();
                return;
            }
            if (Current != null) return;

            Current = this;
            spread = 0;
            lockedMove = interactor != null ? interactor.GetComponent<LGH.FirstPersonController>() : null;
            if (lockedMove != null) lockedMove.enabled = false;
        }

        private void Close()
        {
            if (lockedMove != null) lockedMove.enabled = true;
            lockedMove = null;
            if (Current == this) Current = null;
        }

        private void OnDisable() { if (Current == this) Close(); }

        private void Update()
        {
            if (Current != this) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.qKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (kind != Kind.Book) return;
            if ((kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) && spread < SpreadCount - 1) spread++;
            if ((kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) && spread > 0) spread--;
        }

        private void Styles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(0.25f, 0.15f, 0.08f);
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperLeft, wordWrap = true };
            bodyStyle.normal.textColor = new Color(0.2f, 0.12f, 0.06f);
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            hintStyle.normal.textColor = new Color(0.4f, 0.3f, 0.2f);
        }

        private void OnGUI()
        {
            if (Current != this) return;
            var sync = PotionCraftSync.Instance;
            if (sync == null) return;
            Styles();
            var v = LGH.UiScale.Apply();

            bool book = kind == Kind.Book;
            if (book && sync.BookData != null && sync.BookData.UsesImages)
            {
                int page = Mathf.Clamp(spread, 0, sync.BookData.pages.Length - 1);
                DrawImagePage(v, sync.BookData.pages[page].image,
                    "조합법 " + (page + 1) + "쪽   —   " + (page + 1) + " / " + SpreadCount + "   [E][D] 다음  [A] 이전  [Q] 닫기");
                return;
            }
            var image = book ? null : (sync.FinalRecipe != null ? sync.FinalRecipe.noteImage : null);
            if (image != null) { DrawImagePage(v, image, "[Q] 닫기"); return; }
            float pw = book ? 640f : 460f, ph = book ? 380f : 260f;
            var r = new Rect((v.x - pw) / 2f, (v.y - ph) / 2f - 20f, pw, ph);
            var old = GUI.color;
            GUI.color = new Color(0.93f, 0.86f, 0.70f, 0.97f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.45f, 0.30f, 0.15f, 1f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 4f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 4f, r.width, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (book)
            {
                var data = sync.BookData;
                GUI.Label(new Rect(r.x, r.y + 14f, r.width, 36f), data != null ? data.title : "제조법", titleStyle);
                // 펼침면: 왼쪽 쪽 = 2*spread+1, 오른쪽 쪽 = 2*spread+2
                for (int side = 0; side < 2; side++)
                {
                    int pageIndex = spread * 2 + side;
                    var col = new Rect(r.x + 28f + side * (r.width / 2f), r.y + 68f, r.width / 2f - 56f, r.height - 130f);
                    GUI.Label(new Rect(col.x, col.y, col.width, 24f), (pageIndex + 1) + "쪽", hintStyle);
                    var sb = new StringBuilder();
                    if (data != null && data.pages != null && pageIndex < data.pages.Length)
                    {
                        var p = data.pages[pageIndex];
                        sb.AppendLine("· " + p.entryA);
                        sb.AppendLine();
                        sb.AppendLine("· " + p.entryB);
                    }
                    GUI.Label(new Rect(col.x, col.y + 34f, col.width, col.height - 34f), sb.ToString(), bodyStyle);
                }
                GUI.Label(new Rect(r.x, r.yMax - 40f, r.width, 28f), (spread + 1) + " / " + SpreadCount + "   —   [E][D] 다음  [A] 이전  [Q] 닫기", hintStyle);
            }
            else
            {
                var data = sync.FinalRecipe;
                GUI.Label(new Rect(r.x, r.y + 14f, r.width, 36f), data != null ? data.title : "쪽지", titleStyle);
                GUI.Label(new Rect(r.x + 36f, r.y + 80f, r.width - 72f, r.height - 140f), data != null ? data.noteText : string.Empty, bodyStyle);
                GUI.Label(new Rect(r.x, r.yMax - 40f, r.width, 28f), "[Q] 닫기", hintStyle);
            }
            GUI.color = old;
        }

        // 그림 쪽지·그림 책: 화면을 어둡게 덮고 그림을 가운데에 크게 띄운 뒤, 아래에 안내 문구를 쓴다.
        private void DrawImagePage(Vector2 v, Texture2D image, string hint)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0f, 0f, v.x, v.y), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float w = Mathf.Min(v.x * 0.8f, 1100f);
            float h = w * image.height / image.width;
            if (h > v.y * 0.8f) { h = v.y * 0.8f; w = h * image.width / image.height; } // 세로로 넘치지 않게
            GUI.DrawTexture(new Rect((v.x - w) / 2f, (v.y - h) / 2f - 20f, w, h), image, ScaleMode.ScaleToFit);
            if (imageHintStyle == null)
            {
                imageHintStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                imageHintStyle.normal.textColor = new Color(0.85f, 0.8f, 0.7f);
            }
            GUI.Label(new Rect(0f, (v.y + h) / 2f - 14f, v.x, 28f), hint, imageHintStyle);
            GUI.color = old;
        }
    }
}
