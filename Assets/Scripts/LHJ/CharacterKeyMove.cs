using UnityEngine;
using UnityEngine.InputSystem;

// WASD(또는 방향키)를 누르는 동안 캐릭터들이 같이 걷고, Shift를 같이 누르면 뛴다.
// 키를 떼면 제자리에 선다. (LHJ 테스트용)
public class CharacterKeyMove : MonoBehaviour
{
    [SerializeField] Animator[] characters;
    [SerializeField] float walkSpeed = 0.6f;
    [SerializeField] float runSpeed = 1.6f;
    [SerializeField] float turnSpeed = 720f;

    static readonly int MoveParam = Animator.StringToHash("Move");
    static readonly int RunParam = Animator.StringToHash("Run");

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
        var direction = new Vector3(input.x, 0, input.y).normalized;
        float speed = isRunning ? runSpeed : walkSpeed;

        foreach (var animator in characters)
        {
            if (animator == null) continue;
            animator.SetBool(MoveParam, isMoving);
            animator.SetBool(RunParam, isRunning);
            if (!isMoving) continue;

            var body = animator.transform;
            body.position += direction * speed * Time.deltaTime;
            body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
        }
    }
}
