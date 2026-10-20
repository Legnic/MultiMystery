using UnityEngine;

// 조준점이 단서를 가리킬 때 단서를 "은은하게 밝게" 만드는 강조 표시 컴포넌트.
// 일반 단서(ViewableClue)와 연출 단서(InspectableClue)가 똑같이 쓰도록 따로 떼어낸 것이다.
// 갑툭튀 없는 긴장감 톤에 맞춰 번쩍이지 않고 짧게 서서히 켜지고 꺼진다.
// 사용법: 단서의 최상위 오브젝트에 붙인다. 자식에 있는 모든 렌더러가 함께 밝아진다.
// (단서 컴포넌트가 이것을 찾지 못하면 실행 중에 자동으로 붙인다)
public class ClueHighlighter : MonoBehaviour
{
    [Tooltip("조준됐을 때 기본 색에 곱할 밝기 배율. 1.15 = 15% 밝게.")]
    [SerializeField] private float highlightMultiplier = 1.15f;

    [Tooltip("강조가 서서히 켜지고 꺼지는 시간 (초). 번쩍이지 않게 짧고 부드럽게.")]
    [SerializeField] private float fadeDuration = 0.15f;

    // URP Lit 셰이더의 기본 색 속성 이름. 문자열 대신 숫자 ID로 바꿔 두면 매번 찾는 비용이 없다.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private float current; // 0 = 강조 없음, 1 = 완전 강조 (지금 값)
    private float target;  // 가고 싶은 값

    private void Awake()
    {
        // GetComponent 계열은 Awake에서 한 번만 찾아서 캐싱한다.
        renderers = GetComponentsInChildren<Renderer>();
        // MaterialPropertyBlock: 머티리얼을 복제하지 않고 이 렌더러만 색을 바꾸는 방법.
        // 머티리얼 인스턴스를 만들면 메모리가 늘고, 같은 머티리얼을 쓰는 다른 물체까지 바뀌는 실수가 생기기 쉽다.
        propertyBlock = new MaterialPropertyBlock();
    }

    // 단서 컴포넌트가 조준이 시작/끝날 때 호출한다. 실제 밝기 변화는 Update에서 서서히 진행된다.
    public void SetHighlighted(bool on)
    {
        target = on ? 1f : 0f;
    }

    private void Update()
    {
        if (Mathf.Approximately(current, target)) return; // 변화가 없으면 아무것도 하지 않는다

        // 일시정지(timeScale = 0)와 무관하게 동작하도록 unscaledDeltaTime을 쓴다.
        float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
        current = Mathf.MoveTowards(current, target, step);
        float multiplier = Mathf.Lerp(1f, highlightMultiplier, Mathf.SmoothStep(0f, 1f, current));

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (current <= 0f)
                {
                    // 완전히 꺼지면 블록을 비워서 원래 머티리얼 색으로 완전히 돌아가게 한다.
                    r.SetPropertyBlock(null, i);
                    continue;
                }
                if (mats[i] == null || !mats[i].HasProperty(BaseColorId)) continue;

                Color baseColor = mats[i].GetColor(BaseColorId);
                Color lit = baseColor * multiplier;
                lit.a = baseColor.a; // 투명도는 바꾸지 않는다
                r.GetPropertyBlock(propertyBlock, i);
                propertyBlock.SetColor(BaseColorId, lit);
                r.SetPropertyBlock(propertyBlock, i);
            }
        }
    }
}
