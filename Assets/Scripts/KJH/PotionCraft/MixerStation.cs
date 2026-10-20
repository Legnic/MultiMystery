using UnityEngine;

namespace PotionCraft
{
    // 2층 최종 혼합 통. 가루를 순서대로 3칸 쌓고, 3번째가 들어가는 순간 판정한다.
    // 정답(FinalRecipeData.sequence)이면 황금빛 연출 + 기믹 완료(한 번), 이후 통은 잠긴다.
    // 오답이면 펑 터지고 통이 자동으로 비워진다 (아이템은 생기지 않는다).
    public class MixerStation : PotionStation
    {
        public const int SlotCount = 3;

        [Header("모양 (비어 있어도 동작)")]
        [Tooltip("아래 칸부터 차례로 쌓이는 층 (3개).")]
        [SerializeField] private Renderer[] layers;
        [SerializeField] private Transform effectPoint;

        // 상태: 쌓인 개수, 칸 3개의 가루색, 잠김(완료)
        private int count;
        private readonly int[] slots = new int[SlotCount];
        private bool locked;

        public bool IsLocked { get { return locked; } }
        public int Count { get { return count; } }

        public override bool CanInteract { get { return !locked; } }

        public override string InteractPrompt
        {
            get
            {
                if (Item.KindOf(LocalHeld) == ItemKind.Powder) return "[E] " + NameOf(LocalHeld) + " 넣기 (" + count + " / " + SlotCount + ")";
                return "혼합 통 (가루를 순서대로 " + SlotCount + "개 넣는다)";
            }
        }

        public override string ServerInteract(ref int held)
        {
            if (locked) return null;
            if (Item.KindOf(held) != ItemKind.Powder) return Reject(held == 0 ? "가루를 들고 있어야 한다" : "가루만 넣을 수 있다");

            slots[count++] = Item.SubOf(held);
            string put = NameOf(held);
            held = 0;
            if (count < SlotCount) return put + "를 넣었다";

            var sync = Sync;
            var recipe = sync != null ? sync.FinalRecipe : null;
            if (recipe != null && recipe.Matches(slots, count))
            {
                locked = true;
                Publish();
                sync.Fx(this, PotionFx.Success);
                sync.ServerComplete();
                return null;
            }

            // 오답: 펑 터지고 자동으로 비워진다.
            ClearSlots();
            if (sync != null) sync.Fx(this, PotionFx.Explode);
            return "펑! 잘못된 조합이다";
        }

        // 비우기 버튼이 호스트에서 호출한다 (넣은 가루는 돌려주지 않는다).
        public string ServerClear()
        {
            if (locked) return null;
            if (count == 0) return "비울 가루가 없다";
            ClearSlots();
            Publish();
            return "혼합 통을 비웠다";
        }

        private void ClearSlots()
        {
            count = 0;
            for (int i = 0; i < SlotCount; i++) slots[i] = 0;
        }

        public override int[] GetState() { return new[] { count, slots[0], slots[1], slots[2], locked ? 1 : 0 }; }

        public override void ApplyState(int[] s)
        {
            if (s.Length < 5) return;
            count = s[0];
            for (int i = 0; i < SlotCount; i++) slots[i] = s[1 + i];
            locked = s[4] != 0;
        }

        public override void Refresh()
        {
            var cat = Catalog;
            if (layers == null) return;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                bool on = i < count;
                layers[i].gameObject.SetActive(on);
                if (on && cat != null) PotionVisuals.Tint(layers[i], cat.PowderColorOf((PowderColor)slots[i]));
            }
        }

        public override void PlayFx(PotionFx fx)
        {
            var cat = Catalog;
            Vector3 p = effectPoint != null ? effectPoint.position : transform.position + Vector3.up * 0.3f;
            Material mat = cat != null ? cat.fxMaterial : null;
            switch (fx)
            {
                case PotionFx.Explode:
                    PotionVisuals.Burst(p, new Color(0.25f, 0.2f, 0.2f), 60, 3f, 0.12f, mat, 0.6f);
                    PotionVisuals.Burst(p, new Color(1f, 0.55f, 0.15f), 25, 4f, 0.08f, mat, 0.2f);
                    PotionVisuals.Flash(p, new Color(1f, 0.6f, 0.2f), 6f, 0.4f);
                    if (cat != null && cat.explodeClip != null) AudioSource.PlayClipAtPoint(cat.explodeClip, p);
                    break;
                case PotionFx.Success:
                    PotionVisuals.Burst(p, new Color(1f, 0.85f, 0.25f), 90, 1.8f, 0.07f, mat, -0.05f);
                    PotionVisuals.Flash(p, new Color(1f, 0.85f, 0.3f), 4f, 2f);
                    if (cat != null && cat.successClip != null) AudioSource.PlayClipAtPoint(cat.successClip, p);
                    break;
                default:
                    base.PlayFx(fx);
                    break;
            }
        }
    }
}
