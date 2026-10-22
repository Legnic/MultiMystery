using UnityEngine;

namespace Gim005
{
    // GIM-005 전체 설정 하나. 수치(제한 시간·벌칙·잠금…) + 문양 목록 + 화면 문구 + 완료 시 열리는 대상.
    // 씬의 Gim005Controller가 이 에셋 하나만 참조하면 된다. 값은 전부 가안 🟡이며 플레이테스트 후 Inspector에서 바꾼다.
    [CreateAssetMenu(menuName = "GIM-005/Config", fileName = "Gim005Config")]
    public class Gim005Config : ScriptableObject
    {
        [Tooltip("판정 수치 (제한 시간 180초, 오답 15초 차감, 패널 잠금 3초, 흐려짐 2초, 종소리 30초 전…)")]
        public Gim005Rules rules = new Gim005Rules();

        [Tooltip("문양 13종 (ID, 임시 숫자, 이미지)")]
        public Gim005SymbolData symbols;

        [Header("완료 시 사라지는 대상 (1/2층을 막는 철창)")]
        [Tooltip("기믹 완료 시 모든 컴퓨터에서 비활성화할 오브젝트 이름. 씬마다(KJH/Develop) 참조를 다시 연결하지 않아도 되도록 이름으로 찾는다.")]
        public string[] hideOnCompleteNames = { "Rail_StairFront" };
        [Tooltip("위 이름의 오브젝트가 여러 개일 때, 부모 경로에 이 글자가 포함된 것만 고른다 (비우면 이름만으로)")]
        public string hideOnCompleteParentFilter = "Ravenwood_LGH";

        [Header("화면 문구")]
        public string msgStarted = "등 뒤에서 덤불이 바스락거린다. 서둘러야 한다";
        public string msgPanelIdle = "버튼이 아직 반응하지 않는다";
        public string msgPanelLocked = "패널이 잠겨 있다";
        public string msgWrong = "틀린 문양이다! 시간이 줄어들었다";
        public string msgAlreadyOpen = "이미 눌린 버튼이다";
        public string msgTimeOut = "정신이 아득해진다…";
        public string msgComplete = "덤불이 모두 걷히고, 어딘가에서 철창이 열리는 소리가 들린다";
    }
}
