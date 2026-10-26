using UnityEngine;
using UnityEngine.InputSystem;

// 화면 기준으로 W는 앞(카메라 쪽, 얼굴이 보임), S는 뒤(화면 안쪽), A는 왼쪽, D는 오른쪽이다.
// 키를 누르는 동안 캐릭터들이 그쪽으로 돌아 같이 걷고, Shift를 같이 누르면 뛴다.
// 키를 떼면 제자리에 서고, 스페이스바를 누르면 점프한다. (LHJ 테스트용)
public class CharacterKeyMove : MonoBehaviour
{
    [SerializeField] Animator[] characters;
    [SerializeField] float walkSpeed = 0.6f;
    [SerializeField] float runSpeed = 1.6f;
    [SerializeField] float turnSpeed = 720f;

    static readonly int MoveParam = Animator.StringToHash("Move");
    static readonly int RunParam = Animator.StringToHash("Run");
    static readonly int JumpParam = Animator.StringToHash("Jump");

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        var input = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1;

        bool isMoving = input != Vector2.zero;
        bool isRunning = isMoving && keyboard.shiftKey.isPressed;
        bool jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
        var direction = CameraRelative(input);
        float speed = isRunning ? runSpeed : walkSpeed;

        foreach (var animator in characters)
        {
            if (animator == null) continue;
            animator.SetBool(MoveParam, isMoving);
            animator.SetBool(RunParam, isRunning);
            if (jumpPressed && !animator.GetCurrentAnimatorStateInfo(0).IsName("Jump")) animator.SetTrigger(JumpParam);

            if (!isMoving) continue;

            var body = animator.transform;
            body.position += direction * speed * Time.deltaTime;
            body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }
    }

    // 키 입력을 화면 기준의 바닥 위 방향으로 바꾼다. 앞(W)은 카메라를 향하는 쪽이다.
    static Vector3 CameraRelative(Vector2 input)
    {
        var cam = Camera.main;
        var intoScreen = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        var screenRight = Vector3.Cross(Vector3.up, intoScreen);
        return (screenRight * input.x - intoScreen * input.y).normalized;
    }
}
