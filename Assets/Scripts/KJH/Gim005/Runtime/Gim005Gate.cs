using UnityEngine;

namespace Gim005
{
    // 문양 덤불 하나 (OBJ_GATE_BUSH, G1~G8). 직접 조작할 수는 없고, 2층 버튼에 따라 사라지거나 다시 자란다.
    //  - 앞뒤 두 명판(Gim005SymbolDisplay)에 이번 시도에 배정된 문양을 보여 준다 (막다른 길에서 돌아올 때도 보이도록 양면).
    //  - 열리면 덤불(body)이 땅속으로 가라앉고 콜라이더가 꺼진다. 다시 막히면 솟아오른다.
    //  - 어느 덤불이 G 몇 번인지는 gateIndex로 정한다 (0 = G1). 위치를 옮겨도 번호만 맞으면 된다.
    public class Gim005Gate : MonoBehaviour
    {
        [Tooltip("0 = G1, 1 = G2 … 7 = G8 (미로 구조도 기준)")]
        [SerializeField] int gateIndex;
        [Tooltip("가라앉고 솟아오르는 부분 (덤불 + 명판)")]
        [SerializeField] Transform body;
        [Tooltip("양면 명판")]
        [SerializeField] Gim005SymbolDisplay[] faces = new Gim005SymbolDisplay[0];
        [Tooltip("통행을 막는 콜라이더 (열리면 꺼짐)")]
        [SerializeField] Collider[] blockers = new Collider[0];
        [Tooltip("가라앉는 깊이 (m). 덤불 높이보다 조금 크게")]
        [SerializeField] float sinkDepth = 3.4f;
        [Tooltip("사라지는/자라는 데 걸리는 시간 (초)")]
        [SerializeField] float moveSeconds = 0.9f;
        [Tooltip("열릴 때 소리 (비우면 임시 소리)")]
        [SerializeField] AudioClip openSound;

        public int GateIndex { get { return gateIndex; } }
        public bool IsOpen { get; private set; }

        Vector3 restPos;
        float k;            // 0 = 막힘, 1 = 완전히 가라앉음
        AudioSource source;

        void Awake()
        {
            if (body == null) body = transform;
            restPos = body.localPosition;
            source = GetComponent<AudioSource>();
        }

        void Start()
        {
            var c = Gim005Controller.Instance;
            if (c == null) return;
            c.StateChanged += OnStateChanged;
            // 처음에는 연출 없이 바로 맞춘다
            Refresh(c.State, true);
        }

        void OnDestroy()
        {
            var c = Gim005Controller.Instance;
            if (c != null) c.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(Gim005SyncState prev, Gim005SyncState now) { Refresh(now, false); }

        void Refresh(Gim005SyncState s, bool instant)
        {
            var c = Gim005Controller.Instance;
            var data = c != null && c.Config != null ? c.Config.symbols : null;
            int symbol = gateIndex < s.gateCount ? s.SymbolOfGate(gateIndex) : -1;
            foreach (var f in faces) if (f != null) f.Show(symbol, data);

            bool open = s.IsGateOpen(gateIndex);
            if (open && !IsOpen && !instant && source != null) source.PlayOneShot(openSound != null ? openSound : Gim002.Gim002Tones.Wind());
            IsOpen = open;
            foreach (var col in blockers) if (col != null) col.enabled = !open;
            if (instant) { k = open ? 1f : 0f; Apply(); }
        }

        void Update()
        {
            float target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(k, target)) return;
            k = Mathf.MoveTowards(k, target, Time.deltaTime / Mathf.Max(0.01f, moveSeconds));
            Apply();
        }

        void Apply()
        {
            float e = k * k * (3f - 2f * k);   // 부드럽게 시작·끝
            body.localPosition = restPos + Vector3.down * sinkDepth * e;
            // 완전히 가라앉으면 렌더링도 끈다 (바닥 아래에서 깜빡이지 않게)
            // (body가 이 오브젝트 자신이면 Update가 멈추므로 끄지 않는다)
            bool visible = k < 0.999f;
            if (body != transform && body.gameObject.activeSelf != visible) body.gameObject.SetActive(visible);
        }
    }
}
