using UnityEngine;

namespace Gim002
{
    // 표시등. 하나의 스크립트로 세 종류를 처리한다 (모양이 다른 프리팹 두 개 — 준비 표시등=커튼 모양, 진행 표시등=둥근 램프 — 가 같은 스크립트를 쓴다).
    //  - StageReady  : 1층 피아노 위 "무대" 표시등. 2층이 태엽 열쇠를 쓰면(stageWound) 켜짐. 무대 오답 때 빨갛게 깜빡임.
    //  - PlayerReady : 2층 무대 위 "반주자" 표시등. 1층 음악실 문이 열리면(musicDoorOpened) 켜짐.
    //  - Progress    : 진행 표시등 n번째. 성공한 장면 수(progressCount)가 n 이상이면 켜짐.
    // 색은 MaterialPropertyBlock으로 바꿔서 머티리얼 에셋을 복제하지 않는다(URP Lit의 _BaseColor/_EmissionColor 사용).
    public class Gim002IndicatorLamp : MonoBehaviour
    {
        public enum LampKind { StageReady, PlayerReady, Progress }

        [SerializeField] LampKind kind;
        [Tooltip("Progress일 때 몇 번째 램프인지 (1~3)")]
        [SerializeField] int progressIndex = 1;
        [Tooltip("색을 바꿀 전구 렌더러 (비우면 자식에서 첫 렌더러)")]
        [SerializeField] Renderer bulb;
        [SerializeField] Color offColor = new Color(0.15f, 0.13f, 0.1f);
        [SerializeField] Color onColor = new Color(1f, 0.8f, 0.35f);
        [SerializeField] Color warnColor = new Color(1f, 0.1f, 0.05f);
        [Tooltip("켜졌을 때 빛나는 세기 (Emission)")]
        [SerializeField] float glow = 2.5f;
        [Tooltip("무대 오답(EV_STAGE_WRONG) 때 빨갛게 깜빡일지 (1층 '무대' 표시등만 켬)")]
        [SerializeField] bool blinkOnStageWrong;
        [Tooltip("깜빡이는 횟수")]
        [SerializeField] int blinkCount = 3;

        MaterialPropertyBlock block;
        float blinkUntil, blinkStart;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            if (bulb == null) bulb = GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            if (Gim002Controller.Instance != null) Gim002Controller.Instance.EventReceived += OnEvent;
        }

        void Start()
        {
            // Controller가 이 램프보다 늦게 켜질 수 있어서 Start에서 한 번 더 연결
            var c = Gim002Controller.Instance;
            if (c != null) { c.EventReceived -= OnEvent; c.EventReceived += OnEvent; }
        }

        void OnDisable()
        {
            if (Gim002Controller.Instance != null) Gim002Controller.Instance.EventReceived -= OnEvent;
        }

        void OnEvent(Gim002NetEvent e, int value)
        {
            if (e != Gim002NetEvent.StageWrong || !blinkOnStageWrong) return;
            var t = Gim002Controller.Instance.Timing;
            blinkStart = Time.time;
            blinkUntil = Time.time + t.stageWrongFlashSeconds + t.returnHomeSeconds;
        }

        bool IsOn(Gim002SyncState s)
        {
            switch (kind)
            {
                case LampKind.StageReady: return s.stageWound;
                case LampKind.PlayerReady: return s.musicDoorOpened;
                default: return s.progressCount >= progressIndex;
            }
        }

        void Update()
        {
            var c = Gim002Controller.Instance;
            if (c == null || bulb == null) return;

            Color col = IsOn(c.State) ? onColor : offColor;
            if (Time.time < blinkUntil)
            {
                // 깜빡임 구간을 blinkCount번으로 나눠 앞 절반은 빨강
                float dur = Mathf.Max(0.01f, blinkUntil - blinkStart);
                float phase = (Time.time - blinkStart) / dur * blinkCount;
                if (phase - Mathf.Floor(phase) < 0.5f) col = warnColor;
            }

            bool lit = col != offColor;
            bulb.GetPropertyBlock(block);
            block.SetColor(BaseColorId, col);
            block.SetColor(ColorId, col);
            block.SetColor(EmissionId, lit ? col * glow : Color.black);
            bulb.SetPropertyBlock(block);
        }
    }
}
