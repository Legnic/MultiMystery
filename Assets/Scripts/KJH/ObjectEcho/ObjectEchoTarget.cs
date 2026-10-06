using UnityEngine;
using UnityEngine.Events;

// "손을 얹으면 소리가 들리는" 능력(임시 이름: ObjectEcho)의 대상 사물에 붙이는 설정 컴포넌트.
// 이 컴포넌트는 "무엇을, 어디에" 만 정한다 (손 얹을 자리, 바라볼 지점, 들려줄 소리, 1회용 여부, 완료 이벤트).
// 실제 연출(시야 좁아짐, 손 뻗기, 소리 재생)은 Player에 붙은 ObjectEchoController가 한다.
// → 사물마다 연출 코드를 복사하지 않고, 연출 수치는 컨트롤러 한 곳에서 일괄 조정할 수 있다.
//
// 흐름: 바라보고 일정 거리(InteractionController의 Max Look Distance, 기본 2m) 안에 들어오면
//       "[E] 손을 얹는다" 안내가 뜨고, E를 누르면 ObjectEchoController.TryBegin(this)를 호출한다.
// 사용법: 사물의 최상위에 붙이고, 자식으로 HandTarget / FocusPoint 빈 오브젝트를 만들어 연결한다.
//         (Tools/KJH/ObjectEcho/Make Object Echo Target 메뉴를 쓰면 자식까지 한 번에 만들어진다)
//         콜라이더는 이 오브젝트나 자식 어디에 있어도 된다 (조준 레이가 부모 쪽으로 찾아 올라옴).
public class ObjectEchoTarget : MonoBehaviour, IInteractable, IFocusable
{
    [Header("위치")]
    [Tooltip("손바닥이 닿을 위치/방향. 파란 Z축 = 손가락 끝 방향, 초록 Y축 = 손등 방향(사물 표면 바깥쪽). " +
             "비워두면 이 오브젝트의 위치를 쓴다.")]
    [SerializeField] private Transform handTarget;

    [Tooltip("손을 얹는 동안 카메라가 바라볼 지점. 비워두면 HandTarget을 바라본다.")]
    [SerializeField] private Transform focusPoint;

    [Header("소리")]
    [Tooltip("손을 얹었을 때 들려줄 소리.")]
    [SerializeField] private AudioClip echoClip;

    [Tooltip("소리 크기 (0~1).")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Tooltip("스펙트럼 선의 출렁임 강도 배율. 1 = 기본, 0.5 = 잔잔하게, 2 = 크게.")]
    [Range(0f, 3f)]
    [SerializeField] private float spectrumIntensity = 1f;

    [Tooltip("스펙트럼이 소리에 반응하는 방식 프리셋 (Calm=잔잔 / Normal=보통 / Sensitive=예민). " +
             "비워두면 SpectrumOverlay의 기본 반응을 쓴다. 프리셋 위치: Assets/Scripts/KJH/SoundCue/Presets/")]
    [SerializeField] private SpectrumResponseProfile spectrumResponse;

    [Header("사용 규칙")]
    [Tooltip("여러 번 손을 얹을 수 있는지. 끄면(기본) 한 번 듣고 나면 다시 안내가 뜨지 않는다.")]
    [SerializeField] private bool repeatable = false;

    [Tooltip("안내 문구에 표시할 동작 이름. 앞에 '[E] '가 자동으로 붙는다.")]
    [SerializeField] private string promptText = "손을 얹는다";

    [Tooltip("조준했을 때 은은하게 밝아지는 강조를 쓸지. 단서와 같은 ClueHighlighter를 쓴다. 기본은 꺼짐(안내 문구만).")]
    [SerializeField] private bool highlightOnFocus = false;

    [Header("이벤트 (퍼즐 로직 연결용)")]
    [Tooltip("소리를 끝까지 듣고 연출이 완전히 끝났을 때 호출된다 (중간에 중단되면 호출되지 않는다).")]
    [SerializeField] private UnityEvent onEchoCompleted;

    // 한 번이라도 끝까지 들었는지. 1회용 대상이면 이 값이 true가 된 뒤로는 상호작용할 수 없다.
    public bool HasCompleted { get; private set; }

    // 지금 이 대상으로 연출이 진행 중인지 (중복 시작 방지).
    public bool IsInUse { get; private set; }

    // 컨트롤러가 읽어가는 설정값들. 비어 있는 위치는 대체값을 돌려준다.
    public Transform HandTarget => handTarget != null ? handTarget : transform;
    public Transform FocusPoint => focusPoint != null ? focusPoint : HandTarget;
    public AudioClip EchoClip => echoClip;
    public float Volume => volume;
    public float SpectrumIntensity => spectrumIntensity;
    public SpectrumResponseProfile SpectrumResponse => spectrumResponse;

    // 코드에서 AddListener로 구독할 수 있게 공개 (Inspector 연결과 함께 쓸 수 있음).
    public UnityEvent OnEchoCompleted => onEchoCompleted;

    private ClueHighlighter highlighter;

    private void Awake()
    {
        if (highlightOnFocus)
        {
            highlighter = GetComponent<ClueHighlighter>();
            if (highlighter == null) highlighter = gameObject.AddComponent<ClueHighlighter>();
        }
    }

    // ── IInteractable ──────────────────────────────────────
    // 연출 중이 아니고, (반복 가능하거나 아직 안 들었으면) 상호작용 가능.
    public bool CanInteract => !IsInUse && (repeatable || !HasCompleted);
    public string InteractPrompt => $"[E] {promptText}";

    public void Interact(GameObject interactor)
    {
        if (!CanInteract) return;

        // 연출은 상호작용한 플레이어 쪽 컨트롤러가 맡는다 (2인 협동에서도 "손 얹은 사람"에게만 연출이 나오도록).
        ObjectEchoController echoController = interactor.GetComponent<ObjectEchoController>();
        if (echoController == null)
        {
            Debug.LogWarning("[ObjectEchoTarget] 상호작용한 플레이어에 ObjectEchoController가 없습니다.", interactor);
            return;
        }
        echoController.TryBegin(this);
    }

    // ── IFocusable (선택: 강조 표시) ───────────────────────
    public void OnFocusEnter()
    {
        if (highlighter != null) highlighter.SetHighlighted(true);
    }

    public void OnFocusExit()
    {
        if (highlighter != null) highlighter.SetHighlighted(false);
    }

    // ── 컨트롤러가 부르는 상태 알림 ─────────────────────────

    // 연출이 시작될 때.
    public void NotifyStarted()
    {
        IsInUse = true;
        if (highlighter != null) highlighter.SetHighlighted(false);
    }

    // 연출이 끝났을 때. completed가 true면 소리를 끝까지 들은 것 → 완료 처리 + 이벤트 발생.
    public void NotifyFinished(bool completed)
    {
        IsInUse = false;
        if (!completed) return; // 중간에 중단됐다면 "아직 못 들은 것"으로 남겨서 다시 시도할 수 있게 한다
        HasCompleted = true;
        onEchoCompleted?.Invoke();
    }

    // 씬 뷰에서 이 오브젝트를 선택했을 때 손 자리와 응시 지점을 보여준다 (배치 작업을 돕기 위한 표시).
    private void OnDrawGizmosSelected()
    {
        Transform hand = HandTarget;
        // 손 자리: 손바닥 크기의 납작한 상자 + 손가락 방향(파랑) / 손등 방향(초록) 선.
        Gizmos.color = new Color(0.9f, 0.7f, 0.4f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(hand.position, hand.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0f, 0.01f, 0.02f), new Vector3(0.09f, 0.02f, 0.17f));
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(hand.position, hand.position + hand.forward * 0.12f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(hand.position, hand.position + hand.up * 0.08f);

        // 응시 지점: 작은 구 + 손 자리와 잇는 점선 느낌의 선.
        Transform focus = FocusPoint;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(focus.position, 0.03f);
        if (focus != hand) Gizmos.DrawLine(focus.position, hand.position);
    }
}
