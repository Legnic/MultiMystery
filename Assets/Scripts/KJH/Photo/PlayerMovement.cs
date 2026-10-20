using UnityEngine;
using UnityEngine.InputSystem;

// 1인칭 시점 이동 + 마우스 시점 회전을 담당하는 스크립트.
// 이 스크립트는 CharacterController가 붙어있는 Player 오브젝트에 붙여서 사용한다.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour, IPlayerLock
{
    // ── Inspector에서 설정하는 값들 ─────────────────────────────
    [Header("이동")]
    [SerializeField] private float moveSpeed = 4f; // 초당 이동 속도 (m/s)
    [SerializeField] private float gravity = -9.81f; // 중력 가속도. CharacterController는 Rigidbody가 없어서 중력을 직접 계산해줘야 한다.

    [Header("시점 회전")]
    [SerializeField] private float lookSensitivity = 0.1f; // 마우스 델타에 곱하는 감도 값
    [SerializeField] private float minPitch = -80f; // 위/아래로 고개를 숙일 수 있는 최대 각도 (카메라가 뒤집히지 않도록 제한)
    [SerializeField] private float maxPitch = 80f;

    [Header("입력 액션")]
    [Tooltip("InputSystem_Actions의 Player/Move (Vector2, WASD).")]
    [SerializeField] private InputActionReference moveAction;
    [Tooltip("InputSystem_Actions의 Player/Look (Vector2, 마우스 델타).")]
    [SerializeField] private InputActionReference lookAction;

    // 살펴보기 단서처럼 마우스 이동량이 필요한 다른 기능이 같은 Look 액션을 빌려 쓸 수 있게 공개한다.
    public InputActionReference LookAction => lookAction;

    [Tooltip("시점 회전을 적용할 카메라 Transform (보통 Player의 자식인 Main Camera).")]
    [SerializeField] private Transform cameraTransform;

    // ── 외부에서 조작하는 상태값 ────────────────────────────────
    // CameraModeController가 뷰파인더 모드에 들어갈 때 이 값을 0.5 등으로 낮춰서 이동속도를 줄인다.
    // 1 = 평소 속도, 0.5 = 절반 속도.
    public float SpeedMultiplier { get; set; } = 1f;

    // 인벤토리 UI가 열려있을 때 PhotoInventory가 이 값을 꺼서 시점 회전을 멈춘다.
    public bool LookEnabled { get; set; } = true;

    // 현재 좌우(yaw, 몸통 회전) / 위아래(pitch, 카메라 기울기) 시점 각도 (도). 연출이 "지금 어디를 보고 있는지" 읽을 때 쓴다.
    public float Yaw => transform.eulerAngles.y;
    public float Pitch => pitch;

    // 연출(ObjectEcho 등)이 시점을 특정 방향으로 돌릴 때 쓴다.
    // 카메라를 직접 돌리면 다음 프레임에 HandleLook이 저장된 pitch 값으로 되돌려 버리므로,
    // 반드시 이 함수로 내부 값까지 함께 바꿔야 연출이 끝난 뒤에도 그 방향을 계속 보고 있게 된다.
    public void SetLookAngles(float yaw, float newPitch)
    {
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
        if (cameraTransform != null) cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
    }

    private CharacterController characterController;
    private float verticalVelocity; // 중력에 의해 누적되는 수직 낙하 속도
    private float pitch; // 현재 위아래 시점 각도 (카메라에만 적용, 몸통은 좌우로만 회전)

    private void Awake()
    {
        // CharacterController는 [RequireComponent]로 항상 보장되므로 null 체크 없이 바로 캐싱한다.
        characterController = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (lookAction != null) lookAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null) moveAction.action.Disable();
        if (lookAction != null) lookAction.action.Disable();
    }

    private void Update()
    {
        HandleLook();
        HandleMove();
    }

    private void HandleMove()
    {
        Vector2 input = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

        // 입력(Vector2)을 플레이어가 바라보는 방향 기준의 3D 이동 벡터로 바꾼다.
        // transform.forward/right를 쓰면 플레이어가 어느 방향을 보고 있어도 "앞"이 항상 카메라 앞쪽이 된다.
        Vector3 moveDirection = (transform.right * input.x + transform.forward * input.y);

        // SpeedMultiplier를 곱해서 뷰파인더 모드일 때 자동으로 속도가 줄어들게 한다.
        Vector3 horizontalMove = moveDirection * (moveSpeed * SpeedMultiplier);

        // CharacterController.isGrounded는 바닥에 닿아있는지 알려준다. 땅에 있으면 수직 속도를 살짝 눌러준다
        // (완전히 0으로 두면 약간의 틈에서 공중에 뜬 것처럼 보일 수 있어, 작은 음수값으로 고정하는 것이 일반적인 방식).
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        // Time.deltaTime을 곱해 프레임 속도와 무관하게 중력이 쌓이도록 한다.
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalMove = horizontalMove + Vector3.up * verticalVelocity;

        // CharacterController.Move는 충돌을 자동으로 처리해주는 이동 함수라 Rigidbody 없이도 벽에 막힌다.
        characterController.Move(finalMove * Time.deltaTime);
    }

    private void HandleLook()
    {
        // 인벤토리가 열려있는 동안은 시점 회전을 멈춘다 (마우스 커서로 UI를 조작해야 하므로).
        if (!LookEnabled) return;

        Vector2 lookDelta = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;

        // 좌우(yaw) 회전은 몸통(transform) 전체를 돌려서 이동 방향도 같이 바뀌게 한다.
        transform.Rotate(Vector3.up * (lookDelta.x * lookSensitivity));

        // 위아래(pitch) 회전은 몸통이 아니라 카메라만 기울인다 (몸통까지 기울면 이상하게 보임).
        pitch -= lookDelta.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (cameraTransform != null)
        {
            cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }
    }
}
