using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 쪽에서 "지금 상호작용할 수 있는 대상이 있는가"를 관리하고, E키 입력을 받아
// 그 대상의 Interact()를 실행하는 공용 컨트롤러. PhotoSpot뿐 아니라 나중에 만들
// 서랍/상자 같은 IInteractable도 전부 이 컨트롤러 하나로 처리된다.
// 대상을 찾는 방법은 두 가지다.
//   1) 트리거 방식: PhotoSpot처럼 범위에 들어오면 대상이 스스로 RegisterInteractable을 호출한다.
//   2) 바라보기 방식: 매 프레임 화면 가운데(조준점)에서 레이를 쏴서 맞은 IInteractable을 찾는다 (단서 등).
// 둘 다 해당되면 "조준점이 가리키는 대상"을 우선한다 (PhotoSpot 안에서 단서를 바라보면 단서 우선).
// 능력 필터: 대상이 IRequiresAbility로 "필요한 능력"을 밝혔는데 이 플레이어(PlayerAbilities)에게 그 능력이 없으면,
// 그 대상은 이 플레이어에게 "없는 것"으로 취급한다 (안내 문구·강조·E 반응 모두 없음).
// 예: 플레이어 A(SoundEcho)에게는 사진 촬영 지점이, 플레이어 B(PastPhoto)에게는 소리 듣기 사물이 반응하지 않는다.
// 조건 필터: 대상 오브젝트에 IInteractionCondition(예: TimeUnlock)이 붙어 있고 그 조건을 만족하지 못해도 같은 방식으로 "없는 것"으로 취급한다.
// 조건은 실행 중에 바뀔 수 있으므로(시간대가 바뀜) 매 프레임 다시 확인한다.
// 사용법: Player 오브젝트에 붙인다 (같은 오브젝트에 PhotoCaptureSystem도 있어야 한다).
public class InteractionController : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("촬영 모드 중인지 확인하기 위한 참조. 촬영 모드 중에는 E를 '촬영'으로, Esc를 '취소'로 쓴다.")]
    [SerializeField] private PhotoCaptureSystem photoCaptureSystem;

    [Tooltip("화면에 '[E] ...' 안내 문구를 보여주는 UI.")]
    [SerializeField] private InteractionPromptUI promptUI;

    [Tooltip("인벤토리가 열려 있는 동안엔 바라보기 감지를 멈추기 위한 참조. 비워두면 같은 오브젝트에서 자동으로 찾는다.")]
    [SerializeField] private PhotoInventory photoInventory;

    [Header("입력 액션")]
    [Tooltip("상호작용 입력 (Player/Interact, 기본 E키). 키 단순화: 평소엔 '상호작용 시작', 촬영 모드 중엔 '촬영 확정'으로 쓴다.")]
    [SerializeField] private InputActionReference interactAction;

    [Tooltip("취소/닫기 입력 (Photo/ClosePhoto, 기본 Esc). 촬영 모드 중엔 '촬영 취소(모드 나가기)' 용도로 쓴다.")]
    [SerializeField] private InputActionReference cancelAction;

    [Header("바라보기 감지 (레이캐스트)")]
    [Tooltip("조준점 레이를 쏠 카메라. 비워두면 Camera.main을 쓴다.")]
    [SerializeField] private Camera lookCamera;

    [Tooltip("조준점으로 대상을 감지하는 최대 거리 (m). 기본 2m.")]
    [SerializeField] private float maxLookDistance = 2f;

    [Tooltip("레이가 맞을 수 있는 레이어. Player 레이어는 빼야 자기 몸에 맞지 않는다. 벽(Default)도 포함해야 벽 너머 단서가 감지되지 않는다.")]
    [SerializeField] private LayerMask lookLayerMask = 1; // 1 = Default 레이어만

    // 트리거 범위 안에 들어와 있는 상호작용 대상 (PhotoSpot 등). 범위를 벗어나면 null로 돌아간다.
    private IInteractable triggerInteractable;
    // 조준점이 지금 가리키고 있는 상호작용 대상 (단서 등). 안 가리키면 null.
    private IInteractable lookInteractable;
    // 같은 콜라이더를 계속 바라볼 때 매 프레임 GetComponentInParent를 하지 않도록 캐시해 둔다.
    private Collider lastLookCollider;
    private IInteractable lastLookColliderTarget;
    // 트리거 대상이 지금 이 플레이어에게 보이는지 (능력·조건을 만족하는지). 바뀌는 순간에만 안내 문구를 갱신하려고 기억해 둔다.
    private bool triggerAvailable;
    // 대상의 조건 컴포넌트를 찾을 때 매번 새 리스트를 만들지 않도록 재사용한다 (매 프레임 호출되므로).
    private readonly List<IInteractionCondition> conditionBuffer = new List<IInteractionCondition>();
    // 지금 화면을 독차지하는 상호작용 (살펴보기 등). null이면 평소 상태.
    private IModalInteraction activeModal;

    // 살펴보기 같은 독점 상호작용이 진행 중인지. PhotoInventory가 이 값을 보고 Tab 인벤토리 열기를 막는다.
    public bool IsModalActive => activeModal != null;

    // 독점 상호작용 쪽에서 "[E] 내려놓기" 같은 안내를 띄울 때 같은 UI를 재사용하기 위해 공개한다.
    public InteractionPromptUI PromptUI => promptUI;

    // 살펴보기 대상이 "카메라 앞" 위치를 계산할 때 쓰는 카메라.
    public Camera LookCamera => lookCamera;

    // 촬영 중 / 인벤토리 열림 / 살펴보기 중에는 바라보기 감지를 하지 않는다.
    private bool IsLookDetectionBlocked =>
        (photoCaptureSystem != null && photoCaptureSystem.IsBusy) ||
        (photoInventory != null && photoInventory.IsOpen) ||
        activeModal != null;

    private void Awake()
    {
        // 비워둔 참조는 자동으로 채운다 (Inspector 설정을 깜빡해도 동작하도록).
        if (photoInventory == null) photoInventory = GetComponent<PhotoInventory>();
        if (lookCamera == null) lookCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.performed += OnInteractPerformed;
            interactAction.action.Enable();
        }
        if (cancelAction != null)
        {
            cancelAction.action.performed += OnCancelPerformed;
            cancelAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (interactAction != null) interactAction.action.performed -= OnInteractPerformed;
        if (cancelAction != null) cancelAction.action.performed -= OnCancelPerformed;
    }

    private void Update()
    {
        // 매 프레임 조준점이 가리키는 대상을 찾는다. 감지가 막힌 상태면 "아무것도 안 가리킴"으로 처리한다.
        IInteractable target = IsLookDetectionBlocked ? null : FindLookTarget();
        SetLookTarget(target);

        // 트리거 범위 안에 있는 동안 조건이 바뀌면(예: 시간대가 바뀌어 열림) 범위를 다시 드나들지 않아도 안내 문구가 바로 바뀌게 한다.
        bool available = triggerInteractable != null && IsAllowed(triggerInteractable);
        if (available != triggerAvailable)
        {
            triggerAvailable = available;
            RefreshPrompt();
        }
    }

    // 화면 가운데에서 앞으로 레이를 쏴서 맞은 콜라이더의 부모 쪽에서 IInteractable을 찾는다.
    private IInteractable FindLookTarget()
    {
        if (lookCamera == null) return null;

        Transform cam = lookCamera.transform;
        // 트리거 콜라이더(PhotoSpot 범위 등)는 무시한다: 투명한 범위 상자에 레이가 막히면 안 되기 때문.
        if (!Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, maxLookDistance, lookLayerMask, QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        if (hit.collider != lastLookCollider)
        {
            lastLookCollider = hit.collider;
            // 콜라이더가 자식(메시 오브젝트)에 있어도 부모(단서 루트)의 컴포넌트를 찾을 수 있게 InParent를 쓴다.
            lastLookColliderTarget = hit.collider.GetComponentInParent<IInteractable>();
        }
        return lastLookColliderTarget;
    }

    // 조준 대상이 바뀌었을 때만 강조 알림과 안내 문구를 갱신한다 (매 프레임 갱신하면 페이드가 계속 다시 시작됨).
    private void SetLookTarget(IInteractable target)
    {
        if (target != null && (!target.CanInteract || !IsAllowed(target))) target = null;
        if (target == lookInteractable) return;

        if (lookInteractable is IFocusable oldFocus) oldFocus.OnFocusExit();
        lookInteractable = target;
        if (lookInteractable is IFocusable newFocus) newFocus.OnFocusEnter();

        RefreshPrompt();
    }

    // 이 플레이어가 지금 이 대상과 상호작용할 수 있는지.
    // ① 능력: 능력이 필요 없는 대상(단서 등)은 통과 ② 조건: 대상 오브젝트에 붙은 IInteractionCondition을 모두 만족해야 통과 (없으면 통과)
    private bool IsAllowed(IInteractable target)
    {
        if (target is IRequiresAbility requirement && !PlayerAbilities.Has(gameObject, requirement.RequiredAbility)) return false;

        if (target is Component component)
        {
            component.GetComponents(conditionBuffer);
            foreach (IInteractionCondition condition in conditionBuffer)
            {
                if (!condition.IsMet(gameObject)) return false;
            }
        }
        return true;
    }

    // 지금 E를 누르면 실행될 대상. 조준 대상이 트리거 대상보다 우선이다.
    private IInteractable CurrentTarget => lookInteractable ?? (triggerAvailable ? triggerInteractable : null);

    // 현재 대상에 맞게 안내 문구를 보여주거나 숨긴다.
    private void RefreshPrompt()
    {
        if (promptUI == null) return;
        if (activeModal != null) return; // 살펴보기 중엔 그쪽이 직접 안내 문구를 관리한다

        IInteractable target = CurrentTarget;
        if (target != null) promptUI.Show(target.InteractPrompt);
        else promptUI.Hide();
    }

    // PhotoSpot 등 IInteractable이 트리거에 플레이어가 들어왔을 때 호출한다.
    public void RegisterInteractable(IInteractable interactable)
    {
        // 촬영 모드 중에는 다른 지점의 안내 문구가 뜨면 혼란스러우니 무시한다.
        if (photoCaptureSystem != null && photoCaptureSystem.IsBusy) return;
        // 능력·조건이 맞지 않는 대상(예: 플레이어 A가 사진 촬영 지점 범위에 들어옴, 아직 열리지 않은 시간대)도 기억은 해 두되
        // 안내 문구는 띄우지 않는다. 범위 안에 있는 동안 조건이 맞게 되면(시간대가 바뀜) Update에서 그때 안내 문구를 띄운다.
        triggerInteractable = interactable;
        triggerAvailable = IsAllowed(interactable);
        RefreshPrompt();
    }

    // 트리거를 벗어나거나, 촬영이 끝나 더 이상 상호작용 대상이 아니게 됐을 때 호출한다.
    public void ClearInteractable(IInteractable interactable)
    {
        if (triggerInteractable != interactable) return; // 이미 다른 대상으로 바뀐 뒤의 뒤늦은 호출은 무시

        triggerInteractable = null;
        triggerAvailable = false;
        RefreshPrompt();
    }

    // 살펴보기 같은 독점 상호작용이 시작될 때 호출한다. 감지/안내를 멈추고 E/Esc를 그쪽으로 넘긴다.
    public void BeginModal(IModalInteraction modal)
    {
        activeModal = modal;
        SetLookTarget(null); // 강조를 끄고 조준 대상을 비운다 (activeModal이 있어 안내 문구는 건드리지 않음)
        if (promptUI != null) promptUI.Hide();
    }

    // 독점 상호작용이 끝났을 때 호출한다. 다음 프레임부터 다시 바라보기 감지가 시작된다.
    public void EndModal(IModalInteraction modal)
    {
        if (activeModal != modal) return;
        activeModal = null;
        RefreshPrompt(); // 아직 PhotoSpot 범위 안이라면 그 안내 문구를 다시 보여준다
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        // 키 단순화: 촬영 모드 중이면 E키는 "상호작용 시작"이 아니라 "촬영 확정"으로 동작한다.
        // (진입 E, 촬영 E, 취소 Esc로 통일 — 더 이상 별도의 촬영 전용 키는 쓰지 않는다)
        if (photoCaptureSystem != null && photoCaptureSystem.IsBusy)
        {
            photoCaptureSystem.TryCapture();
            return;
        }

        // 살펴보기 중이면 E는 "내려놓기"로 쓰이므로 그쪽에 그대로 넘긴다.
        if (activeModal != null)
        {
            activeModal.OnInteractPressed();
            return;
        }

        IInteractable target = CurrentTarget;
        if (target != null && target.CanInteract && IsAllowed(target))
        {
            target.Interact(gameObject);
        }
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        // Esc는 촬영 모드 중일 때만 취소(모드 나가기)로 동작한다. 평소엔 PhotoInventory가 같은
        // 액션으로 인벤토리/확대보기를 닫는 용도로 따로 쓰고 있어서 서로 상태 체크만 하고 간섭하지 않는다.
        if (photoCaptureSystem != null && photoCaptureSystem.IsBusy)
        {
            photoCaptureSystem.CancelCapture();
            return;
        }

        // 살펴보기 중이면 Esc도 "내려놓기"로 넘긴다 (기존 Esc = 취소/나가기 규칙과 같은 의미).
        if (activeModal != null)
        {
            activeModal.OnCancelPressed();
        }
    }
}
