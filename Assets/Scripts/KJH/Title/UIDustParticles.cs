using UnityEngine;
using UnityEngine.UI;

// 화면 위를 천천히 떠다니는 먼지 입자 (UI 전용, Screen Space Overlay 캔버스에서도 보인다).
// 파티클 시스템 대신 사각형 여러 개를 한 메시로 그린다. 크기·속도 단위는 캔버스 단위(CanvasScaler 기준 해상도).
// 가까운 먼지(크고 흐림)는 빠르게, 먼 먼지(작고 또렷)는 느리게 움직여 깊이감을 낸다.
[RequireComponent(typeof(CanvasRenderer))]
public class UIDustParticles : MaskableGraphic
{
    [Header("개수")]
    [SerializeField] int count = 220;
    [Tooltip("이 비율만큼은 크고 흐릿한 '가까운 먼지'(보케)로 만든다")]
    [SerializeField, Range(0f, 1f)] float nearRatio = 0.08f;

    [Header("크기 (캔버스 단위)")]
    [SerializeField] Vector2 farSize = new Vector2(2.5f, 7f);
    [SerializeField] Vector2 nearSize = new Vector2(14f, 30f);

    [Header("움직임")]
    [Tooltip("먼 먼지 속도(초당 캔버스 단위). 가까운 먼지는 nearSpeedScale배")]
    [SerializeField] Vector2 speed = new Vector2(5f, 22f);
    [SerializeField] float nearSpeedScale = 2.2f;
    [Tooltip("살짝 위로 떠오르는 경향 (0이면 완전히 무작위 방향)")]
    [SerializeField] float upwardBias = 4f;
    [Tooltip("좌우로 흔들리는 폭")]
    [SerializeField] float sway = 12f;

    [Header("밝기")]
    [SerializeField] Vector2 farAlpha = new Vector2(0.2f, 0.55f);
    [SerializeField] Vector2 nearAlpha = new Vector2(0.05f, 0.15f);
    [Tooltip("반짝임 정도 (0이면 일정한 밝기)")]
    [SerializeField, Range(0f, 1f)] float twinkle = 0.3f;

    struct Dust
    {
        public Vector2 pos;      // 0~1 (rect 기준)
        public Vector2 vel;      // 캔버스 단위/초
        public float size, alpha, phase, twinkleFreq, swayFreq, swayAmp;
        public bool near;
    }

    Dust[] dust;
    static Texture2D dotTex;

    public override Texture mainTexture { get { return DotTexture(); } }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (dust == null || dust.Length != count) Spawn();
    }

#if UNITY_EDITOR
    // Graphic.OnValidate는 에디터에만 있어서, 감싸지 않으면 빌드에서 컴파일 에러가 난다
    protected override void OnValidate()
    {
        base.OnValidate();
        Spawn();
    }
#endif

    void Spawn()
    {
        dust = new Dust[Mathf.Max(0, count)];
        for (int i = 0; i < dust.Length; i++)
        {
            bool near = Random.value < nearRatio;
            float ang = Random.value * Mathf.PI * 2f;
            float spd = Random.Range(speed.x, speed.y) * (near ? nearSpeedScale : 1f);
            dust[i] = new Dust
            {
                pos = new Vector2(Random.value, Random.value),
                vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd + Vector2.up * upwardBias,
                size = near ? Random.Range(nearSize.x, nearSize.y) : Random.Range(farSize.x, farSize.y),
                alpha = near ? Random.Range(nearAlpha.x, nearAlpha.y) : Random.Range(farAlpha.x, farAlpha.y),
                phase = Random.value * 100f,
                twinkleFreq = Random.Range(0.3f, 1.4f),
                swayFreq = Random.Range(0.08f, 0.3f),
                swayAmp = sway * Random.Range(0.4f, 1f) * (near ? 1.6f : 1f),
                near = near,
            };
        }
    }

    void Update()
    {
        if (dust == null) return;
        Vector2 size = rectTransform.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;
        float dt = Time.unscaledDeltaTime;
        float margin = 30f;   // 화면 밖으로 조금 나간 뒤 반대쪽에서 다시 들어온다
        for (int i = 0; i < dust.Length; i++)
        {
            var d = dust[i];
            d.pos += new Vector2(d.vel.x / size.x, d.vel.y / size.y) * dt;
            float mx = margin / size.x, my = margin / size.y;
            if (d.pos.x < -mx) d.pos.x = 1f + mx; else if (d.pos.x > 1f + mx) d.pos.x = -mx;
            if (d.pos.y < -my) d.pos.y = 1f + my; else if (d.pos.y > 1f + my) d.pos.y = -my;
            dust[i] = d;
        }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (dust == null) return;
        Rect r = rectTransform.rect;
        float t = Application.isPlaying ? Time.unscaledTime : 0f;
        Color32 baseCol = color;
        for (int i = 0; i < dust.Length; i++)
        {
            var d = dust[i];
            float s = Mathf.Sin((t + d.phase) * d.swayFreq * Mathf.PI * 2f) * d.swayAmp;
            Vector2 c = new Vector2(r.xMin + d.pos.x * r.width + s, r.yMin + d.pos.y * r.height);
            float tw = 1f - twinkle * 0.5f * (1f + Mathf.Sin((t + d.phase) * d.twinkleFreq * Mathf.PI * 2f));
            Color32 col = baseCol;
            col.a = (byte)Mathf.Clamp(baseCol.a * d.alpha * tw, 0f, 255f);
            float h = d.size * 0.5f;
            int v = vh.currentVertCount;
            vh.AddVert(new Vector3(c.x - h, c.y - h), col, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(c.x - h, c.y + h), col, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(c.x + h, c.y + h), col, new Vector2(1f, 1f));
            vh.AddVert(new Vector3(c.x + h, c.y - h), col, new Vector2(1f, 0f));
            vh.AddTriangle(v, v + 1, v + 2);
            vh.AddTriangle(v + 2, v + 3, v);
        }
    }

    // 가운데가 밝고 가장자리로 부드럽게 사라지는 동그란 점 (코드로 한 번만 만든다)
    static Texture2D DotTexture()
    {
        if (dotTex != null) return dotTex;
        const int N = 64;
        dotTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "UIDustDot", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);   // 부드러운 가장자리
                px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        dotTex.SetPixels32(px);
        dotTex.Apply();
        return dotTex;
    }
}
