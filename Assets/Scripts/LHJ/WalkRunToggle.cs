using UnityEngine;
using UnityEngine.InputSystem;

// 스페이스바를 누를 때마다 장면 안 캐릭터들의 걷기 ↔ 뛰기를 바꾼다 (LHJ 테스트용)
public class WalkRunToggle : MonoBehaviour
{
    [SerializeField] Animator[] characters;

    static readonly int RunParam = Animator.StringToHash("Run");
    bool isRunning;

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame) return;

        isRunning = !isRunning;
        foreach (var animator in characters)
        {
            if (animator != null) animator.SetBool(RunParam, isRunning);
        }
    }
}
