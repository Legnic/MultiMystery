using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 사진 촬영 모드로 들어가고 나올 때의 카메라/연출을 전담하는 컴포넌트.
// "언제 들어가고 나올지"는 이 클래스가 결정하지 않는다 (PhotoCaptureSystem이 결정해서
// EnterCaptureMode/ExitCaptureMode를 호출해준다) — 이 클래스는 오직
// "카메라를 지정된 위치로 부드럽게 옮기고, 뷰파인더 연출(비네팅/필름그레인/프레임 UI)을
// 켜고 끄는 것"만 책임진다.
// 사용법: Player(또는 매니저 오브젝트)에 붙인다.
public class CameraModeController : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("플레이어 시점 카메라. 촬영 모드가 되면 이 카메라를 CameraAnchor 위치로 이동시킨다.")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("비네팅/필름그레인/세피아 효과가 담긴 Volume. Is Global 체크, Weight는 0으로 시작해야 한다.")]
    [SerializeField] private Volume viewfinderVolume;

    [Tooltip("화면 가장자리의 뷰파인더 프레임 UI를 켜고 끄는 CanvasGroup. 평소엔 alpha 0.")]
    [SerializeField] private CanvasGroup viewfinderFrameCanvasGroup;

    [Tooltip("촬영 중 이동/시점 회전을 완전히 멈추기 위해 참조하는 플레이어 이동 스크립트 (IPlayerLock을 구현한 컴포넌트: PlayerMovement, 또는 네트워크 플레이어의 NetworkFirstPersonController 등).")]
    [SerializeField] private MonoBehaviour playerMovement;

    [Header("연출 수치")]
    [Tooltip("카메라가 CameraAnchor로 이동/복귀하는 데 걸리는 시간 (초). 기획서 기준 0.6초.")]
    [SerializeField] private float cameraMoveDuration = 0.6f;

    [Tooltip("촬영 모드일 때 시야각 (FOV). 0 이하로 두면 FOV는 바꾸지 않는다.")]
    [SerializeField] private float captureFov = 50f;

    [Header("호흡 흔들림 (선택 연출)")]
    [Tooltip("뷰파인더 안에 고정된 동안 아주 약하게 흔들리는 '숨소리' 연출을 켤지 여부. 기본 꺼짐.")]
    [SerializeField] private bool breathingSwayEnabled = false;

    [Tooltip("호흡 흔들림의 세기 (각도, degree 단위). 값이 클수록 더 많이 흔들린다.")]
    [SerializeField] private float breathingSwayIntensity = 0.3f;

    [Tooltip("호흡 흔들림의 속도 (초당 진동 수에 영향).")]
    [SerializeField] private float breathingSwaySpeed = 0.8f;

    // 촬영 모드로 들어가거나 나오는 "전환 중"인지. 전환 중엔 중복 입력을 막는 용도로 쓴다.
    public bool IsTransitioning { get; private set; }

    // 전환이 끝나고 카메라가 CameraAnchor에 완전히 고정된 상태인지 (호흡 흔들림은 이때만 적용).
    public bool IsLockedOnAnchor { get; private set; }

    private Coroutine transitionRoutine;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private float originalFov;
    private float breathingSeed;
    private IPlayerLock playerLock;
    // 고정된 순간의 카메라 로컬 회전. 호흡 흔들림은 이 회전 위에 아주 작게 더해진다.
    private Quaternion lockedLocalRotation;
    // 이번 촬영 모드를 나올 때 플레이어 조작을 돌려줄지 (살펴보기 중 촬영이면 false — 살펴보기가 끝날 때 돌려준다).
    private bool restorePlayerControlOnExit = true;

    private void Awake()
    {
        if (mainCamera != null) originalFov = mainCamera.fieldOfView;
        if (viewfinderVolume != null) viewfinderVolume.weight = 0f;
        if (viewfinderFrameCanvasGroup != null) viewfinderFrameCanvasGroup.alpha = 0f;
        breathingSeed = Random.Range(0f, 100f); // 여러 카메라가 동시에 흔들려도 서로 다른 위상을 갖게 하기 위한 오프셋

        // 이동/시점 잠금: Inspector에 연결된 컴포넌트가 IPlayerLock인지 확인하고, 비어 있으면 같은 오브젝트에서 찾는다.
        playerLock = playerMovement as IPlayerLock;
        if (playerLock == null) playerLock = GetComponent<IPlayerLock>();
    }

    private void OnValidate()
    {
        // Unity Inspector는 인터페이스 타입 칸을 직접 만들 수 없어서 MonoBehaviour 칸으로 받는다.
        if (playerMovement != null && !(playerMovement is IPlayerLock))
        {
            Debug.LogWarning($"[CameraModeController] '{playerMovement.GetType().Name}'은(는) IPlayerLock을 구현하지 않아 Player Movement에 넣을 수 없습니다.", this);
            playerMovement = null;
        }
    }

    private void Update()
    {
        // 호흡 흔들림: 완전히 고정된 상태에서만, 그리고 옵션이 켜져 있을 때만 아주 약하게 회전을 흔든다.
        if (!IsLockedOnAnchor || !breathingSwayEnabled || mainCamera == null) return;

        float t = (Time.time + breathingSeed) * breathingSwaySpeed;
        float swayX = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * breathingSwayIntensity;
        float swayY = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * breathingSwayIntensity;
        mainCamera.transform.localRotation = lockedLocalRotation * Quaternion.Euler(swayX, swayY, 0f);
    }

    // 네트워크로 스폰되는 플레이어(예: FPSPlayer)는 프리팹 단계에서 씬의 Volume/CanvasGroup 같은
    // 참조를 미리 연결해둘 수 없어서(프리팹은 씬 오브젝트를 참조할 수 없음), 스폰 직후(OnNetworkSpawn 등)에
    // 코드로 찾아서 주입할 방법이 필요하다.
    public void ConfigureForOwner(Volume viewfinderVolume, CanvasGroup viewfinderFrameCanvasGroup)
    {
        this.viewfinderVolume = viewfinderVolume;
        this.viewfinderFrameCanvasGroup = viewfinderFrameCanvasGroup;
        if (viewfinderVolume != null) viewfinderVolume.weight = 0f;
        if (viewfinderFrameCanvasGroup != null) viewfinderFrameCanvasGroup.alpha = 0f;
    }

    // PhotoCaptureSystem이 상호작용 발생 시 호출한다. anchor는 촬영 대상의 CameraAnchor.
    // anchor가 null이면 카메라는 움직이지 않고(시야각도 그대로) 뷰파인더 연출만 켠다 — 단서를 들고 보는 시점 그대로 찍을 때.
    public void EnterCaptureMode(Transform anchor, bool restorePlayerControlOnExit = true)
    {
        if (mainCamera == null) return;
        this.restorePlayerControlOnExit = restorePlayerControlOnExit;

        // 복귀할 때 쓸 원래 로컬 위치/회전을 기억해둔다 (플레이어 자식으로 붙어있으므로 로컬 좌표 기준).
        originalLocalPosition = mainCamera.transform.localPosition;
        originalLocalRotation = mainCamera.transform.localRotation;

        if (playerLock != null)
        {
            // 촬영 중엔 이동/시점을 완전히 멈춘다 (설계 조건: "플레이어 이동/시점 입력 정지").
            playerLock.SpeedMultiplier = 0f;
            playerLock.LookEnabled = false;
        }

        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(TransitionRoutine(toAnchor: true, anchor));
    }

    // 촬영 완료(확정) 또는 취소 시 PhotoCaptureSystem이 호출한다. 원래 시점으로 복귀한다.
    public void ExitCaptureMode()
    {
        if (mainCamera == null) return;

        IsLockedOnAnchor = false;
        mainCamera.transform.localRotation = originalLocalRotation; // 호흡 흔들림으로 틀어진 회전부터 먼저 원위치

        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(TransitionRoutine(toAnchor: false, null));
    }

    private IEnumerator TransitionRoutine(bool toAnchor, Transform anchor)
    {
        IsTransitioning = true;
        IsLockedOnAnchor = false;

        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        // 목표 위치/회전: 들어갈 땐 CameraAnchor(월드 좌표, 앵커가 없으면 지금 자리), 나갈 땐 기억해둔 원래 로컬 좌표를 월드로 환산.
        Vector3 targetPos = toAnchor ? (anchor != null ? anchor.position : startPos) : mainCamera.transform.parent.TransformPoint(originalLocalPosition);
        Quaternion targetRot = toAnchor ? (anchor != null ? anchor.rotation : startRot) : mainCamera.transform.parent.rotation * originalLocalRotation;

        float startWeight = viewfinderVolume != null ? viewfinderVolume.weight : 0f;
        float targetWeight = toAnchor ? 1f : 0f;

        float startAlpha = viewfinderFrameCanvasGroup != null ? viewfinderFrameCanvasGroup.alpha : 0f;
        float targetAlpha = toAnchor ? 1f : 0f;

        float startFov = mainCamera.fieldOfView;
        // 앵커 없이 찍을 땐 시야각을 바꾸지 않는다 (눈앞에 든 단서의 크기가 갑자기 바뀌지 않도록).
        float targetFov = toAnchor ? (anchor != null && captureFov > 0f ? captureFov : startFov) : originalFov;

        float elapsed = 0f;
        while (elapsed < cameraMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cameraMoveDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t); // 등속보다 부드럽게 느껴지도록 가속/감속 곡선을 쓴다.

            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);
            mainCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, smoothT);
            if (viewfinderVolume != null) viewfinderVolume.weight = Mathf.Lerp(startWeight, targetWeight, smoothT);
            if (viewfinderFrameCanvasGroup != null) viewfinderFrameCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothT);

            yield return null;
        }

        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;
        mainCamera.fieldOfView = targetFov;
        if (viewfinderVolume != null) viewfinderVolume.weight = targetWeight;
        if (viewfinderFrameCanvasGroup != null) viewfinderFrameCanvasGroup.alpha = targetAlpha;

        IsTransitioning = false;

        if (toAnchor)
        {
            lockedLocalRotation = mainCamera.transform.localRotation;
            IsLockedOnAnchor = true;
        }
        else if (playerLock != null && restorePlayerControlOnExit)
        {
            // 원래 시점으로 완전히 돌아온 뒤에야 조작을 돌려준다 (전환 도중 조작이 섞이면 어색하므로).
            playerLock.SpeedMultiplier = 1f;
            playerLock.LookEnabled = true;
        }

        transitionRoutine = null;
    }
}
