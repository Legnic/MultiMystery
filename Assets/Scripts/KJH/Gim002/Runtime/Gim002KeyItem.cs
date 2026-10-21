using UnityEngine;

namespace Gim002
{
    // GIM-001 보상 열쇠 (1층 액자 뒤 음악실 열쇠 / 2층 장난감 상자 속 태엽 열쇠). 공용 들기 시스템으로 집는다.
    //  - GIM-001이 풀리기 전(LOCKED)에는 집을 수 없다 (액자가 아직 안 떨어졌고 상자도 닫혀 있으므로).
    //  - 사용처(Gim002KeyTarget)에 쓰면 사라진다. 사용 여부는 동기화 상태(musicDoorOpened / stageWound)를 보고
    //    모든 컴퓨터에서 똑같이 숨긴다 → 재접속해도 이미 쓴 열쇠는 다시 나타나지 않는다.
    public class Gim002KeyItem : HoldableItem
    {
        [Tooltip("이 열쇠의 정보(이름·문양)")]
        [SerializeField] Gim002KeyData keyData;
        [Tooltip("어디에 쓰는 열쇠인지 (사용 후 숨김 판단용)")]
        [SerializeField] Gim002KeyTarget.TargetKind usedFor;

        public Gim002KeyData KeyData { get { return keyData; } }

        public override string DisplayName
        {
            get { return keyData != null ? keyData.displayName + " (" + keyData.emblem + " 문양)" : displayName; }
        }

        static Gim002Controller C { get { return Gim002Controller.Instance; } }

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
