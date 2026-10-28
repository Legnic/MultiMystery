using UnityEngine;

namespace CoopDemo
{
    // 옵션 창 디자인(디자이너 이미지 1536x864 기준) 리소스 모음.
    // Resources/OptionsDesignTheme 에셋 하나에 연결해 두면 OptionsMenuController가 씬 연결 없이 불러 쓴다.
    // 필요한 칸이 하나라도 비어 있으면 예전 IMGUI 옵션 창을 그린다.
    [CreateAssetMenu(fileName = "OptionsDesignTheme", menuName = "KJH/Options Design Theme")]
    public class OptionsDesignTheme : ScriptableObject
    {
        [Header("배경 (1536x864)")]
        [Tooltip("오디오 탭: 글자·구분선·드롭다운 칸이 그려진 배경 (슬라이더 마름모는 지운 버전)")]
        public Texture2D audioBackground;
        [Tooltip("그래픽/조작 탭: 안쪽 칸을 비운 배경")]
        public Texture2D plainBackground;
        [Tooltip("그래픽 탭: 줄 이름·구분선·밝기 막대가 그려진 배경 (슬라이더 마름모는 지운 버전). 비우면 plainBackground에 직접 그린다")]
        public Texture2D graphicsBackground;
        [Tooltip("조작 탭: 줄 이름·구분선·슬라이더 막대·조작 키 안내가 그려진 배경 (슬라이더 마름모는 지운 버전). 비우면 plainBackground에 직접 그린다")]
        public Texture2D controlsBackground;

        [Header("탭 (오디오, 그래픽, 조작 순서)")]
        public Texture2D[] tabNormal = new Texture2D[3];
        public Texture2D[] tabSelected = new Texture2D[3];
        [Tooltip("마우스를 올렸을 때 보이는 하늘색 테두리 (9-slice, 로비와 같은 이미지)")]
        public Texture2D hoverOutline;

        [Header("컨트롤")]
        public Texture2D sliderThumb;    // 금색 마름모
        public Texture2D micOn;
        public Texture2D micMuted;       // 빨간 사선이 그어진 마이크
        public Texture2D toggleTrack;    // 켜짐/꺼짐 토글 바탕
        public Texture2D toggleKnob;     // 토글 동그라미
        public Texture2D rowLine;        // 줄 사이 구분선 색

        [Header("인게임 메뉴 (ESC)")]
        [Tooltip("ESC 메뉴 패널 (780x775, 바깥은 투명). 디자인 이미지 1536x864에서 (379, 44) 자리")]
        public Texture2D menuPanel;

        [Header("대기방 (방 만들기/참가 직후)")]
        [Tooltip("대기방 화면 (1536x864). '준비 완료' 버튼 글자는 지운 버전 — 상태에 따라 코드가 글자를 그린다")]
        public Texture2D waitingRoomBackground;

        [Header("폰트")]
        [Tooltip("숫자, 마이크 상태 글자 (SongMyung)")]
        public Font numberFont;
        [Tooltip("드롭다운 장치 이름, 다른 탭 본문 (LevelUp)")]
        public Font bodyFont;

        // Resources/OptionsDesignTheme (없으면 null). 다른 화면(ESC 메뉴 등)도 같은 리소스를 쓴다.
        static OptionsDesignTheme cached;
        public static OptionsDesignTheme Load()
        {
            if (cached == null) cached = Resources.Load<OptionsDesignTheme>("OptionsDesignTheme");
            return cached;
        }

        public bool IsComplete =>
            audioBackground != null && plainBackground != null && hoverOutline != null && sliderThumb != null &&
            micOn != null && micMuted != null && toggleTrack != null && toggleKnob != null &&
            numberFont != null && bodyFont != null &&
            tabNormal != null && tabNormal.Length >= 3 && tabSelected != null && tabSelected.Length >= 3 &&
            tabNormal[0] != null && tabNormal[1] != null && tabNormal[2] != null &&
            tabSelected[0] != null && tabSelected[1] != null && tabSelected[2] != null;
    }
}
