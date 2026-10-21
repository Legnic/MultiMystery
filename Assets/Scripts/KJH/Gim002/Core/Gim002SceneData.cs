using UnityEngine;

namespace Gim002
{
    // 인형극 장면 하나의 데이터 (기획 "장면 데이터" 표의 한 줄).
    // 장면 로직은 하나만 만들고 이 데이터만 바꿔 3번 쓴다. 정답·기호·문장이 바뀌면 코드를 고치지 말고 이 에셋만 고친다.
    // 만드는 법: Project 창 우클릭 > Create > GIM-002 > Scene Data (또는 Tools/KJH/GIM-002/Build All이 자동 생성)
    [CreateAssetMenu(menuName = "GIM-002/Scene Data", fileName = "Gim002Scene")]
    public class Gim002SceneData : ScriptableObject
    {
        [Header("정답")]
        [Tooltip("이 장면의 정답 인형")]
        public DollId doll;

        [Tooltip("이 장면의 정답 배경카드")]
        public BackgroundId background;

        [Tooltip("반주판에 표시되고 피아노로 쳐야 하는 기호 순서. 개수(3 또는 4)가 곧 '장면 음 수'다.")]
        public SymbolId[] symbols = new SymbolId[0];

        [Header("연출")]
        [Tooltip("공연 때 인형 Animator에 보낼 트리거 이름 (예: ANIM_OWL_LOOK). Animator가 없으면 무시된다.")]
        public string performTrigger = "";

        [Header("놀이책 (1층)")]
        [Tooltip("놀이책 이 장면 쪽에 적힌 이야기 문장")]
        [TextArea(2, 4)] public string sentence = "";
    }
}
