using System;
using Unity.Netcode;
using UnityEngine;

namespace Gim004
{
    // GIM-004 실제 네트워크 어댑터 (Netcode for GameObjects). GIM-002/GIM-005와 같은 방식:
    //  - 판정은 호스트(IsServer) 한 곳에서만. 호스트는 LocalGim004Net(= Gim004Logic)을 판정 엔진으로 돌린다.
    //  - 클라이언트의 요청은 [Rpc(SendTo.Server)]로 호스트에게 간다. 도착 순서대로 처리된다.
    //  - 상태(선반 4자리에 놓인 화분, 판정 중/완료, 오답 횟수)는 NetworkVariable 하나로 동기화
    //    → 나중에 들어온(재접속한) 플레이어도 접속 즉시 선반 배치와 완료 여부를 그대로 받는다 (명세서 7장).
    //  - 일회성 사건(오답 실패음, 완료 안내)은 [Rpc(SendTo.Everyone)].
    //  - 손에 든 화분은 동기화하지 않는다: 접속이 끊기면 그 컴퓨터의 손(PlayerHand)이 화분을 처음 자리로 돌려보낸다.
    //
    // 사용법: Assets/Prefabs/KJH/Gim004/Gim004Sync.prefab (NetworkObject + 이 컴포넌트)을 씬 최상위에 프리팹 인스턴스로 두고
    //  DefaultNetworkPrefabs에 등록한다 (씬 관리가 꺼져 있어서, 등록되지 않은 씬 NetworkObject는 게스트에게 생기지 않는다).
    public class Gim004NetSync : NetworkBehaviour, IGim004Net
    {
        public static Gim004NetSync Instance { get; private set; }

        public struct NetState : INetworkSerializable, IEquatable<NetState>
        {
            public byte state;
            public sbyte s0, s1, s2, s3;
            public int wrong;

            public static NetState From(Gim004SyncState s)
            {
                return new NetState { state = (byte)s.state, s0 = s.s0, s1 = s.s1, s2 = s.s2, s3 = s.s3, wrong = s.wrongCount };
            }

            public Gim004SyncState ToSync()
            {
                return new Gim004SyncState { state = (Gim004State)state, s0 = s0, s1 = s1, s2 = s2, s3 = s3, wrongCount = wrong };
            }

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref state);
                s.SerializeValue(ref s0); s.SerializeValue(ref s1); s.SerializeValue(ref s2); s.SerializeValue(ref s3);
                s.SerializeValue(ref wrong);
            }

            public bool Equals(NetState o)
            {
                return state == o.state && s0 == o.s0 && s1 == o.s1 && s2 == o.s2 && s3 == o.s3 && wrong == o.wrong;
            }
        }

        // 기본값을 "빈 선반"으로 (default(NetState)는 모든 자리가 0번 화분이 되어 버린다)
        readonly NetworkVariable<NetState> netState = new NetworkVariable<NetState>(
            NetState.From(Gim004SyncState.Empty), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        LocalGim004Net authority;   // 호스트에서만 존재하는 판정 엔진

        public event Action<Gim004SyncState> OnStateSynced;
        public event Action<Gim004NetEvent, int> OnEvent;

        public bool IsAuthority { get { return IsServer; } }
        public Gim004SyncState Current { get { return authority != null ? authority.Current : netState.Value.ToSync(); } }

        void Awake() { Instance = this; }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            var controller = Gim004Controller.Instance;
            if (controller == null || controller.Config == null)
            {
                Debug.LogError("[GIM-004] 씬에 Gim004Controller(Config 연결)가 없어 네트워크 동기화를 시작할 수 없습니다. GIM004_Conservatory 프리팹이 씬에 있는지 확인하세요.", this);
                return;
            }

            if (IsServer)
            {
                authority = new LocalGim004Net(controller.Config);
                authority.OnStateSynced += s => { netState.Value = NetState.From(s); OnStateSynced?.Invoke(s); };
                authority.OnEvent += (e, v) => EventRpc((int)e, v);
                netState.Value = NetState.From(authority.Current);
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
            authority = null;
            if (Gim004Controller.Instance != null) Gim004Controller.Instance.ReleaseNet(this);
        }

        void OnNetStateChanged(NetState prev, NetState now) { OnStateSynced?.Invoke(now.ToSync()); }

        public void Tick(float deltaTime)
        {
            if (authority != null) authority.Tick(deltaTime);
        }

        // ---------------- 요청: 호스트면 바로 판정, 클라이언트면 RPC ----------------

        public void RequestPlace(int slot, int pot) { if (authority != null) authority.RequestPlace(slot, pot); else PlaceRpc(slot, pot); }
        public void RequestTake(int slot) { if (authority != null) authority.RequestTake(slot); else TakeRpc(slot); }

        [Rpc(SendTo.Server)] void PlaceRpc(int slot, int pot) { if (authority != null) authority.RequestPlace(slot, pot); }
        [Rpc(SendTo.Server)] void TakeRpc(int slot) { if (authority != null) authority.RequestTake(slot); }

        [Rpc(SendTo.Everyone)]
        void EventRpc(int e, int value) { OnEvent?.Invoke((Gim004NetEvent)e, value); }
    }
}
