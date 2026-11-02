using UnityEngine;
using UnityEngine.InputSystem;

// 플레이하면 Game 화면에 주황이 눈 시점이 보인다. Tab을 누를 때마다 주황이 → 파랑이 → 원래 카메라 순서로 바뀐다. (LHJ 시점 테스트용)
public class PovViewSwitch : MonoBehaviour
{
    [SerializeField] Camera[] views;
    [SerializeField] Key switchKey = Key.Tab;

    int current;

    void Start()
    {
        Select(0);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard[switchKey].wasPressedThisFrame) Select((current + 1) % (views.Length + 1));
    }

    // views.Length 번째는 "아무 시점도 안 켬" = 원래 메인 카메라 화면
    void Select(int index)
    {
        current = index;
        for (int i = 0; i < views.Length; i++)
        {
            views[i].enabled = i == index;
            views[i].depth = 10;
        }
    }
}
