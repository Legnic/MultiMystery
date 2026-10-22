using UnityEngine;

namespace Gim005
{
    // 2층 정원 관리실의 문양 버튼 패널 (OBJ_SYMBOL_PANEL) + 남은 시간 표시 (OBJ_TIMER_DISPLAY).
    //  - 남은 시간은 이 패널에만 표시한다 (기획 🟡 제안: B 화면에만). A에게는 30초 전 종소리(Gim005MazeArea)로 알린다.
    //  - 오답이면 경고음 + 상태 표시등이 빨갛게 + 근처(radius) 플레이어에게 안내 문구.
    //  - 버튼(Gim005SymbolButton)들은 이 오브젝트의 자식으로 둔다.
    public class Gim005Panel : MonoBehaviour
    {
        [Tooltip("남은 시간 글자 (비우면 표시 안 함)")]
        [SerializeField] TextMesh timerText;
        [Tooltip("상태 글자 (대기 / 진행 / 잠김 / 완료)")]
        [SerializeField] TextMesh statusText;
        [Tooltip("상태 표시등")]
        [SerializeField] Renderer statusLamp;
        [Tooltip("이 거리 안에 있는 플레이어에게만 안내 문구·경고음 (m)")]
        [SerializeField] float radius = 8f;
        [SerializeField] AudioSource source;
        [SerializeField] AudioClip clickSound;
        [SerializeField] AudioClip wrongSound;
        [SerializeField] AudioClip openSound;
        [SerializeField] AudioClip completeSound;

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        int lastShownSeconds = -1;
        int lastLamp = -1;

        void Start()
        {
            var c = Gim005Controller.Instance;
            if (c != null) c.EventReceived += OnEvent;
        }

        void OnDestroy()
        {
            var c = Gim005Controller.Instance;
            if (c != null) c.EventReceived -= OnEvent;
        }

        bool LocalPlayerNear()
        {
            var p = Gim005Controller.LocalPlayer();
            return p != null && Vector3.Distance(p.position, transform.position) <= radius;
        }

        public void PlayClick()
        {
            if (source != null) source.PlayOneShot(clickSound != null ? clickSound : Gim002.Gim002Tones.Click());
        }

        void OnEvent(Gim005NetEvent e, int value)
        {
            var c = Gim005Controller.Instance;
            switch (e)
            {
                case Gim005NetEvent.Wrong:
                    // 경고음은 양층 모두 (정원 쪽은 Gim005MazeArea가 낸다)
                    if (source != null) source.PlayOneShot(wrongSound != null ? wrongSound : Gim002.Gim002Tones.Warning());
                    if (LocalPlayerNear())
                    {
                        string msg = c.Config.msgWrong;
                        if (c.Rules.wrongPolicy != Gim005WrongPolicy.RegrowOpened) msg += " (-" + Mathf.RoundToInt(c.Rules.wrongTimePenalty) + "초)";
                        PlayerHand.Say(msg, 2.5f);
                    }
                    break;
                case Gim005NetEvent.GateOpened:
                    if (source != null) source.PlayOneShot(openSound != null ? openSound : Gim002.Gim002Tones.Open());
                    break;
                case Gim005NetEvent.Complete:
                    if (source != null) source.PlayOneShot(completeSound != null ? completeSound : Gim002.Gim002Tones.Complete());
                    break;
            }
        }

        void Update()
        {
            var c = Gim005Controller.Instance;
            if (c == null) return;
            var st = c.State.state;

            if (timerText != null)
            {
                int secs = Mathf.CeilToInt(c.Remaining);
                if (st == Gim005State.Complete) secs = -2;
                if (secs != lastShownSeconds)
                {
                    lastShownSeconds = secs;
                    timerText.text = secs == -2 ? "--:--" : (secs / 60).ToString("0") + ":" + (secs % 60).ToString("00");
                    timerText.color = st == Gim005State.Running && secs <= c.Rules.bellAtRemaining ? new Color(1f, 0.35f, 0.3f) : Color.white;
                }
            }

            // 0 대기, 1 진행, 2 잠김, 3 시간 초과, 4 완료
            int lamp = st == Gim005State.Complete ? 4 : st == Gim005State.TimeOut ? 3 : st == Gim005State.Ready ? 0 : c.LockRemaining > 0f ? 2 : 1;
            if (lamp == lastLamp) return;
            lastLamp = lamp;
            if (statusText != null)
            {
                string[] words = { "대기 중", "작동 중", "잠김", "시간 초과", "완료" };
                statusText.text = words[lamp];
            }
            if (statusLamp != null)
            {
                Color[] cols = { new Color(0.25f, 0.25f, 0.25f), new Color(0.2f, 0.9f, 0.3f), new Color(1f, 0.1f, 0.05f), new Color(0.5f, 0.1f, 0.6f), new Color(1f, 0.85f, 0.3f) };
                if (block == null) block = new MaterialPropertyBlock();
                statusLamp.GetPropertyBlock(block);
                block.SetColor(BaseColor, cols[lamp]);
                block.SetColor(EmissionColor, lamp == 0 ? Color.black : cols[lamp] * 2f);
                statusLamp.SetPropertyBlock(block);
            }
        }
    }
}
