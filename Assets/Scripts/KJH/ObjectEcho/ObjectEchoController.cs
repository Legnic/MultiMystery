using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 사물에 손을 얹으면 시야가 좁아지고 그 사물의 소리가 들리는 능력(임시 이름: ObjectEcho)의 연출 전체를 지휘한다.
// 흐름(상태): Idle → Reaching(시점 회전 + 손 뻗기) → Narrowing(시야 좁아짐) → Listening(소리 + 스펙트럼)
//            → Returning(시야 복구 + 손 거두기) → Idle(입력 잠금 해제, 완료 이벤트)
//
// ── 왜 코루틴인가 (Awaitable 대신) ──────────────────────────────────────────
//  1) 중단이 쉽다: "연출 도중 중단" 옵션을 켜면 StopCoroutine 한 줄로 진행 중인 단계를 멈추고 복귀 단계로 넘어갈 수 있다.
//     Awaitable(async/await)은 CancellationToken을 모든 단계에 넘기고 예외(OperationCanceledException)를 처리해야 해서
//     초보자가 실수하기 쉽다.
//  2) 수명이 안전하다: 코루틴은 이 컴포넌트/오브젝트가 꺼지면 자동으로 멈춘다. async 함수는 오브젝트가 사라져도
//     계속 돌다가 이미 파괴된 오브젝트를 건드려 오류가 날 수 있다.
//  3) 팀 코드와 통일: InspectableClue, CameraModeController 등 기존 연출이 모두 코루틴이라 읽는 방법이 같다.
//  (Awaitable은 "파일 로딩이 끝나면 다음 줄 실행"처럼 결과 값을 기다리는 작업에 더 잘 맞는다.)
//
// ── 다른 기능 차단 ──────────────────────────────────────────────────────────
//  연출이 시작되면 InteractionController.BeginModal(this)을 호출한다. 기존 공용 규칙에 따라
//   - 다른 상호작용(조준 감지·안내 문구)이 멈추고, E/Esc 입력은 이 컨트롤러로만 들어온다.
//   - 사진 촬영 모드 진입(PhotoSpot의 E)도 이 E가 가로채지므로 불가능하다.
//   - PhotoInventory는 IsModalActive를 보고 Tab 인벤토리 열기를 막는다.
//  그래서 이 클래스는 "차단용 코드"를 따로 갖지 않고 기존 구조를 그대로 재사용한다.
//
// 사용법: Player(InteractionController, PlayerMovement가 있는 오브젝트)에 붙인다.
//         Hand Reach 칸에 손 동작(PlaceholderHandReach 등 IHandReach 구현)을, Sound Cue 칸에 SoundCue를 연결한다.
public class ObjectEchoController : MonoBehaviour, IModalInteraction
{
    public enum State { Idle, Reaching, Narrowing, Listening, Returning }

    [Header("참조 (비워두면 자동으로 찾음)")]
    [Tooltip("공용 상호작용 컨트롤러. 연출 중 다른 상호작용/인벤토리/촬영을 막는 데 쓴다. 비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private InteractionController interactionController;

    [Tooltip("이동/시점 회전을 멈추고, 시점을 사물 쪽으로 돌리는 데 쓴다. 비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("FOV를 바꿀 플레이어 카메라. 비워두면 InteractionController의 카메라(없으면 Camera.main)를 쓴다.")]
    [SerializeField] private Camera viewCamera;

    [Tooltip("손 동작 담당 (IHandReach를 구현한 컴포넌트: 지금은 PlaceholderHandReach, 나중엔 IK 버전). 비워두면 자식에서 찾는다.")]
    [SerializeField] private MonoBehaviour handReach;

    [Tooltip("소리 재생 + 스펙트럼 표시 담당. 비워두면 자식에서 찾는다.")]
    [SerializeField] private SoundCue soundCue;

    [Tooltip("비네팅/채도를 줄 Volume. 비워두면 실행 시 전용 Global Volume을 자동으로 만든다. " +
             "연결한 경우에도 원본 프로필 에셋은 건드리지 않고 실행 중 복사본에 아래 수치를 적용한다.")]
    [SerializeField] private Volume echoVolume;

    [Tooltip("자동으로 만드는 Volume의 우선순위. 다른 Volume보다 높아야 이 효과가 위에 덮인다.")]
    [SerializeField] private float autoVolumePriority = 20f;

    [Header("1. 손 뻗기")]
    [Tooltip("카메라가 FocusPoint 쪽으로 돌아가는 시간 (초).")]
    [SerializeField] private float cameraTurnDuration = 0.9f;

    [Tooltip("카메라 회전이 시작된 뒤 손이 출발하기까지 기다리는 시간 (초). 먼저 바라보고, 그다음 손을 뻗는 순서감.")]
    [SerializeField] private float handReachDelay = 0.25f;

    [Tooltip("손이 HandTarget까지 가는 시간 (초). 천천히 조심스럽게 얹는 느낌이 나도록 길게.")]
    [SerializeField] private float handReachDuration = 1.3f;

    [Tooltip("카메라 회전 곡선 (가로 0~1 시간, 세로 0~1 진행도).")]
    [SerializeField] private AnimationCurve turnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("2. 시야 좁아짐")]
    [Tooltip("시야가 좁아지는 데 걸리는 시간 (초).")]
    [SerializeField] private float narrowDuration = 1.2f;

    [Tooltip("최종 비네팅 강도 (0~1). 0.4~0.5 정도가 '집중'하는 느낌, 그 이상은 답답하다.")]
    [Range(0f, 1f)]
    [SerializeField] private float vignetteIntensity = 0.45f;

    [Tooltip("비네팅 가장자리의 부드러움 (0~1). 클수록 경계가 흐릿하다.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float vignetteSmoothness = 0.5f;

    [Tooltip("비네팅 색. 완전한 검정보다 짙은 갈색(먹색)이 1890년대 사진 톤에 어울린다.")]
    [SerializeField] private Color vignetteColor = new Color(0.08f, 0.05f, 0.03f);

    [Tooltip("좁아졌을 때의 시야각(FOV). 원래 값(보통 60)에서 이 값으로 서서히 바뀐다.")]
    [SerializeField] private float narrowedFov = 52f;

    [Tooltip("(선택) 채도도 함께 낮출지.")]
    [SerializeField] private bool reduceSaturation = true;

    [Tooltip("낮출 채도 값 (-100 = 흑백, 0 = 변화 없음). 살짝만 낮춰야 절제된 느낌이 난다.")]
    [Range(-100f, 0f)]
    [SerializeField] private float saturation = -25f;

    [Tooltip("(선택) 환경음 볼륨을 함께 낮출지. 기본 켜짐. AudioMixer가 비어 있으면 자동으로 건너뛴다.")]
    [SerializeField] private bool duckAmbient = true;

    [Tooltip("환경음 그룹이 있는 AudioMixer.")]
    [SerializeField] private AudioMixer ambientMixer;

    [Tooltip("AudioMixer에서 Expose한 환경음 볼륨 파라미터 이름 (dB 단위).")]
    [SerializeField] private string ambientVolumeParameter = "AmbientVolume";

    [Tooltip("환경음을 얼마나 낮출지 (dB, 음수). -12dB ≈ 체감상 절반 이하.")]
    [Range(-40f, 0f)]
    [SerializeField] private float ambientDuckDb = -12f;

    [Tooltip("시야 좁아짐/복구에 쓰는 곡선.")]
    [SerializeField] private AnimationCurve blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("3. 소리 + 스펙트럼")]
    [Tooltip("시야가 다 좁아진 뒤 소리가 나기까지의 정적 (초). 잠깐의 침묵이 긴장감을 만든다.")]
    [SerializeField] private float delayBeforeSound = 0.4f;

    [Tooltip("소리가 끝난 뒤 복귀를 시작하기까지 기다리는 시간 (초). 여운.")]
    [SerializeField] private float delayAfterSound = 0.5f;

    [Header("4. 복귀")]
    [Tooltip("시야/FOV/채도/환경음이 원래대로 돌아오는 시간 (초).")]
    [SerializeField] private float restoreDuration = 1.6f;

    [Tooltip("복귀 시작 후 손을 거두기 시작하기까지 기다리는 시간 (초).")]
    [SerializeField] private float handRetractDelay = 0.3f;

    [Tooltip("손을 원위치로 거두는 시간 (초).")]
    [SerializeField] private float handRetractDuration = 1.1f;

    [Header("중단")]
    [Tooltip("연출 도중 E/Esc로 중단할 수 있는지. 기본은 중단 불가 (끝까지 듣게 함). 중단하면 완료 이벤트는 발생하지 않는다.")]
    [SerializeField] private bool allowInterrupt = false;

    [Tooltip("중단이 가능할 때, 듣는 동안 보여줄 안내 문구.")]
    [SerializeField] private string interruptPrompt = "[E] 손을 뗀다";

    // ── 런타임 상태 ─────────────────────────────────────────
    public State CurrentState { get; private set; } = State.Idle;
    public bool IsBusy => CurrentState != State.Idle;
    public ObjectEchoTarget CurrentTarget { get; private set; }

    // 다른 시스템(사운드 디자인, 튜토리얼 등)이 코드로 구독할 수 있는 알림.
    public event Action<ObjectEchoTarget> EchoStarted;
    // bool = 끝까지 들었는지 (중단되면 false).
    public event Action<ObjectEchoTarget, bool> EchoFinished;

    private IHandReach hand;
    private Vignette vignette;
    private ColorAdjustments colorAdjustments;

    private Coroutine mainRoutine;
    private Coroutine turnRoutine;

    // 연출 시작 시점의 원래 값 (복구할 때 이 값으로 돌아간다).
    private float originalFov;
    private float originalAmbientDb;
    private bool ambientAvailable;
    private float originalSpeedMultiplier = 1f;
    private bool originalLookEnabled = true;

    // 0 = 평소 시야, 1 = 완전히 좁아진 시야. 비네팅·FOV·채도·환경음이 모두 이 값 하나로 함께 움직인다.
    // 값 하나로 묶어 두면, 중간에 중단돼도 "지금 값에서" 그대로 되돌리면 되어 꼬이지 않는다.
    private float narrowBlend;

    private void Awake()
    {
        if (interactionController == null) interactionController = GetComponent<InteractionController>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (viewCamera == null) viewCamera = interactionController != null && interactionController.LookCamera != null ? interactionController.LookCamera : Camera.main;
        if (soundCue == null) soundCue = GetComponentInChildren<SoundCue>(true);

        // 손 동작: Inspector에 연결된 컴포넌트가 IHandReach인지 확인하고, 비어 있으면 자식에서 찾는다.
        hand = handReach as IHandReach;
        if (hand == null) hand = GetComponentInChildren<IHandReach>(true);

        SetupVolume();
    }

    private void OnValidate()
    {
        // Unity Inspector는 인터페이스 타입 칸을 직접 만들 수 없어서 MonoBehaviour 칸으로 받는다.
        // 대신 IHandReach가 아닌 컴포넌트를 넣으면 바로 알려주고 비운다.
        if (handReach != null && !(handReach is IHandReach))
        {
            Debug.LogWarning($"[ObjectEchoController] '{handReach.GetType().Name}'은(는) IHandReach를 구현하지 않아 Hand Reach에 넣을 수 없습니다.", this);
            handReach = null;
        }
    }

    // ── 시작 ───────────────────────────────────────────────

    // ObjectEchoTarget이 E 입력을 받으면 호출한다. 시작했으면 true.
    public bool TryBegin(ObjectEchoTarget target)
    {
        if (target == null || IsBusy) return false;
        // 이미 다른 독점 상호작용(살펴보기 등)이 진행 중이면 시작하지 않는다.
        if (interactionController != null && interactionController.IsModalActive) return false;
        if (viewCamera == null)
        {
            Debug.LogWarning("[ObjectEchoController] 연출에 쓸 카메라가 없습니다.", this);
            return false;
        }

        CurrentTarget = target;
        mainRoutine = StartCoroutine(EchoRoutine(target));
        return true;
    }

    // ── 연출 본체 ──────────────────────────────────────────

    private IEnumerator EchoRoutine(ObjectEchoTarget target)
    {
        LockPlayer();
        target.NotifyStarted();
        CaptureOriginals();
        ApplyEffectSettings(); // Inspector에서 바꾼 수치를 매번 시작할 때 반영 (플레이 중 조정 가능)
        EchoStarted?.Invoke(target);

        // ── 2단계: 손 뻗기 (카메라 회전과 손 이동을 살짝 겹쳐서 진행) ──
        CurrentState = State.Reaching;
        turnRoutine = StartCoroutine(TurnCameraRoutine(target.FocusPoint.position, cameraTurnDuration));

        bool handArrived = hand == null; // 손이 없으면 손 단계는 건너뛴다
        yield return WaitUnscaled(handReachDelay);
        if (hand != null) hand.ReachTo(target.HandTarget, handReachDuration, () => handArrived = true);
        while (!handArrived || turnRoutine != null) yield return null;

        // ── 3단계: 시야 좁아짐 ──
        CurrentState = State.Narrowing;
        yield return AnimateBlend(1f, narrowDuration);

        // ── 4단계: 소리 + 스펙트럼 ──
        CurrentState = State.Listening;
        if (allowInterrupt) ShowPrompt(interruptPrompt);
        yield return WaitUnscaled(delayBeforeSound);

        if (soundCue != null && target.EchoClip != null)
        {
            soundCue.Play(target.EchoClip, target.Volume, target.SpectrumIntensity, target.SpectrumResponse);
            while (soundCue.IsPlaying) yield return null; // 소리가 끝나면 SoundCue가 스펙트럼 1초 페이드아웃을 시작한다
        }
        else
        {
            Debug.LogWarning($"[ObjectEchoController] '{target.name}'에 소리(Echo Clip)가 없거나 SoundCue가 연결되지 않아 소리 없이 진행합니다.", target);
            yield return WaitUnscaled(1f);
        }
        yield return WaitUnscaled(delayAfterSound);

        // ── 5단계: 복귀 (끝까지 들음) ──
        mainRoutine = StartCoroutine(ReturnRoutine(target, true));
    }

    // 시야/손을 원래대로 되돌리고 잠금을 푼다. 정상 종료와 중단 모두 여기로 모인다.
    private IEnumerator ReturnRoutine(ObjectEchoTarget target, bool completed)
    {
        CurrentState = State.Returning;
        HidePrompt();

        // 시야 복구와 손 거두기를 함께 진행한다 (따로 순서대로 하면 너무 길게 늘어진다).
        bool handReturned = hand == null || !hand.IsExtended;
        StartCoroutine(RetractHandAfterDelay(() => handReturned = true));
        yield return AnimateBlend(0f, restoreDuration);
        while (!handReturned) yield return null;

        // 혹시 남아 있을 효과를 정확히 0으로 맞추고 잠금 해제.
        ApplyBlend(0f);
        UnlockPlayer();
        CurrentState = State.Idle;
        CurrentTarget = null;
        mainRoutine = null;

        // 완료 이벤트는 모든 것이 원래대로 돌아온 "다음"에 보낸다 (퍼즐 로직이 플레이어를 움직여도 안전하도록).
        target.NotifyFinished(completed);
        EchoFinished?.Invoke(target, completed);
    }

    private IEnumerator RetractHandAfterDelay(Action onDone)
    {
        if (hand == null || !hand.IsExtended) { onDone(); yield break; }
        yield return WaitUnscaled(handRetractDelay);
        hand.Retract(handRetractDuration, onDone);
    }

    // 카메라(정확히는 플레이어 몸통 yaw + 카메라 pitch)를 응시 지점 쪽으로 부드럽게 돌린다.
    private IEnumerator TurnCameraRoutine(Vector3 focusPosition, float duration)
    {
        Transform cam = viewCamera.transform;
        Vector3 dir = focusPosition - cam.position;
        if (dir.sqrMagnitude < 0.0001f) { turnRoutine = null; yield break; }

        // 목표 각도: 좌우는 수평 방향의 각도, 위아래는 수평선 대비 기울기 (아래를 볼수록 pitch가 +).
        float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float targetPitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;

        float startYaw = playerMovement != null ? playerMovement.Yaw : cam.eulerAngles.y;
        float startPitch = playerMovement != null ? playerMovement.Pitch : Mathf.DeltaAngle(0f, cam.eulerAngles.x);
        Quaternion startRot = cam.rotation;
        Quaternion endRot = Quaternion.LookRotation(dir);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = turnCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            if (playerMovement != null)
            {
                // PlayerMovement를 통해 돌려야 잠금 해제 후에도 그 방향을 계속 보고 있다 (내부 pitch 값까지 갱신).
                // LerpAngle: 350도 → 10도처럼 0도를 넘어갈 때 한 바퀴 돌지 않고 가까운 쪽으로 돈다.
                playerMovement.SetLookAngles(Mathf.LerpAngle(startYaw, targetYaw, t), Mathf.Lerp(startPitch, targetPitch, t));
            }
            else
            {
                cam.rotation = Quaternion.Slerp(startRot, endRot, t);
            }
            yield return null;
        }
        turnRoutine = null;
    }

    // narrowBlend를 지금 값에서 target까지 duration초 동안 곡선으로 바꾼다.
    private IEnumerator AnimateBlend(float target, float duration)
    {
        float start = narrowBlend;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyBlend(Mathf.Lerp(start, target, blendCurve.Evaluate(Mathf.Clamp01(elapsed / duration))));
            yield return null;
        }
        ApplyBlend(target);
    }

    // ── 효과 적용 ──────────────────────────────────────────

    // 0~1 값 하나로 비네팅·채도(Volume weight), FOV, 환경음을 한꺼번에 맞춘다.
    private void ApplyBlend(float blend)
    {
        narrowBlend = blend;
        // Volume의 weight를 0→1로 올리면 프로필에 적힌 비네팅/채도 값이 그 비율만큼 섞여 들어간다.
        if (echoVolume != null) echoVolume.weight = blend;
        if (viewCamera != null) viewCamera.fieldOfView = Mathf.Lerp(originalFov, narrowedFov, blend);
        if (ambientAvailable) ambientMixer.SetFloat(ambientVolumeParameter, originalAmbientDb + ambientDuckDb * blend);
    }

    // 실행 시 Volume과 효과 항목(비네팅, 색 조정)을 준비한다.
    private void SetupVolume()
    {
        if (echoVolume == null)
        {
            // 씬에 따로 만들지 않아도 동작하도록, 전용 Global Volume을 자식으로 만든다.
            GameObject go = new GameObject("ObjectEcho_Volume (Runtime)");
            go.transform.SetParent(transform, false);
            echoVolume = go.AddComponent<Volume>();
            echoVolume.isGlobal = true;
            echoVolume.priority = autoVolumePriority;
            // 실행 중에만 쓰는 프로필. 에셋 파일로 저장되지 않으므로 다른 씬/프로필에 영향이 없다.
            echoVolume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        }

        // volume.profile은 원본 에셋을 직접 바꾸지 않도록 "실행 중 복사본"을 돌려준다 (에디터에서 에셋이 바뀌는 사고 방지).
        VolumeProfile profile = echoVolume.profile;
        // Add(..., false): 모든 항목을 덮어쓰지 않고, 아래에서 Override한 값(강도/채도 등)만 적용되게 한다.
        if (!profile.TryGet(out vignette)) vignette = profile.Add<Vignette>(false);
        if (!profile.TryGet(out colorAdjustments)) colorAdjustments = profile.Add<ColorAdjustments>(false);
        echoVolume.weight = 0f;
    }

    // Inspector 수치를 Volume 효과에 반영한다.
    private void ApplyEffectSettings()
    {
        if (vignette != null)
        {
            vignette.active = true;
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(vignetteSmoothness);
            vignette.color.Override(vignetteColor);
        }
        if (colorAdjustments != null)
        {
            colorAdjustments.active = reduceSaturation;
            colorAdjustments.saturation.Override(saturation);
        }
    }

    // 시작 순간의 원래 값을 저장한다 (연출 후 정확히 이 값으로 되돌리기 위해).
    private void CaptureOriginals()
    {
        originalFov = viewCamera.fieldOfView;

        ambientAvailable = false;
        if (duckAmbient && ambientMixer != null && !string.IsNullOrEmpty(ambientVolumeParameter))
        {
            // GetFloat이 false면 파라미터가 Expose되지 않은 것 → 경고만 하고 환경음 단계는 건너뛴다.
            ambientAvailable = ambientMixer.GetFloat(ambientVolumeParameter, out originalAmbientDb);
            if (!ambientAvailable)
            {
                Debug.LogWarning($"[ObjectEchoController] AudioMixer '{ambientMixer.name}'에 Expose된 파라미터 '{ambientVolumeParameter}'가 없어 환경음 줄이기를 건너뜁니다.", this);
            }
        }
    }

    // ── 입력 잠금 ──────────────────────────────────────────

    private void LockPlayer()
    {
        if (playerMovement != null)
        {
            originalSpeedMultiplier = playerMovement.SpeedMultiplier;
            originalLookEnabled = playerMovement.LookEnabled;
            playerMovement.SpeedMultiplier = 0f;
            playerMovement.LookEnabled = false;
        }
        // 공용 독점 상호작용 규칙: 다른 상호작용·Tab 인벤토리·촬영 진입이 막히고 E/Esc가 이쪽으로 온다.
        if (interactionController != null) interactionController.BeginModal(this);
    }

    private void UnlockPlayer()
    {
        if (playerMovement != null)
        {
            playerMovement.SpeedMultiplier = originalSpeedMultiplier;
            playerMovement.LookEnabled = originalLookEnabled;
        }
        if (interactionController != null) interactionController.EndModal(this);
    }

    // ── IModalInteraction (연출 중 E / Esc) ────────────────

    public void OnInteractPressed() => TryInterrupt();
    public void OnCancelPressed() => TryInterrupt();

    // 중단 옵션이 켜져 있을 때만, 복귀 단계가 아니라면 지금 상태에서 곧장 복귀한다.
    private void TryInterrupt()
    {
        if (!allowInterrupt) return; // 기본값: 중단 불가 → 입력 무시
        if (CurrentState == State.Idle || CurrentState == State.Returning) return;

        if (mainRoutine != null) StopCoroutine(mainRoutine);
        if (turnRoutine != null) { StopCoroutine(turnRoutine); turnRoutine = null; }
        if (soundCue != null) soundCue.Stop(); // 소리를 짧게 줄여 끊고, 스펙트럼도 페이드아웃
        mainRoutine = StartCoroutine(ReturnRoutine(CurrentTarget, false));
    }

    // ── 안내 문구 (공용 InteractionPromptUI 재사용) ─────────

    private void ShowPrompt(string text)
    {
        if (interactionController != null && interactionController.PromptUI != null && !string.IsNullOrEmpty(text))
            interactionController.PromptUI.Show(text);
    }

    private void HidePrompt()
    {
        if (interactionController != null && interactionController.PromptUI != null)
            interactionController.PromptUI.Hide();
    }

    // ── 보조 ───────────────────────────────────────────────

    // 일시정지(timeScale = 0)와 무관하게 기다린다 (다른 연출 코드와 같은 규칙).
    private static IEnumerator WaitUnscaled(float seconds)
    {
        if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
    }

    // 연출 도중 Player가 꺼지거나 씬이 바뀌면 코루틴이 멈춘다. 화면 효과/잠금이 남지 않도록 즉시 정리한다.
    private void OnDisable()
    {
        if (!IsBusy) return;
        StopAllCoroutines();
        mainRoutine = null;
        turnRoutine = null;
        if (soundCue != null) soundCue.Stop();
        if (hand != null) hand.SnapToRest();
        ApplyBlend(0f);
        HidePrompt();
        UnlockPlayer();
        ObjectEchoTarget target = CurrentTarget;
        CurrentState = State.Idle;
        CurrentTarget = null;
        if (target != null) target.NotifyFinished(false);
    }
}
