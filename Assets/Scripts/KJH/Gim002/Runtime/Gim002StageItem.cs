using UnityEngine;

namespace Gim002
{
    // 무대 슬롯 종류: 인형 꽂이 / 배경카드 슬롯
    public enum StageSlotKind { Doll, Background }

    // 2층 인형(4개)과 배경카드(5장). 공용 들기 시스템(HoldableItem)을 상속해 집기·제자리 두기를 그대로 쓴다.
    //
    // 위치를 정하는 규칙 (매 프레임 확인):
    //  1) 이 컴퓨터 손에 들려 있으면 → 손이 위치를 정한다
    //  2) 동기화 상태에서 슬롯에 내가 꽂혀 있으면 → 그 슬롯으로
    //  3) 방금 내가 꽂았는데 아직 판정 담당의 답이 안 왔으면(pending) → 잠깐 슬롯에 둔 채 기다린다 (네트워크 지연 대비)
    //  4) 그 밖에는 → 원래 자리로 (오답 후 원위치, 공연 후 정리, 바꾸기로 밀려남 모두 여기서 처리)
    // 이렇게 하면 "누가 어디에 꽂혀 있나"는 판정 담당의 동기화 상태 하나만 믿으면 되고, 화면은 저절로 맞춰진다.
    public class Gim002StageItem : HoldableItem
    {
        [Header("GIM-002")]
        [Tooltip("인형이면 Doll, 배경카드면 Background")]
        [SerializeField] StageSlotKind kind;
        [Tooltip("kind가 Doll일 때의 인형 ID")]
        [SerializeField] DollId doll;
        [Tooltip("kind가 Background일 때의 배경 ID")]
        [SerializeField] BackgroundId background;
        [Tooltip("판정 담당의 답을 기다리는 최대 시간(초). 넘으면 원래 자리로 돌아간다.")]
        [SerializeField] float pendingTimeout = 1f;

        public StageSlotKind Kind { get { return kind; } }
        public DollId Doll { get { return doll; } }
        public BackgroundId Background { get { return background; } }
        public int Code { get { return kind == StageSlotKind.Doll ? (int)doll : (int)background; } }

        Gim002StageSlot pendingSlot;
        float pendingUntil;

        static Gim002Controller C { get { return Gim002Controller.Instance; } }

        public override string DisplayName
        {
            get
            {
                var c = C;
                if (c == null || c.Config == null) return displayName;
                return kind == StageSlotKind.Doll ? c.Config.DollName(doll) : c.Config.BackgroundName(background);
            }
        }

        void Start()
        {
            // 오답 후 원위치 시간도 Config 수치를 따른다
            if (C != null) returnSeconds = C.Timing.returnHomeSeconds;
        }

        // 슬롯이 호출: 내가 지금 이 슬롯에 꽂았다 (판정 담당에게 요청을 보내기 직전)
        public void PredictPlace(Gim002StageSlot slot)
        {
            pendingSlot = slot;
            pendingUntil = Time.time + pendingTimeout;
            PlaceAt(slot.Anchor, slot);
        }

        // 동기화 상태에서 이 물건이 슬롯에 꽂혀 있는가
        public bool InSyncedSlot()
        {
            var c = C;
            if (c == null) return false;
            int code = kind == StageSlotKind.Doll ? c.State.slotDoll : c.State.slotBg;
            return code == Code;
        }

        void Update()
        {
            if (C == null || IsHeldLocally) return;

            var slot = Gim002StageSlot.Find(kind);
            if (InSyncedSlot() && slot != null)
            {
                pendingSlot = null;
                if (!IsPlacedOn(slot.Anchor)) PlaceAt(slot.Anchor, slot);
                return;
            }

            if (pendingSlot != null && Time.time < pendingUntil) return;   // 답을 기다리는 중
            pendingSlot = null;

            if (Location == Where.Placed) ReturnHome();
        }

        protected override void OnTaken() { pendingSlot = null; }
    }
}
