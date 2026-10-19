using UnityEngine;

namespace PotionCraft
{
    // 쓰레기통(1층·2층). 손에 든 아이템을 버려서 손을 비운다. 재료·물·덩이·가루 모두 버릴 수 있다.
    public class TrashStation : PotionStation
    {
        public override string InteractPrompt
        {
            get
            {
                int h = LocalHeld;
                return h == 0 ? "쓰레기통 (버릴 것이 없다)" : "[E] " + NameOf(h) + " 버리기";
            }
        }

        public override string ServerInteract(ref int held)
        {
            if (held == 0) return null;
            string n = NameOf(held);
            held = 0;
            return n + "을(를) 버렸다";
        }
    }
}
