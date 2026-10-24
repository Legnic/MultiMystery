using System.Collections.Generic;
using UnityEngine;

// 3D 글자(TextMesh)가 벽 너머로 비쳐 보이는 것을 막는다.
// 기본 글꼴 머티리얼(GUI/Text Shader)은 URP에서 깊이 검사를 하지 않아 가려진 글자도 그대로 보인다.
// 깊이 검사를 하는 템플릿 머티리얼(KJH/Gim005/WorldText 셰이더)을 쓰되, 글꼴 텍스처는 실행 중에 만들어지므로
// 여기서 연결하고, 글꼴에 새 글자가 추가돼 텍스처가 다시 만들어지면 따라서 다시 연결한다.
// TextMesh가 있는 오브젝트에 붙이면 끝이다 (뒷면은 안 그려지므로 글자가 읽히는 쪽에서만 보인다).
[RequireComponent(typeof(TextMesh))]
[DisallowMultipleComponent]
public class WorldTextDepth : MonoBehaviour
{
    [Tooltip("깊이 검사 글자 머티리얼 (셰이더 KJH/Gim005/WorldText). 실행 중 복사본을 만들어 쓴다.")]
    [SerializeField] Material template;

    // (템플릿, 글꼴) 조합마다 런타임 머티리얼 하나를 공유한다
    static readonly Dictionary<(Material, Font), Material> shared = new Dictionary<(Material, Font), Material>();
    static bool hooked;

    void Awake()
    {
        var tm = GetComponent<TextMesh>();
        var r = GetComponent<MeshRenderer>();
        if (template == null || tm == null || r == null || tm.font == null) return;

        var key = (template, tm.font);
        if (!shared.TryGetValue(key, out var mat) || mat == null)
        {
            mat = new Material(template) { name = template.name + " (Runtime)" };
            shared[key] = mat;
        }
        mat.mainTexture = tm.font.material.mainTexture;
        tm.font.RequestCharactersInTexture(tm.text, tm.fontSize, tm.fontStyle);
        r.sharedMaterial = mat;

        if (!hooked) { hooked = true; Font.textureRebuilt += OnFontRebuilt; }
    }

    static void OnFontRebuilt(Font f)
    {
        foreach (var kv in shared)
            if (kv.Key.Item2 == f && kv.Value != null) kv.Value.mainTexture = f.material.mainTexture;
    }

    // 도메인 리로드를 끄고 Play할 때 이전 실행의 정적 상태가 남지 않게 한다
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        shared.Clear();
        if (hooked) { Font.textureRebuilt -= OnFontRebuilt; hooked = false; }
    }
#endif
}
