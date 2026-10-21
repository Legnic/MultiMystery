using UnityEngine;

namespace Gim002
{
    // GIM-002의 모든 시간 수치(기획 8장 "수치 가안"). 전부 가안이라 코드에 박지 않고
    // Gim002Config(ScriptableObject)에 담아 Inspector에서 바꾼다.
    // 판정 로직(Gim002Logic)은 이 클래스만 받으므로 테스트에서는 new Gim002Timing()으로 기본값을 그대로 쓸 수 있다.
    [System.Serializable]
    public class Gim002Timing
    {
        [Tooltip("손잡이를 돌리려면 상호작용 키를 몇 초 누르고 있어야 하는지. 중간에 떼면 처음부터.")]
        public float crankHoldSeconds = 2f;

        [Tooltip("커튼이 열리거나 닫히는 데 걸리는 시간(초). 이 동안은 입력을 무시한다.")]
        public float curtainSeconds = 1f;

        [Tooltip("인형과 배경카드가 둘 다 꽂힌 뒤 판정까지 기다리는 시간(초). 꽂히는 소리가 먼저 나게 하려는 여유.")]
        public float judgeDelaySeconds = 0.3f;

        [Tooltip("무대 오답 때 2층 화면이 빨갛게 깜빡이는 시간(초).")]
        public float stageWrongFlashSeconds = 0.5f;

        [Tooltip("오답 후 인형·카드가 원래 자리로 돌아가는 시간(초).")]
        public float returnHomeSeconds = 0.5f;

        [Tooltip("반주 오답 연출 시간(초). 이 동안 건반 입력은 기록하지 않는다(소리는 남).")]
        public float pianoWrongSeconds = 1f;

        [Tooltip("인형 공연 시간(초). 이 동안 모든 상호작용을 무시한다.")]
        public float performSeconds = 3f;

        [Tooltip("공연 후 정리 시간(초): 커튼 닫힘, 인형·카드 원위치, 반주판 닫힘.")]
        public float cleanupSeconds = 1f;
    }
}
