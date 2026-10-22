using System;
using System.Collections.Generic;
using LGH;
using UnityEngine;
using UnityEngine.Events;

namespace Gim004
{
    // GIM-004의 중심. GIM004_Conservatory 프리팹 안에 하나 들어 있다 (씬에 하나만).
    // 하는 일:
    //  1) 어떤 네트워크 어댑터(IGim004Net)를 쓸지 정한다. 기본은 LocalGim004Net, 네트워크 게임이 시작돼 Gim004NetSync가 스폰되면 그쪽으로 바꾼다.
    //  2) 받은 상태(State)와 사건(EventReceived)을 화면 스크립트들(선반 자리, 화분, 소리)에게 나눠 준다.
    //  3) 정원문·정원 관리실 문을 "이름으로 찾아" 완료 전에는 잠그고, 완료되면 연다.
    //     → LGH 맵 프리팹(Ravenwood_LGH)을 고치거나 컴포넌트를 붙이지 않는다. 잠금은 상태 기반이라 늦게 들어온 사람에게도 같게 적용된다.
    //  4) 완료 시 층별 안내 문구: 1층 "정원문이 열렸습니다.", 2층 "정원 관리실 문이 열렸습니다."
    [DefaultExecutionOrder(-50)]
    public class Gim004Controller : MonoBehaviour
    {
        public static Gim004Controller Instance { get; private set; }

        // 완료 시 모든 컴퓨터에서 한 번 (다른 기믹 연결용)
        public static event Action Completed;

        [Tooltip("판정 규칙·기록일지·문·문구가 담긴 설정 에셋")]
        [SerializeField] Gim004Config config;

        [Tooltip("상태 변화와 사건을 Console에 출력")]
        [SerializeField] bool logToConsole = true;

        [Tooltip("GIM-004 완료 시 판정 담당(호스트, 혼자면 로컬)에서 한 번 실행")]
        public UnityEvent onCompleteAuthority;

        public Gim004Config Config { get { return config; } }
        public IGim004Net Net { get { return net; } }

        public Gim004SyncState State { get; private set; } = Gim004SyncState.Empty;
        public Gim004SyncState Previous { get; private set; } = Gim004SyncState.Empty;

        public event Action<Gim004SyncState, Gim004SyncState> StateChanged;   // (이전, 새)
        public event Action<Gim004NetEvent, int> EventReceived;

        public bool IsComplete { get { return State.state == Gim004State.Complete; } }
        public bool ShelfEditable { get { return State.state == Gim004State.Placing; } }

        LocalGim004Net localNet;
        IGim004Net net;

        struct DoorBinding { public InteractableDoor door; public Gim004DoorTarget target; }
        readonly List<DoorBinding> completeDoors = new List<DoorBinding>();
        readonly List<DoorBinding> entryDoors = new List<DoorBinding>();

        void Awake()
        {
            Instance = this;
            if (config == null)
            {
                Debug.LogError("[GIM-004] Gim004Controller에 Config가 연결되지 않았습니다. Tools/KJH/GIM-004/Build All을 실행하거나 직접 연결하세요.", this);
                enabled = false;
                return;
            }
            string err = config.rules.Validate();
            if (err != null) Debug.LogError("[GIM-004] Config 정답 설정 오류: " + err, this);

            localNet = new LocalGim004Net(config);
            UseNet(localNet);
        }

        void Start()
        {
            ResolveDoors(config.completeDoors, completeDoors);
            ResolveDoors(config.entryDoors, entryDoors);
            ApplyDoorLocks();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Bind(net, false);
        }

        void Update()
        {
            if (net != null) net.Tick(Time.deltaTime);
            // 방 초기화(RoomResetter) 등 다른 스크립트가 문 잠금을 되돌려도 상태에 맞게 다시 맞춘다 (문 몇 개라 매 프레임 확인해도 가볍다)
            ApplyDoorLocks();
        }

        // ---------- 어댑터 교체 ----------

        public void UseNet(IGim004Net n)
        {
            if (n == null || n == net) return;
            Bind(net, false);
            net = n;
            Bind(net, true);
            Log("어댑터 사용: " + n.GetType().Name + (n.IsAuthority ? " (판정 담당)" : ""));
            ApplyState(net.Current);
        }

        public void ReleaseNet(IGim004Net n)
        {
            if (n != net) return;
            if (localNet != null) UseNet(localNet);
        }

        void Bind(IGim004Net n, bool on)
        {
            if (n == null) return;
            if (on) { n.OnStateSynced += ApplyState; n.OnEvent += ApplyEvent; }
            else { n.OnStateSynced -= ApplyState; n.OnEvent -= ApplyEvent; }
        }

        void ApplyState(Gim004SyncState s)
        {
            Previous = State;
            State = s;
            if (!Previous.Equals(s)) Log("상태 → " + s);
            ApplyDoorLocks();
            StateChanged?.Invoke(Previous, s);
        }

        void ApplyEvent(Gim004NetEvent e, int value)
        {
            Log("사건: " + e + " (" + value + ")");
            EventReceived?.Invoke(e, value);
            if (e == Gim004NetEvent.Complete)
            {
                ApplyDoorLocks();
                PlayerHand.Say(LocalOnFloor1 ? config.msgCompleteFloor1 : config.msgCompleteFloor2, 4f);
                Completed?.Invoke();
                if (net.IsAuthority)
                {
                    OpenCompleteDoors();
                    onCompleteAuthority?.Invoke();
                }
            }
        }

        // ---------- 문 ----------

        void ResolveDoors(Gim004DoorTarget[] targets, List<DoorBinding> into)
        {
            into.Clear();
            if (targets == null) return;
            var all = FindObjectsByType<InteractableDoor>(FindObjectsInactive.Include);
            foreach (var t in targets)
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
                if (found == null) { Debug.LogWarning("[GIM-004] 문 '" + t.doorName + "'을(를) 씬에서 찾지 못했습니다.", this); continue; }
                into.Add(new DoorBinding { door = found, target = t });
            }
        }

        static bool HasParentNamed(Transform t, string part)
        {
            for (var p = t.parent; p != null; p = p.parent) if (p.name.Contains(part)) return true;
            return false;
        }

        bool EntryUnlocked
        {
            get
            {
                if (!config.lockEntryUntilPotionCraft) return true;
                var potion = PotionCraft.PotionCraftSync.Instance;
                return potion == null || potion.Completed;   // 약품 제조 퍼즐이 씬에 없으면 잠그지 않는다
            }
        }

        void ApplyDoorLocks()
        {
            bool complete = IsComplete;
            foreach (var b in completeDoors) SetLock(b, !complete);
            if (config.lockEntryUntilPotionCraft) foreach (var b in entryDoors) SetLock(b, !EntryUnlocked);
        }

        static void SetLock(DoorBinding b, bool locked)
        {
            if (b.door == null) return;
            if (locked)
            {
                b.door.lockedMessage = b.target.lockedMessage;
                if (b.door.partner != null) b.door.partner.lockedMessage = b.target.lockedMessage;
                if (!b.door.locked) b.door.Lock();
            }
            else if (b.door.locked) b.door.Unlock();
        }

        // 판정 담당에서만: 완료 문을 연다. 네트워크 게임이면 LGH 문 동기화(DoorNetworkSync)에 요청 → 모두에게 같은 각도로 열린다.
        void OpenCompleteDoors()
        {
            var sync = DoorNetworkSync.Instance;
            foreach (var b in completeDoors)
            {
                if (b.door == null || b.door.IsOpen) continue;
                if (sync != null && sync.IsSpawned) sync.RequestInteract(b.door, b.target.openFrom);
                else
                {
                    b.door.SetOpen(true, b.target.openFrom);
                    if (b.door.partner != null) b.door.partner.SetOpen(true, b.target.openFrom);
                }
                Log("완료: " + b.door.name + " 열기");
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

        // 이 컴퓨터 플레이어가 1층에 있는가 (플레이어가 없으면 1층으로 본다 — 에디터 단독 테스트용)
        public bool LocalOnFloor1
        {
            get
            {
                var p = LocalPlayer();
                return p == null || p.position.y < config.floorSplitY;
            }
        }

        void Log(string msg) { if (logToConsole) Debug.Log("[GIM-004] " + msg, this); }
    }
}
