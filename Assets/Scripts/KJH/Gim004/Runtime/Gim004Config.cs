using System;
using UnityEngine;

namespace Gim004
{
    // 완료 시 열리는 문 하나. LGH 맵 프리팹을 고치지 않도록 "이름"으로 찾는다 (KJH/Develop 둘 다 같은 이름·좌표).
    [Serializable]
    public class Gim004DoorTarget
    {
        [Tooltip("LGH 문(InteractableDoor) 오브젝트 이름. 양문이면 한쪽만 적어도 짝(partner)까지 같이 잠기고 열린다")]
        public string doorName;
        [Tooltip("문을 열 때 '이 위치에서 연 것처럼' 문짝이 반대쪽으로 열린다 (월드 좌표, 보통 복도 쪽)")]
        public Vector3 openFrom;
        [Tooltip("잠겨 있을 때 문을 열려고 하면 뜨는 문구")]
        public string lockedMessage = "잠겨 있다";
    }

    // GIM-004 전체 설정 하나. 판정 규칙 + 기록일지 내용 + 문 + 화면 문구.
    // 씬의 Gim004Controller와 기록일지(Gim004Journal)가 이 에셋을 참조한다.
    [CreateAssetMenu(menuName = "GIM-004/Config", fileName = "Gim004Config")]
    public class Gim004Config : ScriptableObject
    {
        [Tooltip("정답(선반 정면 기준 왼쪽부터 D-E-A-F)과 판정 대기 시간")]
        public Gim004Rules rules = new Gim004Rules();

        [Header("온실 기록일지 (2층 서고)")]
        public string journalTitle = "온실 기록일지";
        public string journalLeftHeading = "중앙 선반에 놓아야 할 식물 리스트";
        public string journalRightHeading = "배치 기록";
        [Tooltip("사진 1~4번에 찍힌 화분 (기본: 1=F, 2=E, 3=D, 4=A). 사진 촬영 도구가 이 순서로 찍는다")]
        public Gim004PotId[] photoPots = { Gim004PotId.F, Gim004PotId.E, Gim004PotId.D, Gim004PotId.A };
        [Tooltip("사진 1~4번 (Tools/KJH/GIM-004/Capture Journal Photos가 실제 화분 모델을 찍어서 채운다)")]
        public Texture2D[] photos = new Texture2D[4];
        [Tooltip("배치 기록 (게임 속 문구 그대로, 한 줄에 한 문장)")]
        [TextArea(4, 10)]
        public string placementRecord =
            "선반 정면에서 바라본 방향을 기준으로 한다.\n" +
            "1번은 맨 오른쪽에 놓았다.\n" +
            "3번은 맨 왼쪽에 놓았다.\n" +
            "2번은 1번보다 두 칸 왼쪽에 놓았다.\n" +
            "4번은 2번과 1번 사이에 놓았다.";

        [Header("완료 시 열리는 문")]
        public Gim004DoorTarget[] completeDoors =
        {
            new Gim004DoorTarget { doorName = "Door_Courtyard_L", openFrom = new Vector3(44f, 0f, 1.5f), lockedMessage = "정원문이 굳게 잠겨 있다" },
            new Gim004DoorTarget { doorName = "Door_MaidRoom", openFrom = new Vector3(34.5f, 3.9f, -12.25f), lockedMessage = "정원 관리실 문이 잠겨 있다" },
        };
        [Tooltip("문 이름이 여러 개일 때, 부모 경로에 이 글자가 포함된 것만 고른다 (비우면 이름만으로)")]
        public string doorParentFilter = "Ravenwood_LGH";

        [Header("선행 기믹 GIM-003 연결 (기본 꺼짐)")]
        [Tooltip("켜면 약품 제조 퍼즐(PotionCraftSync)이 끝나기 전까지 아래 문(온실 문·서고 문)을 잠근다")]
        public bool lockEntryUntilPotionCraft = false;
        public Gim004DoorTarget[] entryDoors =
        {
            new Gim004DoorTarget { doorName = "Door_Conservatory", lockedMessage = "온실 문이 잠겨 있다" },
            new Gim004DoorTarget { doorName = "Door_Library", lockedMessage = "서고 문이 잠겨 있다" },
            new Gim004DoorTarget { doorName = "Door_Library_Guest1", lockedMessage = "서고 문이 잠겨 있다" },
        };

        [Header("층 구분 (안내 문구·실패음을 1층/2층에 나눠 보낼 때)")]
        [Tooltip("내 플레이어 발 높이가 이 값보다 낮으면 1층으로 본다 (2층 바닥 y=3.9)")]
        public float floorSplitY = 2f;

        [Header("화면 문구")]
        public string potName = "화분";
        public string msgCompleteFloor1 = "정원문이 열렸습니다.";
        public string msgCompleteFloor2 = "정원 관리실 문이 열렸습니다.";
        public string msgShelfLocked = "화분이 선반에 단단히 고정되어 있다";
        public string msgJudging = "...";
    }
}
