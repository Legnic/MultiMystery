using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Gim005
{
    // GIM-005의 중심. GIM005_Maze 프리팹 안에 하나 들어 있다 (씬에 하나만).
    // 하는 일:
    //  1) 어떤 네트워크 어댑터(IGim005Net)를 쓸지 정한다. 기본은 LocalGim005Net, 네트워크 게임이 시작돼 Gim005NetSync가 스폰되면 그쪽으로 바꾼다.
    //  2) 받은 상태(State)와 사건(EventReceived)을 화면 스크립트들(덤불, 버튼, 시계…)에게 나눠 준다.
    //  3) 완료 상태가 되면 Config에 적힌 대상(1/2층 철창 Rail_StairFront)을 모든 컴퓨터에서 비활성화한다.
    //     → 상태 기반이라 나중에 들어온 플레이어에게도 그대로 적용되고, 이름으로 찾기 때문에 KJH/Develop 어느 씬에서도 따로 연결할 필요가 없다.
    [DefaultExecutionOrder(-50)]
    public class Gim005Controller : MonoBehaviour
    {
        public static Gim005Controller Instance { get; private set; }

        // 완료 시 모든 컴퓨터에서 한 번 (다른 기믹 연결용)
        public static event Action Completed;

        [Tooltip("수치·문양·문구가 담긴 설정 에셋")]
        [SerializeField] Gim005Config config;

        [Tooltip("상태 변화와 사건을 Console에 출력")]
        [SerializeField] bool logToConsole = true;

        [Tooltip("완료 시 비활성화할 오브젝트를 직접 지정 (Config의 이름 목록과 함께 쓰인다)")]
        [SerializeField] GameObject[] hideOnComplete = new GameObject[0];

        [Tooltip("GIM-005 완료 시 판정 담당(호스트, 혼자면 로컬)에서 한 번 실행")]
        public UnityEvent onCompleteAuthority;

        [Header("임시 글자 (숫자 명판)")]
        [Tooltip("명판·패널 글자가 쓰는 머티리얼 (깊이 검사 셰이더). 글꼴 텍스처는 실행 중에 만들어지므로 여기서 연결한다")]
        [SerializeField] Material textMaterial;
        [SerializeField] Font textFont;

        public Gim005Config Config { get { return config; } }
        public IGim005Net Net { get { return net; } }
        public Gim005Rules Rules { get { return config != null ? config.rules : new Gim005Rules(); } }

        public Gim005SyncState State { get; private set; }
        public Gim005SyncState Previous { get; private set; }

        public event Action<Gim005SyncState, Gim005SyncState> StateChanged;   // (이전, 새)
        public event Action<Gim005NetEvent, int> EventReceived;

        LocalGim005Net localNet;
        IGim005Net net;
        float appliedAt;
        readonly List<GameObject> hidden = new List<GameObject>();

        // 남은 시간 / 패널 잠금: 마지막으로 받은 값에서 받은 뒤 흐른 시간을 빼서 매 프레임 계산 (시간은 매 프레임 동기화하지 않는다)
        public float Remaining
        {
            get { return State.state == Gim005State.Running ? Mathf.Max(0f, State.remaining - (Time.time - appliedAt)) : State.remaining; }
        }

        public float LockRemaining
        {
            get { return State.state == Gim005State.Running ? Mathf.Max(0f, State.lockRemaining - (Time.time - appliedAt)) : 0f; }
        }

        public bool IsComplete { get { return State.state == Gim005State.Complete; } }

        void Awake()
        {
            Instance = this;
            if (config == null)
            {
                Debug.LogError("[GIM-005] Gim005Controller에 Config가 연결되지 않았습니다. Tools/KJH/GIM-005/Build All을 실행하거나 직접 연결하세요.", this);
                enabled = false;
                return;
            }
            localNet = new LocalGim005Net(config);
            UseNet(localNet);

            if (textMaterial != null && textFont != null)
            {
                // 에셋을 직접 바꾸지 않도록 실행 중 복사본을 만들어 GIM-005 글자들에 끼운다
                var source = textMaterial;
                textMaterial = new Material(source) { name = source.name + " (Runtime)" };
                textMaterial.mainTexture = textFont.material.mainTexture;
                foreach (var tm in FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var r = tm.GetComponent<MeshRenderer>();
                    if (r != null && r.sharedMaterial == source) r.sharedMaterial = textMaterial;
                }
                Font.textureRebuilt += OnFontRebuilt;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Bind(net, false);
            Font.textureRebuilt -= OnFontRebuilt;
        }

        // 글꼴에 새 글자가 추가되면 텍스처가 다시 만들어진다 → 글자 머티리얼에도 다시 연결
        void OnFontRebuilt(Font f)
        {
            if (f == textFont && textMaterial != null) textMaterial.mainTexture = f.material.mainTexture;
        }

        void Update()
        {
            if (net != null) net.Tick(Time.deltaTime);
        }

        // ---------- 어댑터 교체 ----------

        public void UseNet(IGim005Net n)
        {
            if (n == null || n == net) return;
            Bind(net, false);
            net = n;
            Bind(net, true);
            Log("어댑터 사용: " + n.GetType().Name + (n.IsAuthority ? " (판정 담당)" : ""));
            ApplyState(net.Current);
        }

        public void ReleaseNet(IGim005Net n)
        {
            if (n != net) return;
            if (localNet != null) UseNet(localNet);
        }

        void Bind(IGim005Net n, bool on)
        {
            if (n == null) return;
            if (on) { n.OnStateSynced += ApplyState; n.OnEvent += ApplyEvent; }
            else { n.OnStateSynced -= ApplyState; n.OnEvent -= ApplyEvent; }
        }

        void ApplyState(Gim005SyncState s)
        {
            Previous = State;
            State = s;
            appliedAt = Time.time;
            if (Previous.state != s.state || Previous.attempt != s.attempt) Log("상태 → " + s);
            ApplyCompleteTargets(s.state == Gim005State.Complete);
            StateChanged?.Invoke(Previous, s);
        }

        void ApplyEvent(Gim005NetEvent e, int value)
        {
            Log("사건: " + e + " (" + value + ")");
            EventReceived?.Invoke(e, value);
            if (e == Gim005NetEvent.Complete)
            {
                PlayerHand.Say(config.msgComplete, 5f);
                Completed?.Invoke();
                if (net.IsAuthority) onCompleteAuthority?.Invoke();
            }
        }

        // ---------- 완료 시 철창 열기 ----------

        void ApplyCompleteTargets(bool complete)
        {
            if (complete)
            {
                foreach (var go in FindCompleteTargets())
                {
                    if (go == null || !go.activeSelf) continue;
                    go.SetActive(false);
                    hidden.Add(go);
                    Log("완료: " + go.name + " 비활성화");
                }
            }
            else if (hidden.Count > 0)
            {
                // 네트워크가 끊겨 로컬 상태로 돌아간 경우 등: 우리가 숨긴 것만 되돌린다
                foreach (var go in hidden) if (go != null) go.SetActive(true);
                hidden.Clear();
            }
        }

        List<GameObject> FindCompleteTargets()
        {
            var list = new List<GameObject>();
            foreach (var go in hideOnComplete) if (go != null) list.Add(go);
            var names = config.hideOnCompleteNames;
            if (names == null || names.Length == 0) return list;

            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (Array.IndexOf(names, t.name) < 0) continue;
                if (!string.IsNullOrEmpty(config.hideOnCompleteParentFilter) && !HasParentNamed(t, config.hideOnCompleteParentFilter)) continue;
                if (!list.Contains(t.gameObject)) list.Add(t.gameObject);
            }
            if (list.Count == 0) Debug.LogWarning("[GIM-005] 완료 시 열 철창(" + string.Join(", ", names) + ")을 씬에서 찾지 못했습니다.", this);
            return list;
        }

        static bool HasParentNamed(Transform t, string part)
        {
            for (var p = t.parent; p != null; p = p.parent) if (p.name.Contains(part)) return true;
            return false;
        }

        // ---------- 화면 스크립트용 도우미 ----------

        // 이 컴퓨터 플레이어의 몸통(CharacterController가 붙은 쪽). 없으면 null
        public static Transform LocalPlayer()
        {
            var pi = LGH.PlayerInteractor.Active;
            if (pi == null) return null;
            var cc = pi.GetComponentInParent<CharacterController>();
            return cc != null ? cc.transform : pi.transform;
        }

        void Log(string msg) { if (logToConsole) Debug.Log("[GIM-005] " + msg, this); }
    }
}
