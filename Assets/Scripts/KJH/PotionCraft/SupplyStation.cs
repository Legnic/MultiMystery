using UnityEngine;

namespace PotionCraft
{
    // 1층 재료/물 보관대. 빈손으로 상호작용하면 해당 재료나 물이 1개 손에 생긴다 (무한).
    // Supply_Material ×4, Supply_Water ×3 프리팹이 이 컴포넌트의 값만 다른 변형이다.
    public class SupplyStation : PotionStation
    {
        [SerializeField] private ItemKind kind = ItemKind.Material;
        [SerializeField] private MaterialId material;
        [SerializeField] private WaterId water;

        public int ItemCode { get { return Item.Make(kind, kind == ItemKind.Water ? (int)water : (int)material); } }

        public override string InteractPrompt
        {
            get
            {
                string n = NameOf(ItemCode);
                return LocalHeld == 0 ? "[E] " + n + " 집기" : n + " (손이 비어 있어야 한다)";
            }
        }

        public override string ServerInteract(ref int held)
        {
            if (held != 0) return Reject("이미 손에 들고 있는 것이 있다");
            held = ItemCode;
            return NameOf(held) + "을(를) 집었다";
        }
    }
}
