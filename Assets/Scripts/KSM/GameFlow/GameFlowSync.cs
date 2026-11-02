using System;
using Unity.Netcode;
using UnityEngine;

namespace CoopDemo
{
    // 로비(대기방) → 도입부 → 플레이 → 엔딩 → 결과 전체 진행 단계. 호스트(서버) 한 곳만 결정하고, 모두에게 NetworkVariable로 뿌린다.
    // 이 프로젝트의 다른 기믹(Gim002NetSync, PortraitPuzzle)과 같은 패턴: 판정/결정은 서버에서만, 클라이언트는 Rpc(SendTo.Server)로 요청만 한다.
    // Lobby -> Intro 전환 조건: 두 플레이어가 전부 접속해 있고, 둘 다 "준비" 버튼을 눌렀고, 그 상태로 3초가 지나면.
    // 인원이 빠지거나 누군가 준비를 취소하면 카운트다운은 취소되고 다시 준비를 기다린다.
    public enum GameStage { Lobby, Intro, Playing, Ending, Result }

    // 씬에 프리팹 인스턴스로 하나 둔다 (Assets/Prefabs/KSM/GameFlow/GameFlowSync.prefab, DefaultNetworkPrefabs에 등록).
    // 이 프로젝트는 씬 관리가 꺼져 있어서 프리팹이 아닌 씬 NetworkObject는 게스트 접속 시 사라진다 — 반드시 프리팹으로 둘 것.
    public class GameFlowSync : NetworkBehaviour
    {
        public static GameFlowSync Instance { get; private set; }

        readonly NetworkVariable<GameStage> stage = new NetworkVariable<GameStage>(
            GameStage.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // 2인 전제: 슬롯 0/1 별로 "이번 단계를 끝냈는가"만 기억한다 (NetworkPlayer와 같은 PlayerSlot 규칙: 호스트=0, 그 외=1).
        readonly NetworkVariable<bool> introDone0 = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> introDone1 = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> endingDone0 = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> endingDone1 = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // 대기방(Lobby) 준비 상태 — 같은 슬롯 규칙. 둘 다 true가 되면 서버가 카운트다운을 시작한다.
        readonly NetworkVariable<bool> readyLobby0 = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> readyLobby1 = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        // 카운트다운이 끝나는 서버 시각(ServerTime.Time 기준). 진행 중이 아니면 -1.
        readonly NetworkVariable<double> introCountdownEndAt = new NetworkVariable<double>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        const float LobbyCountdownSeconds = 3f;

        // 클리어 시간(P1): 서버 시계 기준 단일 값. "게임 시작"은 도입부가 끝나고 Playing이 된 시점으로 잡는다(컷신 시청 시간은 제외).
        readonly NetworkVariable<double> playingStartedAt = new NetworkVariable<double>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<double> clearedAt = new NetworkVariable<double>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public GameStage Stage => stage.Value;
        public double PlayingStartedAt => playingStartedAt.Value;
        public double ClearedAt => clearedAt.Value;
        // 둘 다 기록돼 있을 때만 의미 있는 값 (초 단위). 아직이면 음수.
        public double ClearSeconds => (playingStartedAt.Value >= 0 && clearedAt.Value >= 0) ? clearedAt.Value - playingStartedAt.Value : -1;

        // 로컬 플레이어 기준 대기방 준비 상태 — 대기방 UI가 "나"/"상대"를 구분해서 보여줄 때 씀.
        public bool LocalLobbyReady { get { return LocalSlot() == 0 ? readyLobby0.Value : readyLobby1.Value; } }
        public bool OtherLobbyReady { get { return LocalSlot() == 0 ? readyLobby1.Value : readyLobby0.Value; } }
        // 카운트다운 중이 아니면 음수. 중이면 남은 초(소수점 포함, 0 이상).
        public double LobbyCountdownRemaining
        {
            get
            {
                if (introCountdownEndAt.Value < 0) return -1;
                double now = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0;
                double remain = introCountdownEndAt.Value - now;
                return remain < 0 ? 0 : remain;
            }
        }

        // 로컬 UI가 구독하는 이벤트. 호스트/게스트 모두 NetworkVariable 콜백에서 울린다.
        public event Action<GameStage> StageChanged;

        void Awake() { Instance = this; }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            stage.OnValueChanged += OnStageChanged;
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                // 이 오브젝트는 씬에 계속 남아있는 하나뿐인 인스턴스라서(네트워크 세션이 끝나도 GameObject 자체는 안 지워짐),
                // 새 호스팅 세션이 시작될 때마다(= 이 메서드가 서버로서 다시 불릴 때마다) 이전 플레이의 값이 남아있을 수 있다.
                // 매번 새 방으로 취급해 전부 초기화한다.
                ServerResetForNewSession();
            }
            // 늦게 들어온 클라이언트도 NetworkVariable 특성상 현재 값을 바로 받지만, 구독 직후 한 번 수동으로 쏴줘서
            // "이미 어떤 단계인지" UI가 첫 프레임부터 알 수 있게 한다.
            StageChanged?.Invoke(stage.Value);
        }

        void ServerResetForNewSession()
        {
            stage.Value = GameStage.Lobby;
            readyLobby0.Value = false; readyLobby1.Value = false; introCountdownEndAt.Value = -1;
            introDone0.Value = false; introDone1.Value = false;
            endingDone0.Value = false; endingDone1.Value = false;
            playingStartedAt.Value = -1; clearedAt.Value = -1;
        }

        public override void OnNetworkDespawn()
        {
            stage.OnValueChanged -= OnStageChanged;
            if (NetworkManager != null) NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        }

        void OnStageChanged(GameStage prev, GameStage now) { StageChanged?.Invoke(now); }

        // 호스트 = 0, 그 외 = 1 (게스트가 다시 들어와 clientId가 바뀌어도 같은 자리를 쓴다 — PlayerSlot 참고)
        static int SlotOf(ulong clientId) { return PlayerSlot.Of(clientId); }
        int LocalSlot() { return SlotOf(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0); }

        // 두 명 다 접속해 있고 둘 다 준비를 눌렀으면 카운트다운을 시작/진행하고, 끝나면 Intro로 넘어간다.
        // 중간에 인원이 빠지거나(접속 끊김) 누군가 준비를 취소하면 카운트다운을 취소한다 — 전부 Update에서 서버만 판단.
        void Update()
        {
            if (!IsServer || stage.Value != GameStage.Lobby) return;

            bool twoPlayers = LGH.NetworkPlayer.All.Count >= 2;
            if (!twoPlayers)
            {
                if (readyLobby0.Value) readyLobby0.Value = false;
                if (readyLobby1.Value) readyLobby1.Value = false;
                if (introCountdownEndAt.Value >= 0) introCountdownEndAt.Value = -1;
                return;
            }

            bool bothReady = readyLobby0.Value && readyLobby1.Value;
            if (!bothReady)
            {
                if (introCountdownEndAt.Value >= 0) introCountdownEndAt.Value = -1;
                return;
            }

            if (introCountdownEndAt.Value < 0) introCountdownEndAt.Value = NetworkManager.ServerTime.Time + LobbyCountdownSeconds;
            else if (NetworkManager.ServerTime.Time >= introCountdownEndAt.Value) ServerAdvanceToIntro();
        }

        void OnClientConnected(ulong clientId)
        {
            // 접속/재접속 시점의 즉시 판단은 필요 없음 — 매 프레임 도는 Update()가 같은 조건을 계속 재확인한다.
        }

        // 클라이언트(호스트든 게스트든)가 대기방 준비 버튼을 누르거나 취소할 때 부르는 진입점.
        public void RequestLobbyReady(bool ready)
        {
            if (IsServer) ServerSetLobbyReady(NetworkManager.LocalClientId, ready);
            else LobbyReadyRpc(ready);
        }

        [Rpc(SendTo.Server)]
        void LobbyReadyRpc(bool ready, RpcParams p = default) { ServerSetLobbyReady(p.Receive.SenderClientId, ready); }

        void ServerSetLobbyReady(ulong clientId, bool ready)
        {
            if (!IsServer || stage.Value != GameStage.Lobby) return;
            if (SlotOf(clientId) == 0) readyLobby0.Value = ready; else readyLobby1.Value = ready;
        }

        // ---------------- 서버 전용: 단계 전환 (전부 idempotent — 지금 단계가 아니면 조용히 무시) ----------------

        void ServerAdvanceToIntro()
        {
            if (!IsServer || stage.Value != GameStage.Lobby) return;
            readyLobby0.Value = false;
            readyLobby1.Value = false;
            introCountdownEndAt.Value = -1;
            introDone0.Value = false;
            introDone1.Value = false;
            stage.Value = GameStage.Intro;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 개발자 모드(DevMode)의 "혼자 시작": 로비·도입부를 건너뛰고 바로 Playing으로 간다.
        // 호스트(서버)에서만, 접속한 플레이어가 한 명 이하일 때만 받아들인다 — 두 명이 있으면 상대의 도입부를 가로채지 않는다.
        public bool ServerDevSkipToPlaying()
        {
            if (!IsServer || (stage.Value != GameStage.Lobby && stage.Value != GameStage.Intro)) return false;
            if (LGH.NetworkPlayer.All.Count > 1) return false;
            readyLobby0.Value = false; readyLobby1.Value = false; introCountdownEndAt.Value = -1;
            introDone0.Value = true; introDone1.Value = true;
            endingDone0.Value = false; endingDone1.Value = false;
            clearedAt.Value = -1;
            playingStartedAt.Value = NetworkManager.ServerTime.Time;
            stage.Value = GameStage.Playing;
            return true;
        }
#endif

        // 최종 기믹(탈출 성공) 쪽에서 호출할 연결 지점. 아직 프로젝트에 "출구" 기믹이 없어서 누가 이 메서드를 부를지는 추후 결정
        // (지금 당장은 DebugEscapeTrigger의 비밀 커맨드가 임시로 이 자리를 대신 채운다 — 진짜 기믹이 생기면 그쪽에서 이 메서드만 부르면 됨).
        // 호스트에서만 의미가 있고, Playing 상태가 아니면 무시한다(중복 호출 방지).
        public void ServerConfirmEscapeSuccess()
        {
            if (!IsServer || stage.Value != GameStage.Playing) return;
            endingDone0.Value = false;
            endingDone1.Value = false;
            clearedAt.Value = NetworkManager.ServerTime.Time;
            stage.Value = GameStage.Ending;
        }

        // 클라이언트(호스트든 게스트든) 쪽에서 "탈출 성공"을 요청할 때 쓰는 진입점 — Request~Finished와 같은 패턴.
        public void RequestEscapeSuccess()
        {
            if (IsServer) ServerConfirmEscapeSuccess();
            else EscapeSuccessRpc();
        }

        [Rpc(SendTo.Server)]
        void EscapeSuccessRpc() { ServerConfirmEscapeSuccess(); }

        void ServerMarkIntroDone(ulong clientId)
        {
            if (!IsServer || stage.Value != GameStage.Intro) return;
            if (SlotOf(clientId) == 0) introDone0.Value = true; else introDone1.Value = true;
            if (introDone0.Value && introDone1.Value)
            {
                playingStartedAt.Value = NetworkManager.ServerTime.Time;
                stage.Value = GameStage.Playing;
            }
        }

        void ServerMarkEndingDone(ulong clientId)
        {
            if (!IsServer || stage.Value != GameStage.Ending) return;
            if (SlotOf(clientId) == 0) endingDone0.Value = true; else endingDone1.Value = true;
            if (endingDone0.Value && endingDone1.Value) stage.Value = GameStage.Result;
        }

        // ---------------- 클라이언트가 부르는 쪽 (호스트면 바로, 아니면 Rpc) ----------------

        public void RequestIntroFinished()
        {
            if (IsServer) ServerMarkIntroDone(NetworkManager.LocalClientId);
            else IntroFinishedRpc();
        }

        public void RequestEndingFinished()
        {
            if (IsServer) ServerMarkEndingDone(NetworkManager.LocalClientId);
            else EndingFinishedRpc();
        }

        [Rpc(SendTo.Server)]
        void IntroFinishedRpc(RpcParams p = default) { ServerMarkIntroDone(p.Receive.SenderClientId); }

        [Rpc(SendTo.Server)]
        void EndingFinishedRpc(RpcParams p = default) { ServerMarkEndingDone(p.Receive.SenderClientId); }

        // 로컬 플레이어 기준 "나는 끝냈는데 상대는 아직"인지 — 대기 화면 표시용.
        public bool LocalDoneWaitingForOther()
        {
            int mySlot = LocalSlot();
            if (stage.Value == GameStage.Intro)
            {
                bool mine = mySlot == 0 ? introDone0.Value : introDone1.Value;
                bool other = mySlot == 0 ? introDone1.Value : introDone0.Value;
                return mine && !other;
            }
            if (stage.Value == GameStage.Ending)
            {
                bool mine = mySlot == 0 ? endingDone0.Value : endingDone1.Value;
                bool other = mySlot == 0 ? endingDone1.Value : endingDone0.Value;
                return mine && !other;
            }
            return false;
        }
    }
}
