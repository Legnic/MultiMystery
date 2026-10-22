using System;
using Unity.Netcode;
using UnityEngine;

namespace Gim005
{
    // GIM-005 실제 네트워크 어댑터 (Netcode for GameObjects). GIM-002(Gim002NetSync)와 같은 방식:
    //  - 판정은 호스트(IsServer) 한 곳에서만. 호스트는 LocalGim005Net(= Gim005Logic)을 판정 엔진으로 돌린다. 랜덤 배정도 호스트에서만.
    //  - 클라이언트의 요청은 [Rpc(SendTo.Server)]로 호스트에게 간다. 도착 순서대로 처리된다.
    //  - 상태(기믹 상태, 시도 번호, 덤불별 문양, 열린 덤불, 남은 시간, 패널 잠금)는 NetworkVariable 하나로 동기화
    //    → 나중에 들어온(재접속한) 플레이어도 접속 즉시 현재 상태(완료 여부 포함)를 받는다.
    //  - 일회성 사건(오답 경고음, 종소리, 시간 초과 연출 등)은 [Rpc(SendTo.Everyone)].
    //  - 다른 플레이어가 (재)접속하면 진행 중이던 시도를 Ready로 되돌리고 문양을 재배정한다 (기획 7장).
    //
    // 사용법: Assets/Prefabs/KJH/Gim005/Gim005Sync.prefab (NetworkObject + 이 컴포넌트)을 씬 최상위에 프리팹 인스턴스로 두고
    //  DefaultNetworkPrefabs에 등록한다 (씬 관리가 꺼져 있어서, 등록되지 않은 씬 NetworkObject는 게스트에게 생기지 않는다).
    public class Gim005NetSync : NetworkBehaviour, IGim005Net
    {
        public static Gim005NetSync Instance { get; private set; }

        public struct NetState : INetworkSerializable, IEquatable<NetState>
        {
            public byte state, attempt, open, gates;
            public uint assignment;
            public float remaining, lockRemaining;

            public static NetState From(Gim005SyncState s)
            {
                return new NetState
                {
                    state = (byte)s.state, attempt = (byte)Mathf.Min(255, s.attempt), open = s.openMask, gates = (byte)s.gateCount,
                    assignment = s.assignment, remaining = s.remaining, lockRemaining = s.lockRemaining,
                };
            }

            public Gim005SyncState ToSync()
            {
                return new Gim005SyncState
                {
                    state = (Gim005State)state, attempt = attempt, openMask = open, gateCount = gates,
                    assignment = assignment, remaining = remaining, lockRemaining = lockRemaining,
                };
            }

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref state); s.SerializeValue(ref attempt); s.SerializeValue(ref open); s.SerializeValue(ref gates);
                s.SerializeValue(ref assignment); s.SerializeValue(ref remaining); s.SerializeValue(ref lockRemaining);
            }

            public bool Equals(NetState o)
            {
                return state == o.state && attempt == o.attempt && open == o.open && gates == o.gates
                    && assignment == o.assignment && remaining == o.remaining && lockRemaining == o.lockRemaining;
            }
        }

        readonly NetworkVariable<NetState> netState = new NetworkVariable<NetState>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        LocalGim005Net authority;   // 호스트에서만 존재하는 판정 엔진

        public event Action<Gim005SyncState> OnStateSynced;
        public event Action<Gim005NetEvent, int> OnEvent;

        public bool IsAuthority { get { return IsServer; } }
        public Gim005SyncState Current { get { return authority != null ? authority.Current : netState.Value.ToSync(); } }

        void Awake() { Instance = this; }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            var controller = Gim005Controller.Instance;
            if (controller == null || controller.Config == null)
            {
                Debug.LogError("[GIM-005] 씬에 Gim005Controller(Config 연결)가 없어 네트워크 동기화를 시작할 수 없습니다. GIM005_Maze 프리팹이 씬에 있는지 확인하세요.", this);
                return;
            }

            if (IsServer)
            {
                authority = new LocalGim005Net(controller.Config);
                authority.OnStateSynced += s => { netState.Value = NetState.From(s); OnStateSynced?.Invoke(s); };
                authority.OnEvent += (e, v) => EventRpc((int)e, v);
                netState.Value = NetState.From(authority.Current);
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
            }
            else
            {
                netState.OnValueChanged += OnNetStateChanged;
            }

            controller.UseNet(this);
        }

        public override void OnNetworkDespawn()
        {
            netState.OnValueChanged -= OnNetStateChanged;
            if (NetworkManager != null) NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            authority = null;
            if (Gim005Controller.Instance != null) Gim005Controller.Instance.ReleaseNet(this);
        }

        void OnNetStateChanged(NetState prev, NetState now) { OnStateSynced?.Invoke(now.ToSync()); }

        void OnClientConnected(ulong clientId)
        {
            if (clientId == NetworkManager.LocalClientId || authority == null) return;
            authority.ResetAttempt();
        }

        public void Tick(float deltaTime)
        {
            if (authority != null) authority.Tick(deltaTime);
        }

        // ---------------- 요청: 호스트면 바로 판정, 클라이언트면 RPC ----------------

        public void RequestEnter() { if (authority != null) authority.RequestEnter(); else EnterRpc(); }
        public void RequestPress(int symbol) { if (authority != null) authority.RequestPress(symbol); else PressRpc(symbol); }
        public void RequestCenter() { if (authority != null) authority.RequestCenter(); else CenterRpc(); }

        [Rpc(SendTo.Server)] void EnterRpc() { if (authority != null) authority.RequestEnter(); }
        [Rpc(SendTo.Server)] void PressRpc(int symbol) { if (authority != null) authority.RequestPress(symbol); }
        [Rpc(SendTo.Server)] void CenterRpc() { if (authority != null) authority.RequestCenter(); }

        [Rpc(SendTo.Everyone)]
        void EventRpc(int e, int value) { OnEvent?.Invoke((Gim005NetEvent)e, value); }
    }
}
