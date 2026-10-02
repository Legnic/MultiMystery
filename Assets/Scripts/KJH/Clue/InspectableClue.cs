using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 바라보고 E를 누르면 눈앞으로 다가와서 마우스로 돌려볼 수 있는 "살펴보기 단서".
// 흐름: Idle(평소) → Focused(조준됨, 은은한 강조) → MovingIn(다가오는 중) → Inspecting(살펴보는 중)
//       → MovingOut(돌아가는 중) → Idle
// 갑툭튀 없는 긴장감 톤에 맞춰, 모든 움직임은 천천히 시작하고 천천히 멈추게(EaseInOut) 만들었다.
//
// 사용법: 단서 오브젝트의 "최상위(루트)"에 붙인다 (자식 메시가 아니라, 함께 움직일 전체의 루트).
// 콜라이더는 이 오브젝트나 자식 어디에 있어도 된다 (InteractionController가 부모 쪽으로 찾아 올라옴).
public class InspectableClue : MonoBehaviour, IInteractable, IFocusable, IModalInteraction
{
    // 살펴보기 상태. 상태를 하나의 값으로 관리하면 "이동 중에 또 눌림" 같은 꼬임을 쉽게 막을 수 있다.
    public enum State { Idle, Focused, MovingIn, Inspecting, MovingOut }

    [Header("단서")]
    [Tooltip("이 오브젝트를 처음 살펴볼 때 획득하는 단서 데이터.")]
    [SerializeField] private ClueData clueData;

    [Tooltip("안내 문구에 표시할 동작 이름. '[E] 살펴보기'처럼 앞에 [E]가 자동으로 붙는다.")]
    [SerializeField] private string promptText = "살펴보기";

    [Header("살펴보기 위치")]
    [Tooltip("살펴볼 때 카메라 앞 거리 (m). 카메라 근평면(Near Clip)보다 충분히 멀어야 잘리지 않는다.")]
    [SerializeField] private float inspectDistance = 0.45f;

    [Tooltip("살펴볼 때 가장 긴 변이 이 크기가 되도록 크기를 맞춘다 (m). 0이면 원래 크기 그대로.")]
    [SerializeField] private float inspectFitSize = 0.30f;

    [Tooltip("카메라를 바라볼 때 추가로 돌릴 각도 (도, X/Y/Z). 0,0,0이면 오브젝트의 로컬 X축이 화면 가로 방향이 된다. " +
             "Tools/KJH/Clue/Make Inspectable Parchment 메뉴를 쓰면 글자 면이 카메라를 향하도록 자동으로 계산된다.")]
    [SerializeField] private Vector3 inspectRotationOffset = Vector3.zero;

    [Tooltip("살펴볼 때 글자가 위아래로 뒤집혀 보이면 체크한다 (화면 기준으로 180도 돌림).")]
    [SerializeField] private bool flipUpsideDown = false;

    [Tooltip("살펴볼 때 글자 면이 아니라 뒷면이 보이면 체크한다 (화면 기준으로 좌우로 180도 돌려 반대 면을 보여줌).")]
    [SerializeField] private bool flipFace = false;

    [Header("움직임")]
    [Tooltip("다가오기/돌아가기에 걸리는 시간 (초).")]
    [SerializeField] private float moveDuration = 0.6f;

    [Tooltip("움직임 곡선. 가로 0~1(시간), 세로 0~1(진행도). 기본은 천천히 시작해서 천천히 멈추는 EaseInOut.")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("마우스 1픽셀 이동당 회전 각도 (도).")]
    [SerializeField] private float rotateSensitivity = 0.15f;

    [Tooltip("위아래로 돌려볼 수 있는 최대 각도 (도). 너무 크면 뒤집혀서 어지럽다.")]
    [SerializeField] private float pitchLimit = 80f;

    [Header("펼침 연출 (자동)")]
    [Tooltip("켜두면 이 오브젝트(또는 자식)에 붙은 펼침 연출(ParchmentRoller 등 IRevealAnimation)을 자동으로 재생한다. " +
             "카메라 앞에 도착하면 펼치고, 내려놓기를 시작하면 다시 원래대로 되돌린다.")]
    [SerializeField] private bool autoPlayReveal = true;

    [Tooltip("펼치는 데 걸리는 시간 (초).")]
    [SerializeField] private float revealDuration = 1f;

    [Tooltip("내려놓을 때 원래대로(말림/접힘) 되돌리는 시간 (초). 돌아가는 시간(moveDuration)과 비슷하면 자연스럽다.")]
    [SerializeField] private float hideDuration = 0.6f;

    [Header("입력")]
    [Tooltip("마우스 이동량을 읽을 액션 (Player/Look). 살펴보는 동안 오브젝트 회전에 쓴다.")]
    [SerializeField] private InputActionReference lookAction;

    [Header("강조 표시 (조준됐을 때)")]
    [Tooltip("조준됐을 때 기본 색에 곱할 밝기 배율. 1.15 = 15% 밝게.")]
    [SerializeField] private float highlightMultiplier = 1.15f;

    [Tooltip("강조가 서서히 켜지고 꺼지는 시간 (초). 번쩍이지 않게 짧고 부드럽게.")]
    [SerializeField] private float highlightFadeDuration = 0.15f;

    [Header("배경 연출")]
    [Tooltip("살펴보는 동안 배경을 흐리게 하는 Volume (Inspect_Volume). weight가 0 → 1로 바뀐다. 비워두면 연출 없이 동작한다.")]
    [SerializeField] private Volume inspectVolume;

    [Header("사운드 (비어 있어도 동작)")]
    [SerializeField] private AudioClip pickUpSound;
    [SerializeField] private AudioClip putDownSound;

    [Header("참조 (비워두면 씬에서 자동으로 찾음)")]
    [SerializeField] private ClueJournal clueJournal;
    [SerializeField] private PhotoAcquiredToast acquiredToast;

    [Header("추가 이벤트 (펼침은 위 '자동' 설정이 처리하므로, 소리/조명 등 그 밖의 연출용)")]
    [Tooltip("카메라 앞에 도착해서 살펴보기가 시작될 때 호출된다. 펼침 연출은 autoPlayReveal이 자동으로 하므로 여기에 또 연결하지 않는다.")]
    [SerializeField] private UnityEvent onInspectStarted;

    [Tooltip("내려놓기를 시작할 때 호출된다.")]
    [SerializeField] private UnityEvent onInspectEnded;

    // ── 런타임 상태 ─────────────────────────────────────────
    public State CurrentState { get; private set; } = State.Idle;

    // 나중에 연출 코드에서 AddListener로 구독할 수 있도록 공개한 연결 지점 (Inspector 연결과 함께 쓸 수 있음).
    public UnityEvent OnInspectStarted => onInspectStarted;
    public UnityEvent OnInspectEnded => onInspectEnded;

    // 이 단서에 붙은 펼침 연출들 (ParchmentRoller 등). 하나도 없으면 빈 배열.
    private IRevealAnimation[] revealAnimations;

    // 강조 표시용
    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit의 기본 색 속성
    private float highlightCurrent;  // 0 = 강조 없음, 1 = 완전 강조
    private float highlightTarget;

    // 원래 자리 복원용 (로컬 값으로 저장해야 부모가 있어도 정확히 돌아간다)
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;

    // 살펴보는 동안 끈 콜라이더/바꾼 Rigidbody를 되돌리기 위한 기록
    private readonly List<Collider> disabledColliders = new List<Collider>();
    private Rigidbody body;
    private bool bodyWasKinematic;

    // 살펴보기를 시작한 플레이어 쪽 참조
    private InteractionController controller;
    private PlayerMovement playerMovement;
    private Transform cameraTransform;

    // 살펴보기 중 회전 상태
    private Quaternion inspectBaseRotation; // 카메라 기준 기본 자세 (회전 0일 때)
    private float inspectYaw;
    private float inspectPitch;
    private Vector3 inspectCenterLocal;     // 오브젝트 로컬 좌표에서 본 "눈에 보이는 중심" (피벗이 가운데가 아닐 수 있어서)
    private Vector3 inspectScale;

    private void Awake()
    {
        // GetComponent 계열은 Awake에서 한 번만 찾아서 캐싱한다.
        renderers = GetComponentsInChildren<Renderer>();
        // 인터페이스 타입으로도 컴포넌트를 찾을 수 있다. 어떤 종류의 펼침 연출이든 IRevealAnimation이면 다 모인다.
        revealAnimations = GetComponentsInChildren<IRevealAnimation>(true);
        body = GetComponent<Rigidbody>();
        propertyBlock = new MaterialPropertyBlock();

        if (clueJournal == null) clueJournal = FindAnyObjectByType<ClueJournal>();
        if (acquiredToast == null) acquiredToast = FindAnyObjectByType<PhotoAcquiredToast>(FindObjectsInactive.Include);
        if (inspectVolume != null) inspectVolume.weight = 0f;
    }

    private void OnEnable()
    {
        if (lookAction != null) lookAction.action.Enable();
    }

    private void Update()
    {
        UpdateHighlight();

        if (CurrentState == State.Inspecting) UpdateInspectRotation();
    }

    // ── IInteractable ──────────────────────────────────────
    // 평소(Idle)나 조준된(Focused) 상태에서만 살펴보기를 시작할 수 있다. 이동 중에는 다시 눌러도 무시.
    public bool CanInteract => CurrentState == State.Idle || CurrentState == State.Focused;
    public string InteractPrompt => $"[E] {promptText}";

    public void Interact(GameObject interactor)
    {
        if (!CanInteract) return;

        controller = interactor.GetComponent<InteractionController>();
        playerMovement = interactor.GetComponent<PlayerMovement>();
        Camera cam = controller != null && controller.LookCamera != null ? controller.LookCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[InspectableClue] 살펴보기에 쓸 카메라를 찾지 못했습니다.", this);
            return;
        }
        cameraTransform = cam.transform;

        BeginInspect();
    }

    // ── IFocusable ─────────────────────────────────────────
    public void OnFocusEnter()
    {
        if (CurrentState != State.Idle) return;
        CurrentState = State.Focused;
        highlightTarget = 1f;
    }

    public void OnFocusExit()
    {
        // 살펴보기 시작으로 조준이 풀린 경우에는 상태를 Idle로 되돌리면 안 된다 (Focused일 때만 되돌림).
        if (CurrentState == State.Focused) CurrentState = State.Idle;
        highlightTarget = 0f;
    }

    // ── IModalInteraction (살펴보기 중 E / Esc) ────────────
    public void OnInteractPressed()
    {
        if (CurrentState == State.Inspecting) EndInspect(); // 이동 중(MovingIn/Out)의 입력은 무시
    }

    public void OnCancelPressed()
    {
        if (CurrentState == State.Inspecting) EndInspect();
    }

    // ── 살펴보기 시작 / 끝 ──────────────────────────────────

    private void BeginInspect()
    {
        CurrentState = State.MovingIn;
        highlightTarget = 0f;

        // 1) 원래 자리를 기억한다 (돌아올 때 이 값으로 정확히 복원).
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;

        // 2) 레이캐스트에 다시 맞지 않도록 콜라이더를 끄고, 물리에 끌려가지 않도록 Rigidbody를 Kinematic으로.
        disabledColliders.Clear();
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (!col.enabled) continue; // 원래 꺼져 있던 건 건드리지 않는다 (복원 때 켜버리지 않도록)
            col.enabled = false;
            disabledColliders.Add(col);
        }
        if (body != null)
        {
            bodyWasKinematic = body.isKinematic;
            body.isKinematic = true;
        }

        // 3) 플레이어 이동/시점 회전을 멈춘다 (CameraModeController와 같은 방식).
        if (playerMovement != null)
        {
            playerMovement.SpeedMultiplier = 0f;
            playerMovement.LookEnabled = false;
        }

        // 4) E/Esc 입력을 이쪽으로 받고, 바라보기 감지를 멈추게 한다 (안내 문구도 숨겨짐).
        if (controller != null) controller.BeginModal(this);

        // 5) 처음 살펴볼 때만 단서를 획득하고 알림을 띄운다. 획득 처리는 ClueJournal 한 곳에서만 한다.
        if (clueJournal != null && clueData != null && clueJournal.TryAcquire(clueData))
        {
            if (acquiredToast != null) acquiredToast.ShowMessage($"단서 획득: {clueData.displayName}");
        }
        else if (clueJournal == null)
        {
            Debug.LogWarning("[InspectableClue] 씬에 ClueJournal이 없어 단서를 획득 처리하지 못했습니다.", this);
        }

        PlaySound(pickUpSound);

        // 6) 카메라 앞의 목표 자세를 계산하고 이동을 시작한다.
        PrepareInspectPose();
        StartCoroutine(MoveInRoutine());
    }

    private void EndInspect()
    {
        CurrentState = State.MovingOut;
        if (controller != null && controller.PromptUI != null) controller.PromptUI.Hide();
        if (autoPlayReveal)
        {
            foreach (IRevealAnimation reveal in revealAnimations) reveal.PlayHide(hideDuration); // 돌아가면서 다시 말기/접기
        }
        onInspectEnded?.Invoke();
        PlaySound(putDownSound);
        StartCoroutine(MoveOutRoutine());
    }

    // 카메라 앞에서의 크기와 "보이는 중심"을 미리 계산한다.
    private void PrepareInspectPose()
    {
        inspectYaw = 0f;
        inspectPitch = 0f;

        // 피벗(오브젝트 원점)이 메시 가운데가 아닐 수 있으므로, 렌더러 bounds의 중심을 로컬 좌표로 기억해 둔다.
        // 이 점이 카메라 앞 정중앙에 오게 하고, 회전도 이 점을 중심으로 돌린다.
        Bounds worldBounds = GetWorldBounds();
        inspectCenterLocal = transform.InverseTransformPoint(worldBounds.center);

        // 가장 긴 변이 inspectFitSize가 되도록 크기 배율을 구한다. 회전된 AABB가 아니라 메시 자체 크기로 재야 정확하다.
        inspectScale = transform.localScale;
        if (inspectFitSize > 0f)
        {
            float longest = GetLongestSideWorld();
            if (longest > 0.0001f) inspectScale = transform.localScale * (inspectFitSize / longest);
        }
    }

    // 지금 회전값(yaw/pitch)에서의 카메라 앞 목표 위치/회전.
    private void GetInspectTarget(out Vector3 position, out Quaternion rotation)
    {
        // 뒤집기 옵션은 "화면 기준"으로 적용한다: 위아래 뒤집기 = 화면 앞뒤 축(Z)으로 180도, 면 뒤집기 = 화면 세로 축(Y)으로 180도.
        // 화면 기준으로 돌리기 때문에 모델의 축이 어떻게 생겼든 체크 한 번으로 고칠 수 있다.
        Quaternion screenFlip = Quaternion.Euler(0f, flipFace ? 180f : 0f, flipUpsideDown ? 180f : 0f);
        inspectBaseRotation = cameraTransform.rotation * screenFlip * Quaternion.Euler(inspectRotationOffset);
        // 좌우는 카메라 위쪽 축, 위아래는 카메라 오른쪽 축 기준으로 돌린다 (화면에서 보이는 그대로 돌아가는 느낌).
        rotation = Quaternion.AngleAxis(inspectPitch, cameraTransform.right)
                 * Quaternion.AngleAxis(-inspectYaw, cameraTransform.up)
                 * inspectBaseRotation;

        Vector3 focusPoint = cameraTransform.position + cameraTransform.forward * inspectDistance;
        // "보이는 중심"이 focusPoint에 오도록 원점 위치를 역산한다 (로컬 중심 × 크기 × 회전만큼 빼기).
        position = focusPoint - rotation * Vector3.Scale(inspectCenterLocal, inspectScale);
    }

    private IEnumerator MoveInRoutine()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 startScale = transform.localScale;
        float startWeight = inspectVolume != null ? inspectVolume.weight : 0f;

        // 카메라를 따라다닐 필요가 없도록(시점이 멈춰 있으므로) 부모에서 떼어 월드 기준으로 움직인다.
        transform.SetParent(null, true);

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            // 일시정지(timeScale = 0)와 무관하게 연출이 진행되도록 unscaledDeltaTime을 쓴다.
            elapsed += Time.unscaledDeltaTime;
            float t = moveCurve.Evaluate(Mathf.Clamp01(elapsed / moveDuration));

            GetInspectTarget(out Vector3 targetPos, out Quaternion targetRot);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            transform.localScale = Vector3.Lerp(startScale, inspectScale, t);
            if (inspectVolume != null) inspectVolume.weight = Mathf.Lerp(startWeight, 1f, t);
            yield return null;
        }

        ApplyInspectPose();
        if (inspectVolume != null) inspectVolume.weight = 1f;

        CurrentState = State.Inspecting;
        if (controller != null && controller.PromptUI != null) controller.PromptUI.Show("[E] 내려놓기  [마우스] 돌려보기");
        if (autoPlayReveal)
        {
            foreach (IRevealAnimation reveal in revealAnimations) reveal.PlayReveal(revealDuration); // 눈앞에 도착한 뒤 펼친다
        }
        onInspectStarted?.Invoke();
    }

    private IEnumerator MoveOutRoutine()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 startScale = transform.localScale;
        float startWeight = inspectVolume != null ? inspectVolume.weight : 0f;

        // 원래 자리(월드 기준)를 계산한다. 원래 부모가 있으면 부모 기준 로컬 값을 월드로 바꾼다.
        Vector3 endPos = originalParent != null ? originalParent.TransformPoint(originalLocalPosition) : originalLocalPosition;
        Quaternion endRot = originalParent != null ? originalParent.rotation * originalLocalRotation : originalLocalRotation;

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = moveCurve.Evaluate(Mathf.Clamp01(elapsed / moveDuration));

            transform.position = Vector3.Lerp(startPos, endPos, t);
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            transform.localScale = Vector3.Lerp(startScale, originalLocalScale, t);
            if (inspectVolume != null) inspectVolume.weight = Mathf.Lerp(startWeight, 0f, t);
            yield return null;
        }

        // 마지막에는 보간 오차 없이 저장해 둔 로컬 값을 그대로 넣어 "정확히" 원위치시킨다 (반복해도 조금씩 어긋나지 않게).
        transform.SetParent(originalParent, false);
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
        transform.localScale = originalLocalScale;
        if (inspectVolume != null) inspectVolume.weight = 0f;

        // 꺼뒀던 콜라이더/Rigidbody/플레이어 조작을 원래대로 돌려준다.
        foreach (Collider col in disabledColliders)
        {
            if (col != null) col.enabled = true;
        }
        disabledColliders.Clear();
        if (body != null) body.isKinematic = bodyWasKinematic;

        if (playerMovement != null)
        {
            playerMovement.SpeedMultiplier = 1f;
            playerMovement.LookEnabled = true;
        }

        CurrentState = State.Idle;
        if (controller != null) controller.EndModal(this);
    }

    private void ApplyInspectPose()
    {
        GetInspectTarget(out Vector3 pos, out Quaternion rot);
        transform.position = pos;
        transform.rotation = rot;
        transform.localScale = inspectScale;
    }

    // 살펴보는 동안 마우스 이동량(Look 액션)으로 오브젝트를 돌린다. 커서는 잠긴 상태 그대로다.
    private void UpdateInspectRotation()
    {
        if (lookAction == null || cameraTransform == null) return;

        Vector2 delta = lookAction.action.ReadValue<Vector2>();
        inspectYaw += delta.x * rotateSensitivity;
        inspectPitch = Mathf.Clamp(inspectPitch + delta.y * rotateSensitivity, -pitchLimit, pitchLimit);
        ApplyInspectPose();
    }

    // 테스트/디버그용: 마우스 입력 없이 회전값을 주입한다 (MCP 플레이 테스트에서 사용).
    public void AddInspectRotation(float yawDegrees, float pitchDegrees)
    {
        if (CurrentState != State.Inspecting) return;
        inspectYaw += yawDegrees;
        inspectPitch = Mathf.Clamp(inspectPitch + pitchDegrees, -pitchLimit, pitchLimit);
        ApplyInspectPose();
    }

    // ── 강조 표시 ──────────────────────────────────────────

    // highlightCurrent를 highlightTarget 쪽으로 천천히 옮기고, 그만큼 기본 색을 밝게 한다.
    // MaterialPropertyBlock을 쓰는 이유: 머티리얼을 복제(인스턴스)하지 않고 이 렌더러만 색을 바꿀 수 있어서.
    private void UpdateHighlight()
    {
        if (Mathf.Approximately(highlightCurrent, highlightTarget)) return;

        float step = highlightFadeDuration > 0f ? Time.unscaledDeltaTime / highlightFadeDuration : 1f;
        highlightCurrent = Mathf.MoveTowards(highlightCurrent, highlightTarget, step);
        float multiplier = Mathf.Lerp(1f, highlightMultiplier, Mathf.SmoothStep(0f, 1f, highlightCurrent));

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (highlightCurrent <= 0f)
                {
                    // 완전히 꺼지면 블록을 비워서 원래 머티리얼 색으로 완전히 돌아가게 한다.
                    r.SetPropertyBlock(null, i);
                    continue;
                }
                if (mats[i] == null || !mats[i].HasProperty(BaseColorId)) continue;

                Color baseColor = mats[i].GetColor(BaseColorId);
                Color lit = baseColor * multiplier;
                lit.a = baseColor.a; // 투명도는 바꾸지 않는다
                r.GetPropertyBlock(propertyBlock, i);
                propertyBlock.SetColor(BaseColorId, lit);
                r.SetPropertyBlock(propertyBlock, i);
            }
        }
    }

    // ── 보조 함수 ──────────────────────────────────────────

    private Bounds GetWorldBounds()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    // 메시 자체의 로컬 bounds에 실제 크기(lossyScale)를 곱해서 가장 긴 변을 구한다 (회전과 무관한 실제 길이).
    private float GetLongestSideWorld()
    {
        float longest = 0f;
        foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Vector3 size = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
            longest = Mathf.Max(longest, Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }
        return longest;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || cameraTransform == null) return;
        // 카메라 위치에서 재생해서 거리 감쇠 없이 또렷하게 들리게 한다.
        AudioSource.PlayClipAtPoint(clip, cameraTransform.position);
    }
}
