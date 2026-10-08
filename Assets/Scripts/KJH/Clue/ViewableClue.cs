using UnityEngine;
using UnityEngine.Events;

// 가장 흔한 "일반 단서". 바라보고 E를 누르면 화면에 이미지와 설명(ClueViewerUI)이 뜨고,
// 다시 E 또는 Esc를 누르면 사라진다. 오브젝트 자체는 움직이지 않는다.
// (양피지처럼 눈앞으로 가져와 펼쳐 보는 특별한 단서는 InspectableClue를 쓴다)
// 흐름: Idle(평소) → Focused(조준됨, 은은한 강조) → Open(이미지·설명 표시 중) → Idle
// 사용법: 단서 오브젝트의 최상위에 붙이고 clueData를 연결한다. 콜라이더는 이 오브젝트나 자식 어디에 있어도 된다.
//        (Tools/KJH/Clue/Make Viewable Clue 메뉴를 쓰면 한 번에 설정된다)
public class ViewableClue : MonoBehaviour, IInteractable, IFocusable, IModalInteraction
{
    public enum State { Idle, Focused, Open }

    [Header("단서")]
    [Tooltip("이 오브젝트를 살펴볼 때 보여줄 단서 데이터 (이름·설명·이미지). 처음 볼 때 획득 처리된다.")]
    [SerializeField] private ClueData clueData;

    [Tooltip("안내 문구에 표시할 동작 이름. '[E] 살펴보기'처럼 앞에 [E]가 자동으로 붙는다.")]
    [SerializeField] private string promptText = "살펴보기";

    [Header("사운드 (비어 있어도 동작)")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    [Header("참조 (비워두면 씬에서 자동으로 찾음)")]
    [SerializeField] private ClueViewerUI viewer;
    [SerializeField] private ClueJournal clueJournal;
    [SerializeField] private PhotoAcquiredToast acquiredToast;

    [Header("추가 이벤트 (소리·조명 등 그 밖의 연출용)")]
    [Tooltip("이미지·설명이 화면에 뜰 때 호출된다.")]
    [SerializeField] private UnityEvent onOpened;

    [Tooltip("이미지·설명이 닫힐 때 호출된다.")]
    [SerializeField] private UnityEvent onClosed;

    // 이미지·설명이 화면에 뜰 때마다 코드에서 구독할 수 있는 이벤트 (예: SafeClue가 "처음 읽음"을 판단하는 데 쓴다).
    public event System.Action<ViewableClue> Opened;

    public State CurrentState { get; private set; } = State.Idle;
    public ClueData ClueData => clueData;

    private ClueHighlighter highlighter;
    private InteractionController controller;
    private IPlayerLock playerLock;

    private void Awake()
    {
        // 강조 컴포넌트가 없으면 실행 중에 붙여서 그대로 동작하게 한다.
        highlighter = GetComponent<ClueHighlighter>();
        if (highlighter == null) highlighter = gameObject.AddComponent<ClueHighlighter>();

        // 비워둔 참조는 씬에서 찾아 채운다 (모든 일반 단서가 같은 UI 하나를 같이 쓴다).
        if (viewer == null) viewer = FindAnyObjectByType<ClueViewerUI>(FindObjectsInactive.Include);
        if (clueJournal == null) clueJournal = FindAnyObjectByType<ClueJournal>();
        if (acquiredToast == null) acquiredToast = FindAnyObjectByType<PhotoAcquiredToast>(FindObjectsInactive.Include);
    }

    // ── IInteractable ──────────────────────────────────────
    // 열려 있지 않을 때만 E로 열 수 있다.
    public bool CanInteract => CurrentState != State.Open;
    public string InteractPrompt => $"[E] {promptText}";

    public void Interact(GameObject interactor)
    {
        if (!CanInteract) return;
        if (viewer == null)
        {
            Debug.LogWarning("[ViewableClue] 씬에 ClueViewerUI가 없어 단서를 보여줄 수 없습니다. Tools/KJH/Clue/Create Clue Viewer UI 메뉴로 만들어 주세요.", this);
            return;
        }
        if (clueData == null)
        {
            Debug.LogWarning("[ViewableClue] clueData가 비어 있습니다.", this);
            return;
        }

        controller = interactor.GetComponent<InteractionController>();
        playerLock = interactor.GetComponent<IPlayerLock>();
        Open();
    }

    // ── IFocusable ─────────────────────────────────────────
    public void OnFocusEnter()
    {
        if (CurrentState != State.Idle) return;
        CurrentState = State.Focused;
        highlighter.SetHighlighted(true);
    }

    public void OnFocusExit()
    {
        // 열리는 순간에도 조준 해제 알림이 오는데, 그때 상태를 Idle로 되돌리면 안 된다 (Focused일 때만).
        if (CurrentState == State.Focused) CurrentState = State.Idle;
        highlighter.SetHighlighted(false);
    }

    // ── IModalInteraction (열려 있는 동안 E / Esc) ─────────
    public void OnInteractPressed()
    {
        if (CurrentState == State.Open) Close();
    }

    public void OnCancelPressed()
    {
        if (CurrentState == State.Open) Close();
    }

    // ── 열기 / 닫기 ────────────────────────────────────────

    private void Open()
    {
        CurrentState = State.Open;
        highlighter.SetHighlighted(false);

        // 보고 있는 동안은 플레이어 이동·시점 회전을 멈춘다 (CameraModeController, InspectableClue와 같은 방식).
        if (playerLock != null)
        {
            playerLock.SpeedMultiplier = 0f;
            playerLock.LookEnabled = false;
        }

        // E/Esc 입력을 이쪽으로 받고, 바라보기 감지와 안내 문구를 멈춘다.
        if (controller != null) controller.BeginModal(this);

        viewer.Show(clueData);

        // 처음 볼 때만 단서를 획득하고 알림을 띄운다. 획득 처리는 ClueJournal 한 곳에서만 한다.
        if (clueJournal != null && clueJournal.TryAcquire(clueData))
        {
            if (acquiredToast != null) acquiredToast.ShowMessage($"단서 획득: {clueData.displayName}");
        }
        else if (clueJournal == null)
        {
            Debug.LogWarning("[ViewableClue] 씬에 ClueJournal이 없어 단서를 획득 처리하지 못했습니다.", this);
        }

        PlaySound(openSound);
        onOpened?.Invoke();
        Opened?.Invoke(this);
    }

    private void Close()
    {
        CurrentState = State.Idle;
        viewer.Hide();

        if (playerLock != null)
        {
            playerLock.SpeedMultiplier = 1f;
            playerLock.LookEnabled = true;
        }

        // 독점 상태를 풀면 다음 프레임부터 다시 바라보기 감지가 시작된다 (계속 보고 있으면 다시 강조됨).
        if (controller != null) controller.EndModal(this);

        PlaySound(closeSound);
        onClosed?.Invoke();
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        Camera cam = controller != null && controller.LookCamera != null ? controller.LookCamera : Camera.main;
        // 카메라 위치에서 재생해서 거리 감쇠 없이 또렷하게 들리게 한다.
        AudioSource.PlayClipAtPoint(clip, cam != null ? cam.transform.position : transform.position);
    }
}
