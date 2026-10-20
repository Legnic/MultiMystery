using UnityEngine;

namespace PotionCraft
{
    // 혼합 통 옆 "비우기" 버튼. 1~2칸이 찬 상태에서 누르면 넣은 가루를 모두 없앤다 (돌려주지 않음).
    public class MixerClearButton : PotionStation
    {
        [SerializeField] private MixerStation mixer;

        public override bool CanInteract { get { return mixer != null && !mixer.IsLocked; } }

        public override string InteractPrompt { get { return "[E] 혼합 통 비우기"; } }

        public override string ServerInteract(ref int held)
        {
            if (mixer == null) return null;
            string msg = mixer.ServerClear();
            return msg;
        }
    }
}
