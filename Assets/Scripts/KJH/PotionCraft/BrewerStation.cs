using System.Collections;
using UnityEngine;

namespace PotionCraft
{
    // 1층 제조 통. 재료 1칸 + 물 1칸이 모두 차면 자동으로 제조해서 통 안에 덩이 1개가 생긴다.
    // 꺼내기/비우기는 없고, 덩이를 집기 전까지는 새 재료·물을 받지 않는다.
    // 덩이는 결과 색과 상관없이 항상 같은 회색이다 (UI에도 결과 색을 보이지 않는다).
    public class BrewerStation : PotionStation
    {
        private const int Idle = 0, Brewing = 1, Done = 2;

        [Tooltip("제조에 걸리는 시간(초).")]
        [SerializeField] private float brewSeconds = 2f;
        [Header("모양 (비어 있어도 동작)")]
        [Tooltip("제조 중에 덜컹덜컹 흔들리는 혼합기 모델. 피벗이 바닥에 있어야 바닥 기준으로 흔들린다.")]
        [SerializeField] private Transform shakeVisual;
        [SerializeField] private float shakeAngle = 3f;
        [SerializeField] private float shakeBounce = 0.01f;
        [SerializeField] private Renderer liquid;
        [SerializeField] private GameObject lumpVisual;

        // 상태: 재료(-1=없음), 물(-1=없음), 단계, 결과 가루색
        private int mat = -1, wat = -1, phase = Idle, result;

        private Vector3 shakeBasePos, liquidBaseScale = Vector3.one;
        private Quaternion shakeBaseRot = Quaternion.identity;

        private void Awake()
        {
            if (shakeVisual != null) { shakeBasePos = shakeVisual.localPosition; shakeBaseRot = shakeVisual.localRotation; }
            if (liquid != null) liquidBaseScale = liquid.transform.localScale;
        }

        public override string InteractPrompt
        {
            get
            {
                if (phase == Brewing) return "제조 중…";
                int h = LocalHeld;
                if (phase == Done) return h == 0 ? "[E] 덩이 집기" : "덩이를 집으려면 손을 비워야 한다";
                var kind = Item.KindOf(h);
                if (kind == ItemKind.Material) return mat >= 0 ? "재료 칸이 이미 찼다" : "[E] " + NameOf(h) + " 넣기";
                if (kind == ItemKind.Water) return wat >= 0 ? "물 칸이 이미 찼다" : "[E] " + NameOf(h) + " 넣기";
                return "제조 통 (재료 1개 + 물 1개를 넣는다)";
            }
        }

        public override string ServerInteract(ref int held)
        {
            if (phase == Brewing) return Reject("제조 중이다");
            if (phase == Done)
            {
                if (held != 0) return Reject("덩이를 집으려면 손을 비워야 한다");
                held = Item.Make(ItemKind.Lump, result);
                mat = -1; wat = -1; phase = Idle; result = 0;
                return "덩이를 집었다";
            }

            ItemKind kind = Item.KindOf(held);
            string put = NameOf(held);
            if (kind == ItemKind.Material)
            {
                if (mat >= 0) return Reject("재료 칸이 이미 찼다");
                mat = Item.SubOf(held);
            }
            else if (kind == ItemKind.Water)
            {
                if (wat >= 0) return Reject("물 칸이 이미 찼다");
                wat = Item.SubOf(held);
            }
            else return Reject(held == 0 ? "재료나 물을 들고 있어야 한다" : "제조 통에 넣을 수 없는 것이다");

            held = 0;
            if (mat >= 0 && wat >= 0)
            {
                var table = Sync != null ? Sync.RecipeTable : null;
                result = table != null ? (int)table.Craft((MaterialId)mat, (WaterId)wat) : (int)PowderColor.Black;
                phase = Brewing;
                StartCoroutine(BrewRoutine());
                return put + "을(를) 넣었다 — 제조를 시작한다";
            }
            return put + "을(를) 넣었다";
        }

        private IEnumerator BrewRoutine()
        {
            yield return new WaitForSeconds(brewSeconds);
            phase = Done;
            Publish();
        }

        public override int[] GetState() { return new[] { mat, wat, phase, result }; }

        public override void ApplyState(int[] s)
        {
            if (s.Length < 4) return;
            mat = s[0]; wat = s[1]; phase = s[2]; result = s[3];
        }

        public override void Refresh()
        {
            if (lumpVisual != null) lumpVisual.SetActive(phase == Done);
            if (liquid != null) { liquid.gameObject.SetActive(phase != Done); if (phase != Brewing) liquid.transform.localScale = liquidBaseScale; }
            if (shakeVisual != null && phase != Brewing) { shakeVisual.localPosition = shakeBasePos; shakeVisual.localRotation = shakeBaseRot; }
        }

        private void Update()
        {
            if (phase != Brewing) return;
            float t = Time.time;
            // 제조 중에는 혼합기가 바닥을 기준으로 덜컹덜컹 흔들린다.
            if (shakeVisual != null)
            {
                shakeVisual.localRotation = shakeBaseRot * Quaternion.Euler(shakeAngle * Mathf.Sin(t * 23f), 0f, shakeAngle * Mathf.Sin(t * 17f + 1.3f));
                shakeVisual.localPosition = shakeBasePos + Vector3.up * (shakeBounce * Mathf.Abs(Mathf.Sin(t * 31f)));
            }
            // 통 속 액체가 있으면 부글부글 움직인다.
            if (liquid != null)
            {
                float k = 1f + 0.08f * Mathf.Sin(t * 18f);
                liquid.transform.localScale = new Vector3(liquidBaseScale.x * k, liquidBaseScale.y, liquidBaseScale.z * k);
            }
        }
    }
}
