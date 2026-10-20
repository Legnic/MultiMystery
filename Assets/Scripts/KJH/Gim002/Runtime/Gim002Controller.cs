using System;
using UnityEngine;
using UnityEngine.Events;

namespace Gim002
{
    // GIM-002의 중심. 씬에 하나만 둔다 (Tools/KJH/GIM-002/Build All이 "GIM002/Gim002Controller"로 만든다).
    // 하는 일:
    //  1) 어떤 네트워크 어댑터(IGim002Net)를 쓸지 정한다. 기본은 LocalGim002Net이고,
    //     네트워크 게임이 시작돼 Gim002NetSync가 스폰되면 그쪽으로 바꾼다(UseNet).
    //  2) GIM-001(초상화 퍼즐) 해결 신호(PortraitPuzzle.SolvedEvent)를 받아 GIM-002를 LOCKED → PREPARING으로 연다.
    //  3) 받은 상태(State)와 사건(EventReceived)을 화면 스크립트들(커튼, 슬롯, 표시등...)에게 나눠 준다.
    // 화면 스크립트들은 모두 Gim002Controller.Instance를 통해 상태를 읽고 요청을 보낸다.
    [DefaultExecutionOrder(-50)]
    public class Gim002Controller : MonoBehaviour
    {
        public static Gim002Controller Instance { get; private set; }

        [Tooltip("장면 데이터·시간 수치·문구가 담긴 설정 에셋")]
        [SerializeField] Gim002Config config;

        [Tooltip("상태 변화와 사건을 Console에 출력 (아트·사운드가 없을 때 확인용)")]
        [SerializeField] bool logToConsole = true;

        [Tooltip("GIM-002 완료 시 판정 담당(호스트, 혼자면 로컬)에서 한 번 실행. GIM-003 연결(문 열기 등)은 나중에 여기에 붙인다.")]
        public UnityEvent onCompleteAuthority;

        public Gim002Config Config { get { return config; } }
        public IGim002Net Net { get { return net; } }

        // 지금 상태와 바로 전 상태 (화면 스크립트가 "무엇이 바뀌었나"를 비교할 때 사용)
        public Gim002SyncState State { get; private set; }
        public Gim002SyncState Previous { get; private set; }
        // 지금 상태(state 값)가 시작된 시각 (Time.time). 커튼이 다 열렸는지 등을 화면 쪽에서 판단할 때 쓴다.
        public float StateSince { get; private set; }

        // (이전 상태, 새 상태)
        public event Action<Gim002SyncState, Gim002SyncState> StateChanged;
        // (사건 종류, 값) — 값: KeyPressed는 SymbolId, SceneClear는 장면 번호
        public event Action<Gim002NetEvent, int> EventReceived;

        LocalGim002Net localNet;
        IGim002Net net;

        public Gim002Timing Timing { get { return config != null ? config.timing : new Gim002Timing(); } }
        public Gim002SceneData CurrentScene { get { return config != null ? config.GetScene(State.sceneIndex) : null; } }

        void Awake()
        {
            Instance = this;
            if (config == null)
            {
                Debug.LogError("[GIM-002] Gim002Controller에 Config가 연결되지 않았습니다. Tools/KJH/GIM-002/Build All을 실행하거나 직접 연결하세요.", this);
                enabled = false;
                return;
            }
            localNet = new LocalGim002Net(config);
            UseNet(localNet);
        }

        void OnEnable() { PortraitPuzzle.SolvedEvent += OnGim001Solved; }
        void OnDisable() { PortraitPuzzle.SolvedEvent -= OnGim001Solved; }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Bind(net, false);
        }

        void Start()
        {
            // 이미 풀린 상태에서 시작한 경우(테스트 등)도 놓치지 않게 한 번 확인
            if (PortraitPuzzle.Instance != null && PortraitPuzzle.Instance.Solved) OnGim001Solved();
        }

        void Update()
        {
            if (net != null) net.Tick(Time.deltaTime);
        }

        // GIM-001이 풀리면(모든 컴퓨터에서 불림) 판정 담당만 로직에 알린다
        void OnGim001Solved()
        {
            if (net != null && net.IsAuthority) net.RequestGim001Solved();
        }

        // ---------- 어댑터 교체 ----------

        // 네트워크 어댑터가 준비되면 호출 (Gim002NetSync.OnNetworkSpawn)
        public void UseNet(IGim002Net n)
        {
            if (n == null || n == net) return;
            Bind(net, false);
            net = n;
            Bind(net, true);
            Log("어댑터 사용: " + n.GetType().Name + (n.IsAuthority ? " (판정 담당)" : ""));
            ApplyState(net.Current);
            // 판정 담당이 바뀌었는데 GIM-001이 이미 풀려 있으면 다시 알린다
            if (net.IsAuthority && PortraitPuzzle.Instance != null && PortraitPuzzle.Instance.Solved) net.RequestGim001Solved();
        }

        // 네트워크 어댑터가 사라지면 호출 (접속 종료) → 로컬 어댑터로 되돌린다
        public void ReleaseNet(IGim002Net n)
        {
            if (n != net) return;
            if (localNet != null) UseNet(localNet);
        }

        void Bind(IGim002Net n, bool on)
        {
            if (n == null) return;
            if (on) { n.OnStateSynced += ApplyState; n.OnEvent += ApplyEvent; }
            else { n.OnStateSynced -= ApplyState; n.OnEvent -= ApplyEvent; }
        }

        void ApplyState(Gim002SyncState s)
        {
            Previous = State;
            State = s;
            if (Previous.state != s.state) { StateSince = Time.time; Log("상태 → " + s); }
            StateChanged?.Invoke(Previous, s);
        }

        void ApplyEvent(Gim002NetEvent e, int value)
        {
            Log("사건: " + e + " (" + value + ")");
            EventReceived?.Invoke(e, value);
            if (e == Gim002NetEvent.Complete)
            {
                PlayerHand.Say(config.msgComplete, 5f);
                if (net.IsAuthority) onCompleteAuthority?.Invoke();
            }
        }

        void Log(string msg) { if (logToConsole) Debug.Log("[GIM-002] " + msg, this); }

        // ---------- 화면 스크립트용 도우미 ----------

        // 슬롯이 판정을 기다리는 중인지 (두 칸이 다 찼는데 아직 Staging)
        public bool StageJudging { get { return State.state == Gim002State.Staging && State.slotDoll >= 0 && State.slotBg >= 0; } }

        // 2층 무대에 꽂기/빼기를 할 수 있는 상태인가 (커튼이 열린 Staging, 판정 전)
        // 커튼이 열리는 동안(curtainSeconds)은 판정 담당이 요청을 무시하므로 화면 쪽에서도 미리 막는다.
        public bool StageEditable
        {
            get
            {
                return State.state == Gim002State.Staging && State.curtainOpen && !StageJudging
                    && Time.time - StateSince >= Timing.curtainSeconds;
            }
        }
    }
}
