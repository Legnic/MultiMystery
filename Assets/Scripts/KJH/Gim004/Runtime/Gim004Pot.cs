using UnityEngine;

namespace Gim004
{
    // 1층 온실 화분 7개 (A~G). 공용 들기 시스템(HoldableItem)을 상속해 집기·제자리 두기를 그대로 쓴다.
    // 화분 ID는 데이터용이며 화면에는 절대 표시하지 않는다 (안내 문구는 모두 "화분").
    //
    // 위치를 정하는 규칙 (매 프레임 확인, GIM-002 인형과 같은 방식):
    //  1) 방금 내가 놓거나 뺐는데 아직 판정 담당의 답이 안 왔으면(pending) → 잠깐 기다린다 (네트워크 지연 대비)
    //  2) 동기화 상태에서 선반 자리에 내가 있으면 → 그 자리로 (다른 컴퓨터가 놓은 경우 포함)
    //  3) 이 컴퓨터 손에 들려 있으면 → 손이 위치를 정한다
    //  4) 그 밖에는 → 처음 자리로 (오답 후 원위치, 교체로 밀려남 모두 여기서 처리)
    //
    // 모델 교체: 이 오브젝트의 자식 "Visual"만 바꾸면 된다 (Gim004_PotVisual_X 프리팹). 콜라이더는 루트에 있다.
    public class Gim004Pot : HoldableItem
    {
        [Header("GIM-004")]
        [Tooltip("개발용 화분 ID (A~G). 게임 화면에 표시하지 않는다")]
        [SerializeField] Gim004PotId potId;
        [Tooltip("판정 담당의 답을 기다리는 최대 시간(초). 넘으면 동기화 상태를 따른다")]
        [SerializeField] float pendingTimeout = 1f;

        public Gim004PotId PotId { get { return potId; } }
        public int Id { get { return (int)potId; } }

        float pendingUntil;

        static Gim004Controller C { get { return Gim004Controller.Instance; } }

        public override string DisplayName
        {
            get { return C != null && C.Config != null ? C.Config.potName : displayName; }
        }

        // 선반 자리가 호출: 내가 지금 이 자리에 놓았다 (판정 담당에게 요청을 보내기 직전)
        public void PredictPlace(Gim004ShelfSlot slot)
        {
            pendingUntil = Time.time + pendingTimeout;
            PlaceAt(slot.Anchor, slot);
        }

        // 선반 자리가 호출: 내가 빼거나, 교체로 밀려나 처음 자리로 보냈다
        public void PredictLeave()
        {
            pendingUntil = Time.time + pendingTimeout;
        }

        void Update()
        {
            var c = C;
            if (c == null) return;
            if (Time.time < pendingUntil) return;   // 답을 기다리는 중

            int slotIndex = c.State.SlotOf(Id);
            if (slotIndex >= 0)
            {
                var slot = Gim004ShelfSlot.Find(slotIndex);
                if (slot != null && !IsPlacedOn(slot.Anchor)) PlaceAt(slot.Anchor, slot);   // 손에 들려 있었다면 PlaceAt이 손을 비운다
                return;
            }

            if (IsHeldLocally) return;
            if (Location == Where.Placed) ReturnHome();
        }
    }
}
