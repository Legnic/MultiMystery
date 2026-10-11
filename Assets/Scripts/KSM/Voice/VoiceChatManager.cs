using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopDemo
{
    // 2인 협동 방의 음성 채팅.
    // - 방(세션)이 생기거나 참가하면 자동으로 같은 음성 채널에 들어가고, 방이 사라지면 나온다.
    // - UnityServices 초기화/익명 로그인은 MultiplayerMenu가 이미 한다. 여기서는 그 완료를 기다렸다가 Vivox만 붙인다.
    // - 마이크는 기본적으로 항상 켜져 있고, 게임 중(커서가 잠긴 상태)에 toggleMicKey로 껐다 켤 수 있다.
    public class VoiceChatManager : MonoBehaviour
    {
        public static VoiceChatManager Instance { get; private set; }

        [Header("방식")]
        [SerializeField] VoiceChatMode mode = VoiceChatMode.GroupWithDistanceVolume;

        [Header("거리 음량 (GroupWithDistanceVolume)")]
        [Tooltip("이 거리(m) 안에서는 상대 목소리가 최대 음량으로 들린다.")]
        [SerializeField, Min(0f)] float nearDistance = 6f;
        [Tooltip("이 거리(m) 이상에서는 최소 음량(farVolumeAdjust)으로 들린다. 사이 구간은 선형으로 줄어든다.")]
        [SerializeField, Min(0.1f)] float farDistance = 40f;
        [Tooltip("가장 멀 때의 음량 조정값. 0이 기본 음량이고 -50이면 완전히 안 들린다. 0보다 작게 두면 아무리 멀어도 소리가 완전히 끊기지 않는다.")]
        [SerializeField, Range(-50, 0)] int farVolumeAdjust = -20;
        [Tooltip("거리 음량을 갱신하는 간격(초)")]
        [SerializeField, Min(0.02f)] float volumeUpdateInterval = 0.1f;

        [Header("마이크")]
        [SerializeField] bool startMicMuted;
        [SerializeField] Key toggleMicKey = Key.M;

        readonly Dictionary<VivoxParticipant, Action> m_SpeechHandlers = new Dictionary<VivoxParticipant, Action>();

        bool m_Initializing;
        bool m_Ready;           // Vivox 로그인까지 끝났는가
        bool m_Subscribed;
        bool m_SessionHooked;
        string m_Channel;       // 지금 들어가 있거나 들어가는 중인 채널 이름 (없으면 null)
        string m_Status = "음성: 대기 중";

        float m_NextVolumeUpdate;

        public VoiceChatMode ActiveMode => mode;
        public bool UsesDistanceVolume => mode == VoiceChatMode.GroupWithDistanceVolume;
        // 거리 음량 모드에서 마지막으로 잰 상대와의 거리(m)와 적용한 음량 조정값. 상대가 없으면 거리는 -1.
        public float RemoteDistance { get; private set; } = -1f;
        public int RemoteVolumeAdjust { get; private set; }
        public string Status => m_Status;
        public bool IsReady => m_Ready;
        public bool IsInChannel { get; private set; }
        public bool IsMicMuted => m_Ready && VivoxService.Instance.IsInputDeviceMuted;
        public bool SelfSpeaking { get; private set; }
        public bool RemoteSpeaking { get; private set; }
        public int RemoteCount { get; private set; }
        // 서비스 초기화를 시작했거나 끝난 뒤인가 (HUD 표시 여부 판단용)
        public bool Active => m_Initializing || m_Ready;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void Update()
        {
            // MultiplayerMenu가 UnityServices 초기화 + 로그인을 끝내면 그때 시작한다.
            if (!m_Initializing && !m_Ready
                && UnityServices.State == ServicesInitializationState.Initialized
                && AuthenticationService.Instance.IsSignedIn)
            {
                InitializeVoiceAsync();
            }

            HandleMicToggleInput();

            if (UsesDistanceVolume && IsInChannel && Time.unscaledTime >= m_NextVolumeUpdate)
            {
                m_NextVolumeUpdate = Time.unscaledTime + volumeUpdateInterval;
                UpdateDistanceVolume();
            }
        }

        // 상대 플레이어와의 거리를 재서 상대 목소리의 음량을 조절한다. (2인 협동 전제: 나 말고 다른 참가자는 모두 같은 상대로 본다)
        void UpdateDistanceVolume()
        {
            int adjust = 0;
            float distance = -1f;

            var local = LGH.NetworkPlayer.Local;
            var remote = FindRemotePlayer(local);
            if (local != null && remote != null)
            {
                distance = Vector3.Distance(local.transform.position, remote.transform.position);
                adjust = ComputeVolumeAdjust(distance, nearDistance, farDistance, farVolumeAdjust);
            }

            RemoteDistance = distance;
            RemoteVolumeAdjust = adjust;

            foreach (var p in m_SpeechHandlers.Keys)
            {
                if (!p.IsSelf && p.LocalVolume != adjust) p.SetLocalVolume(adjust);
            }
        }

        // 거리 → 음량 조정값. near 이하 0, far 이상 farAdjust, 사이는 선형.
        public static int ComputeVolumeAdjust(float distance, float near, float far, int farAdjust)
        {
            float t = Mathf.InverseLerp(near, Mathf.Max(far, near + 0.01f), distance);
            return Mathf.RoundToInt(Mathf.Lerp(0f, farAdjust, t));
        }

        static LGH.NetworkPlayer FindRemotePlayer(LGH.NetworkPlayer local)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (local == null || nm == null || !nm.IsListening) return null;

            foreach (var networkObject in nm.SpawnManager.SpawnedObjects.Values)
            {
                var player = networkObject.GetComponent<LGH.NetworkPlayer>();
                if (player != null && player != local) return player;
            }

            return null;
        }

        void HandleMicToggleInput()
        {
            // 로비 화면의 입력창에 M을 쳐도 토글되지 않도록, 게임 중(커서 잠김)일 때만 받는다.
            if (!m_Ready || Cursor.lockState != CursorLockMode.Locked) return;
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[toggleMicKey].wasPressedThisFrame) return;
            SetMicMuted(!VivoxService.Instance.IsInputDeviceMuted);
        }

        public void SetMicMuted(bool muted)
        {
            if (!m_Ready) return;
            if (muted) VivoxService.Instance.MuteInputDevice();
            else VivoxService.Instance.UnmuteInputDevice();
        }

        async void InitializeVoiceAsync()
        {
            m_Initializing = true;
            try
            {
                SetStatus("음성: 서비스 초기화 중...");
                try
                {
                    await VivoxService.Instance.InitializeAsync();
                }
                catch (Exception e) when (e.Message.Contains("5041") || e.Message.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // 에디터에서 도메인 리로드 없이 다시 재생한 경우: 이미 초기화되어 있다.
                }

                Subscribe();

                if (!VivoxService.Instance.IsLoggedIn)
                {
                    string id = AuthenticationService.Instance.PlayerId ?? "player";
                    await VivoxService.Instance.LoginAsync(new LoginOptions { DisplayName = "P-" + id.Substring(0, Math.Min(6, id.Length)) });
                }

                m_Ready = true;
                if (startMicMuted) VivoxService.Instance.MuteInputDevice();

                HookSessions();
                SetStatus("음성: 방에 들어오면 자동 연결");
            }
            catch (Exception e)
            {
                m_Initializing = false;
                SetStatus("음성: 초기화 실패 - " + e.Message);
                Debug.LogException(e, this);
            }
        }

        // ---------- 세션(방) 연동 ----------

        void HookSessions()
        {
            if (m_SessionHooked) return;
            m_SessionHooked = true;

            MultiplayerService.Instance.SessionAdded += OnSessionAdded;
            MultiplayerService.Instance.SessionRemoved += OnSessionRemoved;

            // 음성 초기화가 늦어서 이미 방에 들어와 있는 경우
            foreach (var session in MultiplayerService.Instance.Sessions.Values)
            {
                OnSessionAdded(session);
            }
        }

        void OnSessionAdded(ISession session)
        {
            JoinChannelAsync("mansion-" + session.Id);
        }

        void OnSessionRemoved(ISession session)
        {
            LeaveChannelAsync();
        }

        async void JoinChannelAsync(string channelName)
        {
            if (m_Channel == channelName) return;
            m_Channel = channelName;
            SetStatus("음성: 채널 연결 중...");
            try
            {
                await VivoxService.Instance.JoinGroupChannelAsync(
                    channelName, ChatCapability.AudioOnly, new ChannelOptions { MakeActiveChannelUponJoining = true });
            }
            catch (OperationCanceledException)
            {
                // 채널에 들어가는 도중 재생을 멈추는 등 Vivox가 먼저 종료된 경우. 오류가 아니다.
                m_Channel = null;
            }
            catch (Exception e)
            {
                m_Channel = null;
                SetStatus("음성: 채널 연결 실패 - " + e.Message);
                Debug.LogException(e, this);
            }
        }

        async void LeaveChannelAsync()
        {
            string channel = m_Channel;
            m_Channel = null;
            if (channel == null) return;
            try
            {
                await VivoxService.Instance.LeaveChannelAsync(channel);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        // ---------- Vivox 이벤트 ----------

        void Subscribe()
        {
            if (m_Subscribed) return;
            m_Subscribed = true;
            var vivox = VivoxService.Instance;
            vivox.LoggedOut += OnLoggedOut;
            vivox.ChannelJoined += OnChannelJoined;
            vivox.ChannelLeft += OnChannelLeft;
            vivox.ParticipantAddedToChannel += OnParticipantAdded;
            vivox.ParticipantRemovedFromChannel += OnParticipantRemoved;
        }

        void Unsubscribe()
        {
            if (!m_Subscribed) return;
            m_Subscribed = false;
            var vivox = VivoxService.Instance;
            if (vivox == null) return;
            vivox.LoggedOut -= OnLoggedOut;
            vivox.ChannelJoined -= OnChannelJoined;
            vivox.ChannelLeft -= OnChannelLeft;
            vivox.ParticipantAddedToChannel -= OnParticipantAdded;
            vivox.ParticipantRemovedFromChannel -= OnParticipantRemoved;
        }

        void OnLoggedOut()
        {
            m_Ready = false;
            m_Initializing = false;
            IsInChannel = false;
            m_Channel = null;
            SetStatus("음성: 연결이 끊어짐");
        }

        void OnChannelJoined(string channelName)
        {
            if (channelName != m_Channel) return;
            IsInChannel = true;
            SetStatus("음성: 연결됨");
        }

        void OnChannelLeft(string channelName)
        {
            if (channelName != m_Channel && m_Channel != null) return;
            IsInChannel = false;
            RemoteCount = 0;
            RemoteDistance = -1f;
            RemoteVolumeAdjust = 0;
            SelfSpeaking = false;
            RemoteSpeaking = false;
            SetStatus("음성: 방에 들어오면 자동 연결");
        }

        void OnParticipantAdded(VivoxParticipant participant)
        {
            if (m_SpeechHandlers.ContainsKey(participant)) return;
            Action handler = RefreshSpeaking;
            m_SpeechHandlers[participant] = handler;
            participant.ParticipantSpeechDetected += handler;
            RefreshParticipants();
        }

        void OnParticipantRemoved(VivoxParticipant participant)
        {
            if (m_SpeechHandlers.TryGetValue(participant, out var handler))
            {
                participant.ParticipantSpeechDetected -= handler;
                m_SpeechHandlers.Remove(participant);
            }
            RefreshParticipants();
        }

        void RefreshParticipants()
        {
            int remote = 0;
            foreach (var p in m_SpeechHandlers.Keys)
            {
                if (!p.IsSelf) remote++;
            }
            RemoteCount = remote;
            RefreshSpeaking();
        }

        void RefreshSpeaking()
        {
            bool self = false, remote = false;
            foreach (var p in m_SpeechHandlers.Keys)
            {
                if (!p.SpeechDetected) continue;
                if (p.IsSelf) self = true;
                else remote = true;
            }
            SelfSpeaking = self;
            RemoteSpeaking = remote;
        }

        void SetStatus(string status)
        {
            m_Status = status;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;

            foreach (var pair in m_SpeechHandlers) pair.Key.ParticipantSpeechDetected -= pair.Value;
            m_SpeechHandlers.Clear();

            if (m_SessionHooked && UnityServices.State == ServicesInitializationState.Initialized)
            {
                MultiplayerService.Instance.SessionAdded -= OnSessionAdded;
                MultiplayerService.Instance.SessionRemoved -= OnSessionRemoved;
            }

            Unsubscribe();
        }
    }
}
