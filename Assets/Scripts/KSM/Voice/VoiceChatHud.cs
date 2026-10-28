using UnityEngine;
using UnityEngine.UI;

namespace CoopDemo
{
    // 화면 왼쪽 아래 마이크 표시 (플레이 중에만).
    // 2026-10-28: 예전의 큰 음성 상태 패널(상태 글자·마이크 글자·감도 슬라이더)은 숨기고, 마이크 아이콘 하나만 남겼다.
    //  - 내 마이크에 소리가 들어오면(말하는 중) 초록 테두리
    //  - 조용하면 그냥 마이크
    //  - 음소거면 음소거 마이크(빨간 사선)
    // 아이콘은 배경이 투명한 전용 이미지(Resources/OptionsDesignTheme의 hudMic/hudMicMuted/hudMicGlow)를 쓴다.
    // 감도·음소거 조절은 ESC 메뉴와 옵션 화면에서 한다. 아래 직렬화 필드들은 VoiceChat 프리팹 연결을 깨지 않게 남겨 둔 것.
    public class VoiceChatHud : MonoBehaviour
    {
        [SerializeField] VoiceChatManager manager;
        [Tooltip("예전 음성 상태 패널 — 이제 항상 숨긴다")]
        [SerializeField] GameObject content;
        [SerializeField] Text statusText;
        [SerializeField] Text micText;
        [SerializeField] Slider micVolumeSlider;
        [SerializeField] Text micVolumeText;
        [SerializeField] Image micIcon;

        // 디자인 이미지(1536x864) 기준 위치·크기. 화면 높이에 맞춰 같은 비율로 커진다.
        const float DesignH = 864f;
        const float IconHeight = 52f;        // 마이크 이미지(투명 여백 포함) 높이
        const float Margin = 18f;            // 왼쪽·아래 여백

        float speakGlow;                     // 초록 테두리 밝기 (부드럽게 켜고 끈다)

        bool Visible
        {
            get
            {
                if (manager == null || !manager.Active || LGH.NetworkPlayer.Local == null) return false;
                // 대기방·도입부·엔딩·결과 화면에서는 그 화면이 화면을 차지하므로 그리지 않는다
                var flow = GameFlowSync.Instance;
                if (flow != null && flow.IsSpawned && flow.Stage != GameStage.Playing) return false;
                return !OptionsMenuController.IsOpen;
            }
        }

        void Update()
        {
            if (content != null && content.activeSelf) content.SetActive(false);
            bool speaking = manager != null && manager.Active && !manager.IsMicMuted && manager.SelfSpeaking;
            speakGlow = Mathf.MoveTowards(speakGlow, speaking ? 1f : 0f, Time.unscaledDeltaTime * 8f);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Visible) return;
            var theme = OptionsDesignTheme.Load();
            if (theme == null || theme.hudMic == null || theme.hudMicMuted == null) return;

            var old = GUI.matrix;
            float s = Screen.height / DesignH;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

            var icon = manager.IsMicMuted ? theme.hudMicMuted : theme.hudMic;
            float ih = IconHeight, iw = ih * icon.width / icon.height;
            var r = new Rect(Margin, DesignH - Margin - ih, iw, ih);

            var prevColor = GUI.color;
            // 말하는 중: 마이크 모양 테두리에 초록 빛 (hudMicGlow는 아이콘과 같은 크기라 같은 자리에 겹쳐 그린다)
            if (speakGlow > 0.01f && theme.hudMicGlow != null)
            {
                GUI.color = MeetYouGuiSkin.GuiColor(new Color(0.35f, 1f, 0.45f, speakGlow));
                GUI.DrawTexture(r, theme.hudMicGlow);
            }
            GUI.color = Color.white;
            GUI.DrawTexture(r, icon);
            GUI.color = prevColor;
            GUI.matrix = old;
        }
    }
}
