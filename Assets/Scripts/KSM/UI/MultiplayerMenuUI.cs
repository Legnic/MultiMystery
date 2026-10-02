using UnityEngine;
using UnityEngine.UI;

namespace CoopDemo
{
    public class MultiplayerMenuUI : MonoBehaviour
    {
        [SerializeField] Button hostButton;
        [SerializeField] Button joinButton;
        [SerializeField] InputField joinCodeInput;
        [SerializeField] Text statusText;
        [SerializeField] Text joinCodeDisplay;

        void OnEnable()
        {
            if (CoopSessionManager.Instance != null)
            {
                CoopSessionManager.Instance.StatusChanged += HandleStatusChanged;
                CoopSessionManager.Instance.JoinCodeReady += HandleJoinCodeReady;
            }

            if (hostButton != null) hostButton.onClick.AddListener(HandleHostClicked);
            if (joinButton != null) joinButton.onClick.AddListener(HandleJoinClicked);
        }

        void OnDisable()
        {
            if (CoopSessionManager.Instance != null)
            {
                CoopSessionManager.Instance.StatusChanged -= HandleStatusChanged;
                CoopSessionManager.Instance.JoinCodeReady -= HandleJoinCodeReady;
            }

            if (hostButton != null) hostButton.onClick.RemoveListener(HandleHostClicked);
            if (joinButton != null) joinButton.onClick.RemoveListener(HandleJoinClicked);
        }

        void HandleHostClicked()
        {
            CoopSessionManager.Instance.HostGame();
        }

        void HandleJoinClicked()
        {
            CoopSessionManager.Instance.JoinGame(joinCodeInput != null ? joinCodeInput.text : string.Empty);
        }

        void HandleStatusChanged(string status)
        {
            if (statusText != null)
            {
                statusText.text = status;
            }
        }

        void HandleJoinCodeReady(string code)
        {
            if (joinCodeDisplay != null)
            {
                joinCodeDisplay.text = $"참가 코드: {code}";
            }
        }
    }
}
