using UnityEngine;
using UnityEngine.UI;

// 잉크 펜으로 그은 듯한 "한 줄짜리 파형 선"을 그리는 UI 그래픽.
// Image처럼 Canvas 안에서 동작하지만, 그림(스프라이트) 대신 점 목록으로 선 모양 메시를 직접 만든다.
// 높이 값(-1 ~ 1)만 SetHeights로 넘겨 받고, "소리를 어떻게 분석할지"는 SpectrumOverlay가 담당한다
// (그리기와 분석을 나눠 두면, 나중에 다른 연출에서도 이 선 그리기만 재사용할 수 있다).
//
// 잉크 느낌을 내는 방법:
//   1) 번짐(bleed): 굵고 아주 옅은 선을 먼저 깔아 종이에 잉크가 스민 듯한 테두리를 만든다.
//   2) 펜 압력: 선 굵기를 위치에 따라 천천히 변하게 해서(펄린 노이즈) 손으로 그은 것처럼 보이게 한다.
//   3) 끝 가늘어짐: 선의 양 끝은 펜을 떼듯 점점 가늘고 옅어진다.
// 사용법: SpectrumOverlay 아래의 RectTransform 오브젝트에 붙인다. 색은 Graphic의 Color(먹색/세피아)로 정한다.
[RequireComponent(typeof(CanvasRenderer))]
public class InkWaveformGraphic : MaskableGraphic
{
    [Header("선 모양")]
    [Tooltip("선의 기본 굵기 (픽셀, Canvas 기준 해상도에서).")]
    [SerializeField] private float lineWidth = 2.4f;

    [Tooltip("펜 압력처럼 굵기가 변하는 정도 (0 = 일정, 0.4 = ±40%).")]
    [Range(0f, 1f)]
    [SerializeField] private float pressureVariation = 0.35f;

    [Tooltip("선 양 끝에서 가늘어지는 구간의 비율 (전체 길이 대비).")]
    [Range(0f, 0.5f)]
    [SerializeField] private float edgeTaper = 0.12f;

    [Header("잉크 번짐")]
    [Tooltip("번짐 선의 굵기 배율 (기본 선 굵기 × 이 값).")]
    [SerializeField] private float bleedWidthMultiplier = 3.2f;

    [Tooltip("번짐 선의 불투명도 배율 (기본 색 알파 × 이 값). 아주 옅게 깔아야 자연스럽다.")]
    [Range(0f, 1f)]
    [SerializeField] private float bleedAlpha = 0.16f;

    // 현재 그릴 높이 값들. 0번이 왼쪽 끝, 마지막이 오른쪽 끝. 값은 -1(아래 끝) ~ 1(위 끝).
    private float[] heights = new float[0];
    // 펜 압력 노이즈가 아주 천천히 흐르게 하기 위한 시간 값 (SetHeights 때마다 갱신).
    private float pressureTime;

    // SpectrumOverlay가 매 프레임 호출한다. 배열은 복사하지 않고 참조만 들고 있다가 메시를 다시 만든다.
    public void SetHeights(float[] newHeights, float time)
    {
        heights = newHeights ?? new float[0];
        pressureTime = time;
        SetVerticesDirty(); // "모양이 바뀌었으니 다음 그리기 전에 OnPopulateMesh를 다시 불러달라"는 요청
    }

    // 파형은 클릭을 받을 필요가 없으므로 기본으로 레이캐스트 대상에서 뺀다 (다른 UI 클릭을 막지 않도록).
    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    // Unity UI가 메시가 필요할 때 부르는 함수. 여기서 선 모양의 사각형(쿼드) 띠를 직접 만든다.
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int count = heights.Length;
        if (count < 2) return;

        Rect r = rectTransform.rect;
        Vector2[] points = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float x01 = i / (float)(count - 1);
            // 가로는 영역 전체 폭에 고르게, 세로는 영역 가운데를 기준으로 높이 값 × 절반 높이.
            points[i] = new Vector2(Mathf.Lerp(r.xMin, r.xMax, x01), r.center.y + heights[i] * r.height * 0.5f);
        }

        // 번짐 선을 먼저 깔고(아래), 그 위에 본 선을 그린다(위). 먼저 넣은 삼각형이 뒤에 그려진다.
        Color32 bleedColor = color;
        bleedColor.a = (byte)(color.a * bleedAlpha);
        AddStroke(vh, points, lineWidth * bleedWidthMultiplier, bleedColor, 0f);
        AddStroke(vh, points, lineWidth, color, pressureVariation);
    }

    // 점 목록을 따라 굵기가 있는 띠(사각형 연속)를 만든다.
    private void AddStroke(VertexHelper vh, Vector2[] points, float width, Color32 baseColor, float pressure)
    {
        int count = points.Length;
        int startIndex = vh.currentVertCount;

        for (int i = 0; i < count; i++)
        {
            // 진행 방향: 앞뒤 점을 이용해 구한다 (양 끝은 한쪽만 사용).
            Vector2 prev = points[Mathf.Max(0, i - 1)];
            Vector2 next = points[Mathf.Min(count - 1, i + 1)];
            Vector2 dir = (next - prev).normalized;
            // 진행 방향에 수직인 방향으로 위/아래 두 꼭짓점을 벌려서 굵기를 만든다.
            Vector2 normal = new Vector2(-dir.y, dir.x);

            float x01 = i / (float)(count - 1);
            // 끝 가늘어짐: 양 끝 edgeTaper 구간에서 0 → 1로 부드럽게 커진다.
            float taper = edgeTaper > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Min(x01, 1f - x01) / edgeTaper) : 1f;
            // 펜 압력: 위치와 시간에 따라 아주 천천히 변하는 펄린 노이즈로 굵기를 흔든다.
            float noise = Mathf.PerlinNoise(x01 * 6f, pressureTime * 0.35f) * 2f - 1f;
            float w = width * taper * (1f + noise * pressure) * 0.5f;

            Color32 c = baseColor;
            c.a = (byte)(baseColor.a * Mathf.Lerp(0.25f, 1f, taper)); // 끝으로 갈수록 잉크가 옅어지게

            vh.AddVert(points[i] + normal * w, c, Vector2.zero);
            vh.AddVert(points[i] - normal * w, c, Vector2.zero);

            if (i > 0)
            {
                // 직전 두 점과 지금 두 점으로 사각형 하나(삼각형 2개)를 만든다.
                int a = startIndex + (i - 1) * 2;
                vh.AddTriangle(a, a + 2, a + 1);
                vh.AddTriangle(a + 1, a + 2, a + 3);
            }
        }
    }
}
