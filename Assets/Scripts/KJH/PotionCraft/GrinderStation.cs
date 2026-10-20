using UnityEngine;

namespace PotionCraft
{
    // 2층 빻는 도구. 덩이를 들고 상호작용해서 넣고, E를 연타(기본 10회)하면 그 덩이의 색 가루가 종이 위에 생긴다.
    // 진행 횟수는 도구에 남고, 가루를 집기 전에는 새 덩이를 받지 않는다.
    public class GrinderStation : PotionStation
    {
        private const int Empty = 0, Loaded = 1, PowderReady = 2;

        [Tooltip("가루가 되기까지 필요한 E 입력 횟수.")]
        [SerializeField] private int hitsRequired = 10;
        [Header("모양 (비어 있어도 동작)")]
        [SerializeField] private Transform pestle;
        [SerializeField] private float pestleDrop = 0.12f;
        [SerializeField] private GameObject lumpVisual;
        [SerializeField] private GameObject powderVisual;
        [SerializeField] private Renderer progressBar;
        [SerializeField] private float progressBarFullWidth = 0.4f;

        // 상태: 단계, 덩이(가루) 색, 진행 횟수
        private int phase = Empty, color, progress;
        private int shownProgress;
        private float hitTimer = 1f;
        private Vector3 pestleHome;
        private bool pestleCaptured;

        public override string InteractPrompt
        {
            get
            {
                int h = LocalHeld;
                switch (phase)
                {
                    case Loaded: return "[E] 빻기 (" + progress + " / " + hitsRequired + ")";
                    case PowderReady:
                        var cat = Catalog;
                        string n = cat != null ? cat.PowderNameOf((PowderColor)color) : "가루";
                        return h == 0 ? "[E] " + n + " 집기" : "가루를 집으려면 손을 비워야 한다";
                    default:
                        return Item.KindOf(h) == ItemKind.Lump ? "[E] 덩이 넣기" : "빻는 도구 (덩이를 들고 있어야 한다)";
                }
            }
        }

        public override string ServerInteract(ref int held)
        {
            switch (phase)
            {
                case Loaded:
                    progress++;
                    if (progress >= hitsRequired) { progress = hitsRequired; phase = PowderReady; return "가루가 완성되었다"; }
                    return null;
                case PowderReady:
                    if (held != 0) return Reject("가루를 집으려면 손을 비워야 한다");
                    held = Item.Make(ItemKind.Powder, color);
                    phase = Empty; color = 0; progress = 0;
                    return NameOf(held) + "를 집었다";
                default:
                    if (Item.KindOf(held) != ItemKind.Lump)
                        return Reject(held == 0 ? "덩이를 들고 있어야 한다" : "덩이만 빻을 수 있다");
                    color = Item.SubOf(held);
                    held = 0; phase = Loaded; progress = 0;
                    return "덩이를 넣었다 — E를 연타해서 빻자";
            }
        }

        public override int[] GetState() { return new[] { phase, color, progress }; }

        public override void ApplyState(int[] s)
        {
            if (s.Length < 3) return;
            phase = s[0]; color = s[1]; progress = s[2];
        }

        public override void Refresh()
        {
            if (lumpVisual != null) lumpVisual.SetActive(phase == Loaded);
            if (powderVisual != null)
            {
                powderVisual.SetActive(phase == PowderReady);
                var cat = Catalog;
                if (phase == PowderReady && cat != null) PotionVisuals.TintMarked(powderVisual, cat.PowderColorOf((PowderColor)color));
            }
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(phase == Loaded);
                float f = Mathf.Clamp01(progress / (float)Mathf.Max(1, hitsRequired));
                var t = progressBar.transform;
                t.localScale = new Vector3(Mathf.Max(0.001f, progressBarFullWidth * f), t.localScale.y, t.localScale.z);
            }
            // 진행 횟수가 늘어난 순간 공이가 내려찍는다.
            if (progress > shownProgress) hitTimer = 0f;
            shownProgress = progress;
        }

        private void Update()
        {
            if (pestle == null) return;
            if (!pestleCaptured) { pestleHome = pestle.localPosition; pestleCaptured = true; }
            if (hitTimer < 1f)
            {
                hitTimer += Time.deltaTime / 0.18f;
                float k = Mathf.Sin(Mathf.Clamp01(hitTimer) * Mathf.PI);
                pestle.localPosition = pestleHome + Vector3.down * pestleDrop * k;
            }
            else pestle.localPosition = pestleHome;
        }
    }
}
