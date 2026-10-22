using UnityEngine;
using UnityEngine.InputSystem;

// W·A·D(또는 방향키)를 누르는 동안 캐릭터들이 그쪽으로 돌아 같이 걷고, Shift를 같이 누르면 뛴다.
// S(아래 방향키)만 누르면 보고 있는 방향 그대로 뒤로 걷는다.
// 키를 떼면 제자리에 서고, 스페이스바를 누르면 점프한다. (LHJ 테스트용)
public class CharacterKeyMove : MonoBehaviour
{
    [SerializeField] Animator[] characters;
    [SerializeField] float walkSpeed = 0.6f;
    [SerializeField] float runSpeed = 1.6f;
    [SerializeField] float backSpeed = 0.4f;
    [SerializeField] float turnSpeed = 720f;

    static readonly int MoveParam = Animator.StringToHash("Move");
    static readonly int RunParam = Animator.StringToHash("Run");
    static readonly int BackParam = Animator.StringToHash("Back");
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

        bool isBack = input.y < 0 && input.x == 0;
        bool isMoving = input != Vector2.zero && !isBack;
        bool isRunning = isMoving && keyboard.shiftKey.isPressed;
        bool jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
        var direction = new Vector3(input.x, 0, input.y).normalized;
        float speed = isRunning ? runSpeed : walkSpeed;

        foreach (var animator in characters)
        {
            if (animator == null) continue;
            animator.SetBool(MoveParam, isMoving);
            animator.SetBool(RunParam, isRunning);
            animator.SetBool(BackParam, isBack);
            if (jumpPressed && !animator.GetCurrentAnimatorStateInfo(0).IsName("Jump")) animator.SetTrigger(JumpParam);

            var body = animator.transform;
            if (isBack)
            {
                body.position -= body.forward * backSpeed * Time.deltaTime;
                continue;
            }
            if (!isMoving) continue;

            body.position += direction * speed * Time.deltaTime;
            body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }
    }
}
