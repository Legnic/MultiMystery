using UnityEngine;
using UnityEngine.InputSystem;

// T를 누르고 있는 동안 캐릭터들의 입이 열렸다 닫혔다 한다. (LHJ 입 리깅 테스트용)
// 입 닫기 모양(블렌드 셰이프 "MouthClose")이 들어간 메시에서만 움직인다.
public class MouthFlapTest : MonoBehaviour
{
    [SerializeField] SkinnedMeshRenderer[] bodies;
    [SerializeField] float flapsPerSecond = 4f;

    const string ShapeName = "MouthClose";

    void Update()
    {
        var keyboard = Keyboard.current;
        bool talking = keyboard != null && keyboard.tKey.isPressed;
        // 말하지 않을 때는 입을 다물고 있다.
        float weight = talking ? Mathf.PingPong(Time.time * flapsPerSecond * 2f, 1f) * 100f : 100f;

        foreach (var body in bodies)
        {
            if (body == null) continue;
            int index = body.sharedMesh.GetBlendShapeIndex(ShapeName);
            if (index >= 0) body.SetBlendShapeWeight(index, weight);
        }
    }
}
