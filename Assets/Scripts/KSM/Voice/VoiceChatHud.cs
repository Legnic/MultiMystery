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

        static readonly Color k_Idle = new Color(0.85f, 0.85f, 0.85f, 1f);
        static readonly Color k_Speaking = new Color(0.45f, 1f, 0.55f, 1f);
        static readonly Color k_Muted = new Color(1f, 0.5f, 0.45f, 1f);

        void Update()
        {
            if (manager == null || content == null) return;

            bool show = manager.Active;
            if (content.activeSelf != show) content.SetActive(show);
            if (!show) return;

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

            statusText.color = manager.RemoteSpeaking ? k_Speaking : k_Idle;
            if (manager.RemoteSpeaking) statusText.text += "   상대가 말하는 중";
        }
    }
}
