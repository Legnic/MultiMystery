using UnityEngine;
using UnityEngine.UI;

namespace CoopDemo
{
    // 화면 왼쪽 아래에 음성 채팅 상태를 보여 주는 간단한 HUD.
    // 음성 서비스가 시작되기 전(타이틀 화면)에는 숨겨져 있다.
    public class VoiceChatHud : MonoBehaviour
    {
        [SerializeField] VoiceChatManager manager;
        [SerializeField] GameObject content;
        [SerializeField] Text statusText;
        [SerializeField] Text micText;
        [SerializeField] Slider micVolumeSlider;
        [SerializeField] Text micVolumeText;
        [Tooltip("마이크 켜짐/꺼짐 아이콘 (스프라이트는 Resources/MeetYouUiTheme의 micOn/micMuted)")]
        [SerializeField] Image micIcon;

        static readonly Color k_Idle = new Color(0.85f, 0.85f, 0.85f, 1f);
        static readonly Color k_Speaking = new Color(0.45f, 1f, 0.55f, 1f);
        static readonly Color k_Muted = new Color(1f, 0.5f, 0.45f, 1f);

        bool m_SliderHooked;

        void Start()
        {
            if (micVolumeSlider == null) return;
            micVolumeSlider.minValue = -50;
            micVolumeSlider.maxValue = 50;
            micVolumeSlider.wholeNumbers = true;
        }

        void Update()
        {
            if (manager == null || content == null) return;

            // 방 만들기(로비) 화면에서는 아직 음성 서비스만 켜져 있을 뿐 실제 플레이어가 없으므로 숨긴다.
            // 방을 만들거나 참가해서 플레이어가 스폰된 뒤(플레이 화면)에만 보여준다.
            bool show = manager.Active && LGH.NetworkPlayer.Local != null;
            if (content.activeSelf != show) content.SetActive(show);
            if (!show) return;

            // 음성 서비스가 막 준비됐을 때 한 번만 슬라이더를 현재 값으로 맞춘다 (이후엔 사용자가 직접 조작).
            if (!m_SliderHooked && micVolumeSlider != null)
            {
                m_SliderHooked = true;
                micVolumeSlider.SetValueWithoutNotify(manager.MicVolume);
                micVolumeSlider.onValueChanged.AddListener(OnMicVolumeSliderChanged);
                UpdateMicVolumeText(manager.MicVolume);
            }

            statusText.text = manager.Status
                + (manager.IsInChannel ? $"   (상대 {manager.RemoteCount}명)" : string.Empty);
            if (manager.IsInChannel && manager.UsesDistanceVolume && manager.RemoteDistance >= 0f)
            {
                statusText.text += $"   거리 {manager.RemoteDistance:F0}m / 음량 {manager.RemoteVolumeAdjust}";
            }

            if (manager.IsMicMuted)
            {
                micText.text = "마이크 꺼짐  [M]";
                micText.color = k_Muted;
            }
            else
            {
                micText.text = manager.SelfSpeaking ? "마이크 켜짐 - 말하는 중  [M]" : "마이크 켜짐  [M]";
                micText.color = manager.SelfSpeaking ? k_Speaking : k_Idle;
            }

            UpdateMicIcon();

            statusText.color = manager.RemoteSpeaking ? k_Speaking : k_Idle;
            if (manager.RemoteSpeaking) statusText.text += "   상대가 말하는 중";
        }

        // 켜짐/꺼짐에 맞는 아이콘으로 바꾸고, 내가 말하는 중이면 초록빛으로 밝힌다 (꺼짐 아이콘은 빨간 X가 이미 들어 있다)
        void UpdateMicIcon()
        {
            if (micIcon == null) return;
            var theme = MeetYouGuiSkin.Theme;
            if (theme == null) return;
            var sprite = manager.IsMicMuted ? theme.micMuted : theme.micOn;
            if (sprite != null && micIcon.sprite != sprite) micIcon.sprite = sprite;
            micIcon.color = (!manager.IsMicMuted && manager.SelfSpeaking) ? new Color(0.7f, 1f, 0.7f, 1f) : Color.white;
        }

        void OnMicVolumeSliderChanged(float value)
        {
            int volume = Mathf.RoundToInt(value);
            manager.SetMicVolume(volume);
            UpdateMicVolumeText(volume);
        }

        void UpdateMicVolumeText(int volume)
        {
            if (micVolumeText != null) micVolumeText.text = $"마이크 감도 {volume:+#;-#;0}";
        }
    }
}
