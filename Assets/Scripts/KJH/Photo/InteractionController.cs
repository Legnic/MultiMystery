using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어 쪽에서 "지금 상호작용할 수 있는 대상이 있는가"를 관리하고, E키 입력을 받아
// 그 대상의 Interact()를 실행하는 공용 컨트롤러. PhotoSpot뿐 아니라 나중에 만들
// 서랍/상자 같은 IInteractable도 전부 이 컨트롤러 하나로 처리된다.
// 사용법: Player 오브젝트에 붙인다 (같은 오브젝트에 PhotoCaptureSystem도 있어야 한다).
public class InteractionController : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("촬영 모드 중인지 확인하기 위한 참조. 촬영 모드 중에는 E를 '촬영'으로, Esc를 '취소'로 쓴다.")]
    [SerializeField] private PhotoCaptureSystem photoCaptureSystem;

    [Tooltip("화면에 '[E] ...' 안내 문구를 보여주는 UI.")]
    [SerializeField] private InteractionPromptUI promptUI;

    [Header("입력 액션")]
    [Tooltip("상호작용 입력 (Player/Interact, 기본 E키). 키 단순화: 평소엔 '상호작용 시작', 촬영 모드 중엔 '촬영 확정'으로 쓴다.")]
    [SerializeField] private InputActionReference interactAction;

    [Tooltip("취소/닫기 입력 (Photo/ClosePhoto, 기본 Esc). 촬영 모드 중엔 '촬영 취소(모드 나가기)' 용도로 쓴다.")]
    [SerializeField] private InputActionReference cancelAction;

    // 지금 트리거 범위 안에 들어와 있는 상호작용 대상. 범위를 벗어나면 null로 돌아간다.
    private IInteractable currentInteractable;

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

    // PhotoSpot 등 IInteractable이 트리거에 플레이어가 들어왔을 때 호출한다.
    public void RegisterInteractable(IInteractable interactable)
    {
        // 촬영 모드 중에는 다른 지점의 안내 문구가 뜨면 혼란스러우니 무시한다.
        if (photoCaptureSystem != null && photoCaptureSystem.IsBusy) return;

        currentInteractable = interactable;
        if (promptUI != null) promptUI.Show(interactable.InteractPrompt);
    }

    // 트리거를 벗어나거나, 촬영이 끝나 더 이상 상호작용 대상이 아니게 됐을 때 호출한다.
    public void ClearInteractable(IInteractable interactable)
    {
        if (currentInteractable != interactable) return; // 이미 다른 대상으로 바뀐 뒤의 뒤늦은 호출은 무시

        currentInteractable = null;
        if (promptUI != null) promptUI.Hide();
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

        if (currentInteractable != null && currentInteractable.CanInteract)
        {
            currentInteractable.Interact(gameObject);
        }
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        // Esc는 촬영 모드 중일 때만 취소(모드 나가기)로 동작한다. 평소엔 PhotoInventory가 같은
        // 액션으로 인벤토리/확대보기를 닫는 용도로 따로 쓰고 있어서 서로 상태 체크만 하고 간섭하지 않는다.
        if (photoCaptureSystem != null && photoCaptureSystem.IsBusy)
        {
            photoCaptureSystem.CancelCapture();
        }
    }
}
