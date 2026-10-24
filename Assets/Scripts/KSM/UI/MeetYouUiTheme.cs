using UnityEngine;

namespace CoopDemo
{
    // 코드(IMGUI)로 그리는 화면(옵션 창, 방 만들기/대기방, 일시정지)이 쓰는 UI 이미지 모음.
    // Resources/MeetYouUiTheme 에셋 하나에 연결해 두고 MeetYouGuiSkin이 이 이미지로 GUI 스킨을 만든다.
    // 이미지가 비어 있으면 그 부분은 기본 IMGUI 모양으로 남는다.
    [CreateAssetMenu(fileName = "MeetYouUiTheme", menuName = "KSM/Meet You UI Theme")]
    public class MeetYouUiTheme : ScriptableObject
    {
        [Header("패널")]
        public Texture2D windowPanel;      // 큰 창 배경
        public Texture2D innerPanel;       // 창 안쪽 영역
        public Texture2D fieldPanel;       // 글자 입력칸

        [Header("버튼 (가로로 긴 것)")]
        public Texture2D buttonNormal;     // 기본(남색)
        public Texture2D buttonHover;      // 마우스 올림(청록 테두리)
        public Texture2D buttonPressed;    // 누름(금색)
        public Texture2D buttonSelected;   // 선택된 탭 등(금색)

        [Header("인벤토리 슬롯")]
        public Texture2D slotNormal;
        public Texture2D slotHover;
        public Texture2D slotSelected;

        [Header("음성 아이콘")]
        public Sprite micOn;
        public Sprite micMuted;

        [Header("슬라이더 / 체크박스")]
        public Texture2D sliderBar;
        public Texture2D sliderThumb;      // 금색 마름모
        public Texture2D toggleBox;        // 빈 칸
        public Texture2D checkIcon;        // 체크 표시
    }
}
