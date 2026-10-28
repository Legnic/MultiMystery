using UnityEngine;

namespace Gim002
{
    // GIM-001 보상 열쇠 (1층 액자 뒤 음악실 열쇠 / 2층 장난감 상자 속 태엽 열쇠). 공용 들기 시스템으로 왼손에 든다.
    //  - GIM-001이 풀리기 전(LOCKED)에는 집을 수 없다 (액자가 아직 안 떨어졌고 상자도 닫혀 있으므로).
    //  - 사용처(Gim002KeyTarget)를 바라보고 E → 문양이 맞으면 사라진다. 사용 여부는 동기화 상태(musicDoorOpened / stageWound)를 보고
    //    모든 컴퓨터에서 똑같이 숨긴다 → 재접속해도 이미 쓴 열쇠는 다시 나타나지 않는다.
    //  - 상대 화면에서는 HoldableNetworkSync가 상대 캐릭터 왼손에 보여 준다.
    // 2026-10-28: 인벤토리(LGH WorldItem) 방식 → 다시 왼손에 드는 방식으로 (인벤토리 시스템 제거 예정).
    //  예전 방식으로 배치된 씬(Develop 등)에는 같은 오브젝트에 WorldItem이 남아 있을 수 있어서, 시작할 때 그 아이템 칸을 비워
    //  인벤토리 줍기로 잡히지 않게 한다 (컴포넌트를 지우면 LGH ItemNetworkSync의 아이템 번호가 어긋날 수 있어 남겨 둔다).
    public class Gim002KeyItem : HoldableItem
    {
        [Tooltip("이 열쇠의 정보(이름·문양)")]
        [SerializeField] Gim002KeyData keyData;
        [Tooltip("어디에 쓰는 열쇠인지 (사용 후 숨김 판단용)")]
        [SerializeField] Gim002KeyTarget.TargetKind usedFor;

        public Gim002KeyData KeyData { get { return keyData; } }
        public Gim002KeyTarget.TargetKind UsedFor { get { return usedFor; } }

        public override string DisplayName
        {
            get { return keyData != null ? keyData.displayName + " (" + keyData.emblem + " 문양)" : displayName; }
        }

        static Gim002Controller C { get { return Gim002Controller.Instance; } }

        protected override void Awake()
        {
            var legacy = GetComponent<LGH.WorldItem>();
            if (legacy != null) legacy.item = null;   // PlayerInteractor는 item이 비어 있는 WorldItem을 무시한다
            base.Awake();
        }

        protected override bool CanPickUp
        {
            get { return C != null && C.State.state != Gim002State.Locked && !Used; }
        }

        bool Used
        {
            get
            {
                if (C == null) return false;
                return usedFor == Gim002KeyTarget.TargetKind.MusicDoor ? C.State.musicDoorOpened : C.State.stageWound;
            }
        }

        void Update()
        {
            // 이미 사용된 열쇠면 (내가 썼든, 상대가 썼든, 재접속이든) 사라진다
            if (Used) Consume();
        }
    }
}
