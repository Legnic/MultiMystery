using UnityEngine;

// "정해진 위치에서만 촬영 가능한" 사진 촬영 지점. 트리거 영역에 플레이어가 들어오면
// IInteractable을 통해 InteractionController에 자신을 등록하고, E키를 누르면
// PhotoCaptureSystem에 촬영을 요청한다. 한 번 촬영하면 다시 촬영할 수 없다.
// 사용법: 트리거로 쓸 Collider(예: BoxCollider, Is Trigger 체크)가 있는 빈 오브젝트에 붙인다.
// Rigidbody가 반드시 필요한 이유: 트리거 콜백(OnTriggerEnter/Exit)은 두 Collider 중
// 적어도 하나에 Rigidbody가 붙어있어야 발생한다. Player는 CharacterController만 있고
// Rigidbody가 없으므로, 트리거 쪽인 PhotoSpot이 Kinematic Rigidbody를 들고 있어야 한다.
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class PhotoSpot : MonoBehaviour, IInteractable, IRequiresAbility
{
    // 과거 사진 촬영은 플레이어 B(PastPhoto 능력)만 할 수 있다. 다른 플레이어에게는 안내도 뜨지 않는다.
    public PlayerAbility RequiredAbility => PlayerAbility.PastPhoto;

    [Header("참조")]
    [Tooltip("촬영 시 카메라가 이동해서 고정될 위치/각도. 보통 이 PhotoSpot의 자식으로 빈 오브젝트를 만들어 연결한다.")]
    [SerializeField] private Transform cameraAnchor;

    [Tooltip("이 지점에서 촬영했을 때 지급할 사진 아이템 데이터.")]
    [SerializeField] private PhotoItemData photoItem;

    [Header("UI")]
    [Tooltip("범위 안에 들어왔을 때 보여줄 안내 문구.")]
    [SerializeField] private string interactPrompt = "[E] 사진 찍기";

    // 이미 촬영을 완료했는지. 한 번 true가 되면 다시는 상호작용할 수 없다 (설계 조건 5번).
    [SerializeField] private bool captured = false;

    // 플레이어가 이 트리거 범위 안에 들어와 있는 동안, 등록해 둔 컨트롤러를 기억해뒀다가
    // 촬영 완료 시 "더 이상 상호작용 대상이 아니다"라고 알려주기 위해 들고 있는다.
    private InteractionController registeredController;

    public Transform CameraAnchor => cameraAnchor;
    public PhotoItemData PhotoItem => photoItem;

    private void Awake()
    {
        // 트리거 감지에만 필요한 Rigidbody라서, 물리적으로 움직이거나 떨어지면 안 된다.
        // 디자이너가 Inspector에서 깜빡하고 안 끄는 경우를 대비해 코드에서 확실히 고정한다.
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    // IInteractable 구현 ─────────────────────────────────────
    public bool CanInteract => !captured;
    public string InteractPrompt => interactPrompt;

    public void Interact(GameObject interactor)
    {
        if (!CanInteract) return;

        // 촬영은 Player에 붙어있는 PhotoCaptureSystem이 실제로 수행한다.
        // PhotoSpot은 "어디서, 무엇을 찍을지"만 알려주는 역할만 한다.
        PhotoCaptureSystem captureSystem = interactor.GetComponent<PhotoCaptureSystem>();
        if (captureSystem != null)
        {
            captureSystem.BeginCapture(this);
        }
        else
        {
            Debug.LogWarning("[PhotoSpot] interactor에 PhotoCaptureSystem이 없습니다.", interactor);
        }
    }

    // 촬영이 끝나면 PhotoCaptureSystem이 이 메서드를 호출해서 "다시는 못 찍게" 표시한다.
    public void MarkCaptured()
    {
        captured = true;

        // 이미 안내 문구가 떠 있었다면 즉시 꺼준다 (다시 촬영 못 하는데 문구만 남아있으면 혼란스러우므로).
        if (registeredController != null)
        {
            registeredController.ClearInteractable(this);
            registeredController = null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!CanInteract) return; // 이미 촬영한 지점은 안내 문구조차 띄우지 않는다 (설계 조건 5번).
        if (!other.CompareTag("Player")) return;

        InteractionController controller = other.GetComponentInParent<InteractionController>();
        if (controller == null) return;

        registeredController = controller;
        controller.RegisterInteractable(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        InteractionController controller = other.GetComponentInParent<InteractionController>();
        if (controller != null)
        {
            controller.ClearInteractable(this);
        }
        registeredController = null;
    }
}
