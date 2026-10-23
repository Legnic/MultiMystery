using System;
using System.Collections.Generic;
using LGH;
using UnityEngine;
using UnityEngine.Events;

namespace Gim007
{
    // GIM-007의 중심. GIM007_MasterBedroom 프리팹 안에 하나 들어 있다 (씬에 하나만).
    // 하는 일:
    //  1) 어떤 네트워크 어댑터(IGim007Net)를 쓸지 정한다. 기본은 LocalGim007Net, 네트워크 게임이 시작돼 Gim007NetSync가 스폰되면 그쪽으로 바꾼다.
    //  2) 받은 상태(State)와 사건(EventReceived)을 화면 스크립트들(가구, 손잡이, 미는 플레이어, 발판)에게 나눠 준다.
    //  3) 비밀문(드레스룸 양문)을 "이름으로 찾아" 완료 전에는 잠그고, 완료되면 열린 채로 고정한다.
    //     → LGH 맵 프리팹(Ravenwood_LGH)을 고치거나 컴포넌트를 붙이지 않는다.
    //       문 상태는 매 프레임 "기믹 상태"에 맞추므로 늦게 들어온 사람·방 초기화 후에도 같게 보인다.
    [DefaultExecutionOrder(-50)]
    public class Gim007Controller : MonoBehaviour
    {
        public static Gim007Controller Instance { get; private set; }

        // 완료 시 모든 컴퓨터에서 한 번 (후속 기믹 연결용: 다락방 보물상자 등)
        public static event Action Completed;

        [Tooltip("판정 수치·문·문구가 담긴 설정 에셋")]
        [SerializeField] Gim007Config config;

        [Tooltip("상태 변화와 사건을 Console에 출력")]
        [SerializeField] bool logToConsole = true;

        [Header("혼자 테스트용 (네트워크 없이 Play할 때만 동작)")]
        [Tooltip("켜면 내가 손잡이를 잡을 때 가짜 동료가 반대쪽을 잡고, 라운드마다 자동으로 입력한다")]
        [SerializeField] bool debugFakePartner = false;
        [Tooltip("가짜 동료가 라운드마다 성공할 확률")]
        [Range(0f, 1f)][SerializeField] float debugPartnerSuccessRate = 0.85f;
        [Tooltip("가짜 동료가 키를 누르기까지 걸리는 시간(초)")]
        [SerializeField] float debugPartnerDelay = 0.5f;

        [Tooltip("GIM-007 완료 시 판정 담당(호스트, 혼자면 로컬)에서 한 번 실행")]
        public UnityEvent onCompleteAuthority;

        public Gim007Config Config { get { return config; } }
        public IGim007Net Net { get { return net; } }

        public Gim007SyncState State { get; private set; } = Gim007SyncState.Empty;
        public Gim007SyncState Previous { get; private set; } = Gim007SyncState.Empty;

        public event Action<Gim007SyncState, Gim007SyncState> StateChanged;   // (이전, 새)
        public event Action<Gim007NetEvent, int> EventReceived;

        public bool IsComplete { get { return State.IsComplete; } }
        public ulong LocalPlayerId { get { return net != null ? net.LocalPlayerId : LocalGim007Net.OfflinePlayerId; } }
        // 이 컴퓨터 플레이어가 잡고 있는 손잡이 (없으면 -1)
        public int LocalHandle { get { return State.HandleOf(LocalPlayerId); } }
        // 진행률 0~1 (가구 위치)
        public float Progress01 { get { return config.rules.requiredSteps > 0 ? Mathf.Clamp01((float)State.step / config.rules.requiredSteps) : 1f; } }
        // 비밀문 위치 (발판 덜컥거림 소리를 문 쪽에서 내기 위해). 문을 못 찾았으면 null
        public Vector3? DoorPosition { get { return doors.Count > 0 && doors[0].door != null ? doors[0].door.transform.position + Vector3.up * 1.2f : (Vector3?)null; } }

        LocalGim007Net localNet;
        IGim007Net net;

        struct DoorBinding { public InteractableDoor door; public Gim007DoorTarget target; }
        readonly List<DoorBinding> doors = new List<DoorBinding>();

        const ulong FakePartnerId = 9999;
        float fakePartnerAt = -1f;
        int fakePartnerRound = -1;

        void Awake()
        {
            Instance = this;
            if (config == null)
            {
                Debug.LogError("[GIM-007] Gim007Controller에 Config가 연결되지 않았습니다. Tools/KJH/GIM-007/Build All을 실행하거나 직접 연결하세요.", this);
                enabled = false;
                return;
            }
            string err = config.rules.Validate();
            if (err != null) Debug.LogError("[GIM-007] Config 수치 설정 오류: " + err, this);

            localNet = new LocalGim007Net(config);
            UseNet(localNet);
        }

        void Start()
        {
            ResolveDoors();
            ApplyDoors();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Bind(net, false);
        }

        void Update()
        {
            if (net != null) net.Tick(Time.deltaTime);
            if (debugFakePartner) TickFakePartner();
            // 방 초기화(RoomResetter)·늦은 접속 등으로 문이 바뀌어도 기믹 상태에 맞게 다시 맞춘다 (문 2짝이라 매 프레임 확인해도 가볍다)
            ApplyDoors();
        }

        // ---------- 어댑터 교체 ----------

        public void UseNet(IGim007Net n)
        {
            if (n == null || n == net) return;
            Bind(net, false);
            net = n;
            Bind(net, true);
            Log("어댑터 사용: " + n.GetType().Name + (n.IsAuthority ? " (판정 담당)" : ""));
            ApplyState(net.Current);
        }

        public void ReleaseNet(IGim007Net n)
        {
            if (n != net) return;
            if (localNet != null) UseNet(localNet);
        }

        void Bind(IGim007Net n, bool on)
        {
            if (n == null) return;
            if (on) { n.OnStateSynced += ApplyState; n.OnEvent += ApplyEvent; }
            else { n.OnStateSynced -= ApplyState; n.OnEvent -= ApplyEvent; }
        }

        void ApplyState(Gim007SyncState s)
        {
            Previous = State;
            State = s;
            if (!Previous.Equals(s)) Log("상태 → " + s);
            StateChanged?.Invoke(Previous, s);
        }

        void ApplyEvent(Gim007NetEvent e, int value)
        {
            Log("사건: " + e + " (" + value + ")");
            EventReceived?.Invoke(e, value);
            if (e == Gim007NetEvent.Complete)
            {
                ApplyDoors();
                Completed?.Invoke();
                if (net.IsAuthority) onCompleteAuthority?.Invoke();
            }
        }

        // ---------- 비밀문 ----------

        void ResolveDoors()
        {
            doors.Clear();
            if (config.secretDoors == null) return;
            var all = FindObjectsByType<InteractableDoor>(FindObjectsInactive.Include);
            foreach (var t in config.secretDoors)
            {
                if (t == null || string.IsNullOrEmpty(t.doorName)) continue;
                InteractableDoor found = null;
                foreach (var d in all)
                {
                    if (d.name != t.doorName) continue;
                    if (!string.IsNullOrEmpty(config.doorParentFilter) && !HasParentNamed(d.transform, config.doorParentFilter)) continue;
                    found = d;
                    break;
                }
                if (found == null) { Debug.LogWarning("[GIM-007] 비밀문 '" + t.doorName + "'을(를) 씬에서 찾지 못했습니다.", this); continue; }
                doors.Add(new DoorBinding { door = found, target = t });
            }
        }

        static bool HasParentNamed(Transform t, string part)
        {
            for (var p = t.parent; p != null; p = p.parent) if (p.name.Contains(part)) return true;
            return false;
        }

        // 완료 전: 닫힌 채 잠금. 완료 후: 열고, (설정 시) 열린 채로 잠가서 닫을 수 없게.
        // 각 컴퓨터가 같은 상태를 보고 똑같이 맞추므로 문 동기화(DoorNetworkSync)를 거치지 않아도 모두에게 같게 보인다.
        void ApplyDoors()
        {
            bool complete = IsComplete;
            foreach (var b in doors)
            {
                var d = b.door;
                if (d == null) continue;
                if (!complete)
                {
                    SetLocked(d, true, config.msgDoorLocked);
                    continue;
                }
                if (!d.IsOpen) OpenLeaf(d, b.target.openFrom);
                if (d.partner != null && !d.partner.IsOpen) OpenLeaf(d.partner, b.target.openFrom);
                if (config.keepDoorOpenAfterComplete) SetLocked(d, true, config.msgDoorHeldOpen);
                else if (d.locked) d.Unlock();
            }
        }

        static void OpenLeaf(InteractableDoor d, Vector3 from)
        {
            d.ApplyState(true, d.ComputeAngle(true, from));
        }

        static void SetLocked(InteractableDoor d, bool locked, string message)
        {
            if (!locked) { if (d.locked) d.Unlock(); return; }
            d.lockedMessage = message;
            if (d.partner != null) d.partner.lockedMessage = message;
            if (!d.locked || (d.partner != null && !d.partner.locked)) d.Lock();
        }

        // ---------- 혼자 테스트: 가짜 동료 ----------

        void TickFakePartner()
        {
            if (net != localNet) return;   // 네트워크 게임 중에는 절대 끼어들지 않는다
            var s = State;
            int mine = s.HandleOf(LocalPlayerId);
            int fakeHandle = s.HandleOf(FakePartnerId);
            if (mine >= 0 && fakeHandle < 0 && !s.IsComplete) { localNet.GripAs(FakePartnerId, 1 - mine); return; }
            if (mine < 0 && fakeHandle >= 0) { localNet.ReleaseAs(FakePartnerId); return; }
            if (s.state != Gim007State.Round || fakeHandle < 0) return;
            if (s.round != fakePartnerRound) { fakePartnerRound = s.round; fakePartnerAt = Time.time + debugPartnerDelay; }
            if (fakePartnerAt > 0f && Time.time >= fakePartnerAt)
            {
                fakePartnerAt = -1f;
                localNet.ReportAs(FakePartnerId, s.round, UnityEngine.Random.value < debugPartnerSuccessRate);
            }
        }

        // ---------- 화면 스크립트용 도우미 ----------

        // 이 컴퓨터 플레이어의 몸통(CharacterController가 붙은 쪽). 없으면 null
        public static Transform LocalPlayer()
        {
            var pi = PlayerInteractor.Active;
            if (pi == null) return null;
            var cc = pi.GetComponentInParent<CharacterController>();
            return cc != null ? cc.transform : pi.transform;
        }

        void Log(string msg) { if (logToConsole) Debug.Log("[GIM-007] " + msg, this); }
    }
}
