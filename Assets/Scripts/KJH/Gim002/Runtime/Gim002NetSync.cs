using System;
using Unity.Netcode;
using UnityEngine;

namespace Gim002
{
    // GIM-002 실제 네트워크 어댑터 (Netcode for GameObjects). 프로젝트의 다른 퍼즐(PortraitPuzzleSync, PotionCraftSync)과 같은 방식:
    //  - 판정은 호스트(IsServer) 한 곳에서만. 호스트는 LocalGim002Net(= Gim002Logic)을 "판정 엔진"으로 그대로 돌린다.
    //  - 클라이언트의 요청은 [Rpc(SendTo.Server)]로 호스트에게 간다.
    //  - 상태는 NetworkVariable 하나로 모두에게 동기화된다 → 나중에 들어온(재접속한) 플레이어도 접속 즉시 현재 상태를 받는다.
    //  - 일회성 사건(오답 연출, 성공음 등)은 [Rpc(SendTo.Everyone)]로 모두에게 알린다.
    //  - 다른 플레이어가 (재)접속하면 진행 중이던 장면만 처음(CURTAIN_CLOSED)으로 되돌린다 (기획 7장 재접속 규칙).
    //
    // 사용법: Assets/Prefabs/KJH/Gim002/Gim002Sync.prefab (NetworkObject + 이 컴포넌트)을 씬에 두고 NetworkManager의 네트워크 프리팹 목록에 등록한다.
    //  (이 프로젝트는 씬 관리가 꺼져 있어서, 프리팹으로 등록되지 않은 씬 NetworkObject는 클라이언트에 생기지 않는다)
    //  씬 오브젝트(설정 등)는 프리팹이 직접 참조할 수 없으므로 실행 중에 Gim002Controller.Instance에서 가져온다.
    public class Gim002NetSync : NetworkBehaviour, IGim002Net
    {
        public static Gim002NetSync Instance { get; private set; }

        // 네트워크로 보낼 상태. 작은 정수/불값만 담아 가볍게 보낸다.
        public struct NetState : INetworkSerializable, IEquatable<NetState>
        {
            public byte state, scene, progress;
            public sbyte doll, bg;
            public bool door, wound, curtain, board;

            public static NetState From(Gim002SyncState s)
            {
                return new NetState
                {
                    state = (byte)s.state, scene = (byte)s.sceneIndex, progress = (byte)s.progressCount,
                    doll = (sbyte)s.slotDoll, bg = (sbyte)s.slotBg,
                    door = s.musicDoorOpened, wound = s.stageWound, curtain = s.curtainOpen, board = s.scoreboardOpen,
                };
            }

            public Gim002SyncState ToSync()
            {
                return new Gim002SyncState
                {
                    state = (Gim002State)state, sceneIndex = scene, progressCount = progress,
                    slotDoll = doll, slotBg = bg,
                    musicDoorOpened = door, stageWound = wound, curtainOpen = curtain, scoreboardOpen = board,
                };
            }

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref state); s.SerializeValue(ref scene); s.SerializeValue(ref progress);
                s.SerializeValue(ref doll); s.SerializeValue(ref bg);
                s.SerializeValue(ref door); s.SerializeValue(ref wound); s.SerializeValue(ref curtain); s.SerializeValue(ref board);
            }

            public bool Equals(NetState o)
            {
                return state == o.state && scene == o.scene && progress == o.progress && doll == o.doll && bg == o.bg
                    && door == o.door && wound == o.wound && curtain == o.curtain && board == o.board;
            }
        }

        // 기본값 = 처음 상태 (LOCKED, 장면 1, 슬롯 비어 있음)
        readonly NetworkVariable<NetState> netState = new NetworkVariable<NetState>(
            NetState.From(new Gim002SyncState { sceneIndex = 1, slotDoll = -1, slotBg = -1 }),
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        LocalGim002Net authority;   // 호스트에서만 존재하는 판정 엔진

        public event Action<Gim002SyncState> OnStateSynced;
        public event Action<Gim002NetEvent, int> OnEvent;

        public bool IsAuthority { get { return IsServer; } }
        public Gim002SyncState Current { get { return authority != null ? authority.Current : netState.Value.ToSync(); } }

        void Awake() { Instance = this; }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            var controller = Gim002Controller.Instance;
            if (controller == null || controller.Config == null)
            {
                Debug.LogError("[GIM-002] 씬에 Gim002Controller(Config 연결)가 없어 네트워크 동기화를 시작할 수 없습니다.", this);
                return;
            }

            if (IsServer)
            {
                // 호스트: 판정 엔진을 만들고, 결과를 네트워크로 내보낸다
                authority = new LocalGim002Net(controller.Config);
                authority.OnStateSynced += s => { netState.Value = NetState.From(s); OnStateSynced?.Invoke(s); };
                authority.OnEvent += (e, v) => EventRpc((int)e, v);
                netState.Value = NetState.From(authority.Current);
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
            }
            else
            {
                // 클라이언트: 상태가 바뀔 때마다 화면에 반영
                netState.OnValueChanged += OnNetStateChanged;
            }

            controller.UseNet(this);
        }

        public override void OnNetworkDespawn()
        {
            netState.OnValueChanged -= OnNetStateChanged;
            if (NetworkManager != null) NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            authority = null;
            if (Gim002Controller.Instance != null) Gim002Controller.Instance.ReleaseNet(this);
        }

        void OnNetStateChanged(NetState prev, NetState now) { OnStateSynced?.Invoke(now.ToSync()); }

        // 다른 플레이어가 (재)접속하면 진행 중이던 장면을 처음으로 (성공 장면·열쇠 사용 여부는 유지)
        void OnClientConnected(ulong clientId)
        {
            if (clientId == NetworkManager.LocalClientId || authority == null) return;
            authority.ResetCurrentScene();
        }

        public void Tick(float deltaTime)
        {
            if (authority != null) authority.Tick(deltaTime);
        }

        // ---------------- 요청: 호스트면 바로 판정, 클라이언트면 RPC ----------------

        public void RequestGim001Solved() { if (authority != null) authority.RequestGim001Solved(); }

        public void RequestMusicDoorOpened() { if (authority != null) authority.RequestMusicDoorOpened(); else MusicDoorRpc(); }
        public void RequestStageWound() { if (authority != null) authority.RequestStageWound(); else WoundRpc(); }
        public void RequestCrankCompleted() { if (authority != null) authority.RequestCrankCompleted(); else CrankRpc(); }
        public void RequestPianoKey(SymbolId symbol) { if (authority != null) authority.RequestPianoKey(symbol); else PianoRpc((int)symbol); }

        public void RequestSlotChanged(DollId? doll, BackgroundId? bg)
        {
            if (authority != null) authority.RequestSlotChanged(doll, bg);
            else SlotRpc(Gim002SyncState.Encode(doll), Gim002SyncState.Encode(bg));
        }

        [Rpc(SendTo.Server)] void MusicDoorRpc() { if (authority != null) authority.RequestMusicDoorOpened(); }
        [Rpc(SendTo.Server)] void WoundRpc() { if (authority != null) authority.RequestStageWound(); }
        [Rpc(SendTo.Server)] void CrankRpc() { if (authority != null) authority.RequestCrankCompleted(); }
        [Rpc(SendTo.Server)] void PianoRpc(int symbol) { if (authority != null) authority.RequestPianoKey((SymbolId)symbol); }

        [Rpc(SendTo.Server)]
        void SlotRpc(int doll, int bg)
        {
            if (authority == null) return;
            authority.RequestSlotChanged(doll < 0 ? (DollId?)null : (DollId)doll, bg < 0 ? (BackgroundId?)null : (BackgroundId)bg);
        }

        // 호스트 → 모두 (호스트 자신 포함): 일회성 사건
        [Rpc(SendTo.Everyone)]
        void EventRpc(int e, int value) { OnEvent?.Invoke((Gim002NetEvent)e, value); }
    }
}
