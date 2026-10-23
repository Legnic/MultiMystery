using System;
using UnityEngine;

namespace Gim007
{
    // 발판에 가구가 올라가면 열리는 문 하나. LGH 맵 프리팹을 고치지 않도록 "이름"으로 찾는다 (KJH/Develop 둘 다 같은 이름·좌표).
    [Serializable]
    public class Gim007DoorTarget
    {
        [Tooltip("LGH 문(InteractableDoor) 오브젝트 이름. 양문이면 한쪽만 적어도 짝(partner)까지 같이 잠기고 열린다")]
        public string doorName;
        [Tooltip("문을 열 때 '이 위치에서 연 것처럼' 문짝이 반대쪽으로 열린다 (월드 좌표, 보통 안방 쪽)")]
        public Vector3 openFrom;
    }

    // GIM-007 전체 설정 하나. 판정 수치 + 문 + 화면 문구 + 연출 세기.
    // 씬의 Gim007Controller가 이 에셋을 참조한다. Build All은 이 에셋을 "없을 때만" 만들므로 여기서 바꾼 값은 보존된다.
    [CreateAssetMenu(menuName = "GIM-007/Config", fileName = "Gim007Config")]
    public class Gim007Config : ScriptableObject
    {
        [Tooltip("필요 성공 횟수·제한 시간·숨고르기 등 (명세서 제안값이 기본)")]
        public Gim007Rules rules = new Gim007Rules();

        [Header("비밀문 (2026-10-23 결정: 안방 → 드레스룸 양문)")]
        public Gim007DoorTarget[] secretDoors =
        {
            new Gim007DoorTarget { doorName = "Door_Master_Dressing", openFrom = new Vector3(34.5f, 3.9f, 12.95f) },
        };
        [Tooltip("문 이름이 여러 개일 때, 부모 경로에 이 글자가 포함된 것만 고른다 (비우면 이름만으로)")]
        public string doorParentFilter = "Ravenwood_LGH";
        [Tooltip("완료 전 문을 열려고 할 때 뜨는 문구")]
        public string msgDoorLocked = "문이 꿈쩍도 하지 않는다. 옆 바닥의 발판과 이어진 장치가 있는 것 같다";
        [Tooltip("완료 후 문을 '열린 채로' 고정한다 (닫을 수 없음, 명세서: 열린 상태로 유지)")]
        public bool keepDoorOpenAfterComplete = true;
        [Tooltip("완료 후 열린 문을 닫으려 할 때 뜨는 문구")]
        public string msgDoorHeldOpen = "발판에 눌린 장치가 문을 붙잡고 있다";

        [Header("힌트")]
        [Tooltip("혼자 잡고 힘을 준 횟수가 이 값 이상이면 힌트 문구로 바뀐다 (명세서 제안 3회)")]
        public int soloHintAfter = 3;

        [Header("화면 문구")]
        public string furnitureName = "서랍장";
        public string msgGripPrompt = "[E] {0} 붙잡기";
        public string msgReleasePrompt = "[E] 손 떼기";
        public string msgHandleTaken = "그쪽은 이미 누가 잡고 있다. 반대쪽을 잡자";
        public string msgSoloStuck = "꿈쩍도 하지 않는다";
        public string msgSoloHint = "꿈쩍도 하지 않는다… 누군가와 함께라면…";
        public string msgWaitingPartner = "혼자서는 움직이지 않는다\n같이 밀 사람을 기다리는 중";
        public string msgReady = "같이 밀 준비…";
        public string msgResting = "숨을 고르는 중…";
        public string msgPartnerLeft = "상대가 손을 놓았다";
        public string msgPlateRattle = "발판이 덜컥거리다 만다… 무게가 부족하다";
        public string msgComplete = "철컥— 발판이 내려앉고 드레스룸 문이 열렸다";
        public string msgLocked = "서랍장이 발판 위에 단단히 자리 잡았다";

        [Header("연출")]
        [Tooltip("성공 시 미는 사람 화면 흔들림 세기(m)")]
        public float successShake = 0.025f;
        [Tooltip("혼자 힘을 줄 때 화면 흔들림 세기(m)")]
        public float strainShake = 0.012f;
        [Tooltip("숨고르기 중 집 안 어딘가에서 나무가 삐걱이는 소리가 날 확률 (0~1, 한 번의 숨고르기당)")]
        [Range(0f, 1f)] public float restCreakChance = 0.5f;
        [Tooltip("사람이 발판에 올라갔을 때 덜컥거림 반복 간격(초)")]
        public float plateRattleCooldown = 2.5f;
    }
}
