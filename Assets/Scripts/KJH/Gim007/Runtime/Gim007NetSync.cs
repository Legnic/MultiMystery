using System;
using Unity.Netcode;
using UnityEngine;

namespace Gim007
{
    // GIM-007 실제 네트워크 어댑터 (Netcode for GameObjects). GIM-004/GIM-005와 같은 방식:
    //  - 판정은 호스트(IsServer) 한 곳에서만. 호스트는 LocalGim007Net(= Gim007Logic)을 판정 엔진으로 돌린다.
    //  - 클라이언트의 요청은 [Rpc(SendTo.Server)]로 호스트에게 간다. 도착 순서대로 처리되므로
    //    같은 손잡이를 동시에 잡으려 하면 먼저 도착한 쪽이 차지한다 (명세서 7장).
    //    "누가" 보냈는지는 RPC 발신자 ID로 정한다 (요청 안에 ID를 넣지 않음).
    //  - 상태(손잡이 점유자, 진행 칸, 라운드 키·결과, 완료 여부)는 NetworkVariable 하나로 동기화
    //    → 나중에 들어온(재접속한) 플레이어도 접속 즉시 가구 위치와 완료 여부를 그대로 받는다.
    //  - 접속이 끊긴 플레이어의 손잡이는 호스트가 바로 놓는다 (가구 위치는 유지).
    //  - 일회성 사건(성공음·숨소리·완료)은 [Rpc(SendTo.Everyone)].
    //  - 입력 제한 시간: 각 컴퓨터가 자기 화면에 키가 뜬 순간부터 roundSeconds를 재서 성공/실패를 보고하고,
    //    호스트는 roundSeconds + latencyGraceSeconds까지 기다려 준다 (명세서 7장 "제한 시간과 지연").
    //
    // 사용법: Assets/Prefabs/KJH/Gim007/Gim007Sync.prefab (NetworkObject + 이 컴포넌트)을 씬 최상위에 프리팹 인스턴스로 두고
    //  DefaultNetworkPrefabs에 등록한다 (씬 관리가 꺼져 있어서, 등록되지 않은 씬 NetworkObject는 게스트에게 생기지 않는다).
    public class Gim007NetSync : NetworkBehaviour, IGim007Net
    {
        public static Gim007NetSync Instance { get; private set; }

        public struct NetState : INetworkSerializable, IEquatable<NetState>
        {
            public byte state;
            public ulong g0, g1;
            public byte step;
            public int round;
            public byte k0, k1, r0, r1;
            public int fails;

            public static NetState From(Gim007SyncState s)
            {
                return new NetState
                {
                    state = (byte)s.state, g0 = s.grip0, g1 = s.grip1, step = (byte)s.step, round = s.round,
                    k0 = (byte)s.key0, k1 = (byte)s.key1, r0 = (byte)s.result0, r1 = (byte)s.result1, fails = s.failCount,
                };
            }

            public Gim007SyncState ToSync()
            {
                return new Gim007SyncState
                {
                    state = (Gim007State)state, grip0 = g0, grip1 = g1, step = step, round = round,
                    key0 = (Gim007Key)k0, key1 = (Gim007Key)k1, result0 = (Gim007RoundResult)r0, result1 = (Gim007RoundResult)r1, failCount = fails,
                };
            }

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref state);
                s.SerializeValue(ref g0); s.SerializeValue(ref g1);
                s.SerializeValue(ref step);
                s.SerializeValue(ref round);
                s.SerializeValue(ref k0); s.SerializeValue(ref k1); s.SerializeValue(ref r0); s.SerializeValue(ref r1);
                s.SerializeValue(ref fails);
            }

            public bool Equals(NetState o)
            {
                return state == o.state && g0 == o.g0 && g1 == o.g1 && step == o.step && round == o.round
                    && k0 == o.k0 && k1 == o.k1 && r0 == o.r0 && r1 == o.r1 && fails == o.fails;
            }
        }

        // 기본값을 "아무도 안 잡음"으로 (default(NetState)는 두 손잡이 모두 0번 클라이언트 = 호스트가 잡은 것이 되어 버린다)
        readonly NetworkVariable<NetState> netState = new NetworkVariable<NetState>(
            NetState.From(Gim007SyncState.Empty), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        LocalGim007Net authority;   // 호스트에서만 존재하는 판정 엔진

        public event Action<Gim007SyncState> OnStateSynced;
        public event Action<Gim007NetEvent, int> OnEvent;

        public bool IsAuthority { get { return IsServer; } }
        public Gim007SyncState Current { get { return authority != null ? authority.Current : netState.Value.ToSync(); } }
        public ulong LocalPlayerId { get { return NetworkManager != null ? NetworkManager.LocalClientId : 0; } }

        void Awake() { Instance = this; }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            var controller = Gim007Controller.Instance;
            if (controller == null || controller.Config == null)
            {
                Debug.LogError("[GIM-007] 씬에 Gim007Controller(Config 연결)가 없어 네트워크 동기화를 시작할 수 없습니다. GIM007_MasterBedroom 프리팹이 씬에 있는지 확인하세요.", this);
                return;
            }

            if (IsServer)
            {
                authority = new LocalGim007Net(controller.Config);
                authority.OnStateSynced += s => { netState.Value = NetState.From(s); OnStateSynced?.Invoke(s); };
                authority.OnEvent += (e, v) => EventRpc((int)e, v);
                netState.Value = NetState.From(authority.Current);
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
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
            if (authority != null && NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            authority = null;
            if (Gim007Controller.Instance != null) Gim007Controller.Instance.ReleaseNet(this);
        }

        void OnNetStateChanged(NetState prev, NetState now) { OnStateSynced?.Invoke(now.ToSync()); }

        void OnClientDisconnected(ulong clientId)
        {
            if (authority != null) authority.ReleaseAs(clientId);
        }

        public void Tick(float deltaTime)
        {
            if (authority != null) authority.Tick(deltaTime);
        }

        // ---------------- 요청: 호스트면 바로 판정, 클라이언트면 RPC ----------------

        public void RequestGrip(int handle) { if (authority != null) authority.GripAs(LocalPlayerId, handle); else GripRpc(handle); }
        public void RequestRelease() { if (authority != null) authority.ReleaseAs(LocalPlayerId); else ReleaseRpc(); }
        public void RequestReport(int round, bool success) { if (authority != null) authority.ReportAs(LocalPlayerId, round, success); else ReportRpc(round, success); }

        [Rpc(SendTo.Server)]
        void GripRpc(int handle, RpcParams p = default) { if (authority != null) authority.GripAs(p.Receive.SenderClientId, handle); }

        [Rpc(SendTo.Server)]
        void ReleaseRpc(RpcParams p = default) { if (authority != null) authority.ReleaseAs(p.Receive.SenderClientId); }

        [Rpc(SendTo.Server)]
        void ReportRpc(int round, bool success, RpcParams p = default) { if (authority != null) authority.ReportAs(p.Receive.SenderClientId, round, success); }

        [Rpc(SendTo.Everyone)]
        void EventRpc(int e, int value) { OnEvent?.Invoke((Gim007NetEvent)e, value); }
    }
}
