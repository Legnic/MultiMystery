using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace CoopDemo
{
    [DefaultExecutionOrder(-100)]
    public class CoopSessionManager : MonoBehaviour
    {
        public static CoopSessionManager Instance { get; private set; }

        public event Action<string> StatusChanged;
        public event Action<string> JoinCodeReady;

        ISession m_Session;
        bool m_ServicesReady;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            await EnsureServicesInitializedAsync();
        }

        async Task EnsureServicesInitializedAsync()
        {
            if (m_ServicesReady)
            {
                return;
            }

            try
            {
                StatusChanged?.Invoke("Unity 서비스 초기화 중...");
                await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                m_ServicesReady = true;
                StatusChanged?.Invoke("준비 완료. 호스팅 또는 참가를 선택하세요.");
            }
            catch (Exception e)
            {
                StatusChanged?.Invoke($"초기화 실패: {e.Message}");
            }
        }

        public async void HostGame()
        {
            await EnsureServicesInitializedAsync();

            if (!m_ServicesReady)
            {
                return;
            }

            try
            {
                StatusChanged?.Invoke("세션 생성 중...");

                var options = new SessionOptions
                {
                    Name = "CoopSession",
                    MaxPlayers = 2
                }.WithRelayNetwork();

                m_Session = await MultiplayerService.Instance.CreateSessionAsync(options);

                JoinCodeReady?.Invoke(m_Session.Code);
                StatusChanged?.Invoke($"호스팅 중 (참가 코드: {m_Session.Code})");
            }
            catch (Exception e)
            {
                StatusChanged?.Invoke($"호스팅 실패: {e.Message}");
            }
        }

        public async void JoinGame(string sessionCode)
        {
            await EnsureServicesInitializedAsync();

            if (!m_ServicesReady)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sessionCode))
            {
                StatusChanged?.Invoke("참가 코드를 입력하세요.");
                return;
            }

            try
            {
                StatusChanged?.Invoke("접속 중...");
                m_Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode.Trim());
                StatusChanged?.Invoke("접속 완료.");
            }
            catch (Exception e)
            {
                StatusChanged?.Invoke($"접속 실패: {e.Message}");
            }
        }

        public async void LeaveGame()
        {
            if (m_Session == null)
            {
                return;
            }

            try
            {
                await m_Session.LeaveAsync();
            }
            finally
            {
                m_Session = null;
                StatusChanged?.Invoke("세션을 종료했습니다.");
            }
        }
    }
}
