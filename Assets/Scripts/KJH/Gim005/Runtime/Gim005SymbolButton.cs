using UnityEngine;

namespace Gim005
{
    // 2층 정원 관리실 문양 버튼 하나 (OBJ_SYMBOL_BUTTON, 13개). 바라보고 E → 판정 담당에게 입력을 보낸다.
    //  - 불빛(lamp): 이번 시도에 이 문양으로 덤불을 열었으면 켜진다.
    //  - 오답 후 패널 잠금 중이면 누를 수 없다 (안내 문구만).
    //  - 판정(맞음/오답/이미 연 문양)은 판정 담당(Gim005Logic)이 한다. 여기서는 눌림 연출과 요청만.
    public class Gim005SymbolButton : MonoBehaviour, IWorldInteractable
    {
        [Tooltip("문양 번호 (0 = SYM_01 … 12 = SYM_13)")]
        [SerializeField] int symbol;
        [SerializeField] Gim005SymbolDisplay display;
        [Tooltip("불빛 (머티리얼에 Emission이 켜져 있어야 함)")]
        [SerializeField] Renderer lamp;
        [Tooltip("눌릴 때 들어가는 부분 (비우면 이 오브젝트)")]
        [SerializeField] Transform cap;
        [SerializeField] float pressDepth = 0.02f;
        [SerializeField] Color lampOnColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] Color lampLockedColor = new Color(0.6f, 0.05f, 0.05f);

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock block;
        Vector3 capRest;
        float pressedUntil;
        int lampState = -1;

        public int Symbol { get { return symbol; } }

        void Awake()
        {
            if (cap == null) cap = transform;
            capRest = cap.localPosition;
        }

        void Update()
        {
            var c = Gim005Controller.Instance;
            if (c == null) return;
            if (display != null) display.Show(symbol, c.Config != null ? c.Config.symbols : null);

            cap.localPosition = Time.time < pressedUntil ? capRest + cap.localRotation * Vector3.forward * pressDepth : capRest;

            // 0 = 꺼짐, 1 = 켜짐(연 문양), 2 = 잠김
            int st = c.State.IsSymbolOpen(symbol) && c.State.state == Gim005State.Running ? 1 : c.LockRemaining > 0f ? 2 : 0;
            if (c.IsComplete) st = 1;
            if (st != lampState) { lampState = st; SetLamp(st); }
        }

        void SetLamp(int st)
        {
            if (lamp == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            lamp.GetPropertyBlock(block);
            Color col = st == 1 ? lampOnColor : st == 2 ? lampLockedColor : new Color(0.15f, 0.15f, 0.15f);
            block.SetColor(BaseColor, col);
            block.SetColor(EmissionColor, st == 0 ? Color.black : col * (st == 1 ? 2.5f : 1.2f));
            lamp.SetPropertyBlock(block);
        }

        public bool CanInteract
        {
            get { var c = Gim005Controller.Instance; return c != null && !c.IsComplete; }
        }

        public string InteractPrompt
        {
            get
            {
                var c = Gim005Controller.Instance;
                if (c == null) return null;
                if (c.LockRemaining > 0f) return c.Config.msgPanelLocked + " (" + Mathf.CeilToInt(c.LockRemaining) + ")";
                return "[E] 문양 버튼 누르기";
            }
        }

        public void Interact(GameObject interactor)
        {
            var c = Gim005Controller.Instance;
            if (c == null || c.Net == null) return;
            var s = c.State;
            if (s.state != Gim005State.Running) { PlayerHand.Say(c.Config.msgPanelIdle); return; }
            if (c.LockRemaining > 0f) { PlayerHand.Say(c.Config.msgPanelLocked); return; }

            pressedUntil = Time.time + 0.15f;
            var panel = GetComponentInParent<Gim005Panel>();
            if (panel != null) panel.PlayClick();

            if (s.IsSymbolOpen(symbol)) { PlayerHand.Say(c.Config.msgAlreadyOpen); return; }
            c.Net.RequestPress(symbol);
        }
    }
}
