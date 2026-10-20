using LGH;
using UnityEngine;
using UnityEngine.InputSystem;

// 갤러리 액자 하나. 바라보고 E를 누르면 액자 정면에 고정되어 Q(왼쪽)/E(오른쪽)로 30도씩 기울일 수 있다.
// 단계(Step): -1 = 왼쪽 30도, 0 = 가운데, +1 = 오른쪽 30도. (왼쪽 → 가운데 → 오른쪽 순서로만 이동)
// 액자를 보는 사람 기준의 "오른쪽" = 시계 방향이다.
// 실제 단계 값은 PortraitPuzzle(호스트 판정)이 정해서 ApplyStep()으로 내려주고, 이 컴포넌트는 입력과 연출만 담당한다.
public class PortraitTilt : MonoBehaviour
{
    // 지금 이 컴퓨터의 플레이어가 살펴보고 있는 액자 (없으면 null). PlayerInteractor가 이걸 보고 입력을 양보한다.
    public static PortraitTilt Current { get; private set; }

    [Tooltip("액자 앞면에서 플레이어가 서는 거리(m)")]
    [SerializeField] float viewDistance = 1.5f;
    [SerializeField] float tiltAngle = 30f;
    [Tooltip("기울어지는 속도(도/초)")]
    [SerializeField] float tiltSpeed = 90f;

    public int Step { get; private set; }

    // 액자 두께 축(로컬 X)이 벽 안쪽을 향한다고 가정: 보는 사람 → 액자 방향
    Vector3 ViewDir { get { return transform.right; } }

    Quaternion baseLocalRotation;
    Quaternion targetLocalRotation;

    // 살펴보기 세션 상태
    PlayerInteractor user;
    FirstPersonController userMove;
    CharacterController userController;
    Transform userCamera;
    Quaternion savedCameraRotation;
    int beginFrame;

    public bool IsViewing { get { return user != null; } }

    void Awake()
    {
        baseLocalRotation = transform.localRotation;
        targetLocalRotation = baseLocalRotation;
    }

    public void ApplyStep(int step, bool instant = false)
    {
        Step = Mathf.Clamp(step, -1, 1);
        // 보는 사람 기준 오른쪽(시계 방향)이 Step +1. 실제 플레이로 확인한 결과 로컬 X축 양의 회전은
        // 반시계로 보이므로 부호를 반대로 적용한다.
        targetLocalRotation = baseLocalRotation * Quaternion.Euler(-Step * tiltAngle, 0f, 0f);
        if (instant) transform.localRotation = targetLocalRotation;
    }

    public void BeginView(PlayerInteractor interactor)
    {
        if (user != null || interactor == null) return;
        user = interactor;
        userMove = interactor.GetComponent<FirstPersonController>();
        userController = interactor.GetComponent<CharacterController>();
        userCamera = interactor.viewCamera;
        if (userMove != null) userMove.enabled = false;
        if (userController != null) userController.enabled = false;
        if (userCamera != null) savedCameraRotation = userCamera.localRotation;
        beginFrame = Time.frameCount;
        Current = this;
    }

    void EndView()
    {
        if (user == null) return;
        if (userCamera != null) userCamera.localRotation = savedCameraRotation;
        if (userController != null) userController.enabled = true;
        if (userMove != null) userMove.enabled = true;
        user = null;
        userMove = null;
        userController = null;
        userCamera = null;
        if (Current == this) Current = null;
    }

    void OnDisable() { EndView(); }

    void Update()
    {
        if (transform.localRotation != targetLocalRotation)
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetLocalRotation, tiltSpeed * Time.deltaTime);

        if (user == null) return;

        // 액자 정면(눈높이 그대로, 수평 시선)으로 부드럽게 이동·고정
        Vector3 flatView = Vector3.ProjectOnPlane(ViewDir, Vector3.up).normalized;
        Vector3 stand = transform.position - flatView * viewDistance;
        stand.y = user.transform.position.y;
        float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
        user.transform.SetPositionAndRotation(
            Vector3.Lerp(user.transform.position, stand, k),
            Quaternion.Slerp(user.transform.rotation, Quaternion.LookRotation(flatView, Vector3.up), k));
        if (userCamera != null)
            userCamera.localRotation = Quaternion.Slerp(userCamera.localRotation, Quaternion.identity, k);

        if (Time.frameCount == beginFrame) return; // 시작한 E 입력이 곧바로 기울이기로 이어지지 않게
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.fKey.wasPressedThisFrame) { EndView(); return; }
        if (PortraitPuzzle.Instance == null) return;
        if (kb.qKey.wasPressedThisFrame) PortraitPuzzle.Instance.RequestShift(this, -1);
        else if (kb.eKey.wasPressedThisFrame) PortraitPuzzle.Instance.RequestShift(this, +1);
    }

    public string ViewPrompt
    {
        get
        {
            string state = Step < 0 ? "왼쪽 30°" : Step > 0 ? "오른쪽 30°" : "가운데";
            return "[Q] 왼쪽  [E] 오른쪽  [F] 그만 보기   (" + state + ")";
        }
    }
}
