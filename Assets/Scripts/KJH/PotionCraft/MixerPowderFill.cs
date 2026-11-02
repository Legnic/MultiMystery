using UnityEngine;

namespace PotionCraft
{
    // 혼합 통 안에 "진짜 가루가 쌓이는" 모습을 보여주는 순수 연출 컴포넌트 (판정·네트워크와 무관, 각 컴퓨터가 따로 그린다).
    // MixerStation과 같은 오브젝트에 붙이면 MixerStation이 층(layers) 대신 이걸 쓴다.
    //  - 가루를 하나 넣을 때마다 위에서 가루 줄기가 쏟아지고, 그 색의 가루 더미가 솟아오른다.
    //    첫 가루는 바닥 전체를 덮고, 다음 가루는 그 위 가운데에 원뿔처럼 쌓인다 → 가장자리에 이전 색이 고리처럼 남는다.
    //  - 정답으로 잠기면(locked) 더미 전체가 금색 가루로 서서히 바뀐다.
    //  - 비우기/오답으로 개수가 줄면 더미가 빠르게 꺼진다.
    // 모든 좌표는 이 오브젝트(스테이션) 로컬 기준이다. 그릇 모양이 바뀌면 아래 값들을 다시 맞추면 된다.
    public class MixerPowderFill : MonoBehaviour
    {
        [Header("그릇 안쪽 (스테이션 로컬 좌표)")]
        [Tooltip("그릇 안쪽 바닥 가운데")]
        [SerializeField] private Vector3 floorCenter = new Vector3(0.003f, 0.172f, -0.003f);
        [Tooltip("첫 가루가 바닥을 덮는 반지름 (그릇 안쪽 벽에 닿게)")]
        [SerializeField] private float floorRadius = 0.17f;

        [Header("더미 모양 (칸 0 = 바닥을 덮는 첫 가루)")]
        [Tooltip("칸마다 더미가 퍼지는 반지름")]
        [SerializeField] private float[] pileRadius = { 0.17f, 0.14f, 0.11f };
        [Tooltip("칸마다 더미 꼭대기 높이 (첫 칸은 바닥 두께 포함)")]
        [SerializeField] private float[] pileHeight = { 0.040f, 0.032f, 0.034f };
        [Tooltip("칸마다 더미 중심이 그릇 가운데에서 벗어난 정도 (자연스럽게 보이도록)")]
        [SerializeField] private Vector2[] pileOffset = { Vector2.zero, new Vector2(0.012f, -0.008f), new Vector2(-0.010f, 0.010f) };
        [Tooltip("첫 칸이 바닥 전체에 고르게 까는 두께")]
        [SerializeField] private float baseThickness = 0.014f;
        [Tooltip("가루 표면 울퉁불퉁함")]
        [SerializeField] private float lumpiness = 0.005f;

        [Header("연출")]
        [SerializeField] private Material powderMaterial;
        [SerializeField] private float pourSeconds = 1.0f;
        [SerializeField] private float collapseSeconds = 0.35f;
        [SerializeField] private float goldSeconds = 1.5f;
        [Tooltip("가루 줄기가 시작되는 높이 (바닥 위)")]
        [SerializeField] private float pourHeight = 0.45f;

        const int Slots = MixerStation.SlotCount;
        const int Rings = 40;
        const int Segments = 96;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int SmoothId = Shader.PropertyToID("_Smoothness");
        static readonly int MetalId = Shader.PropertyToID("_Metallic");

        MeshFilter[] filters;
        MeshRenderer[] renderers;
        Mesh[] meshes;
        Vector3[][] verts;
        readonly float[] amp = new float[Slots];      // 0~1, 더미가 솟아오른 정도
        readonly float[] target = new float[Slots];
        readonly Color[] colors = new Color[Slots];
        Color goldColor = new Color(1f, 0.82f, 0.2f);
        float gold, goldTarget;
        bool built, initialized, dirty;
        int displayed;

        ParticleSystem pour;
        Material fxMaterial;

        // MixerStation.Refresh에서 상태가 바뀔 때마다 부른다.
        public void Show(int count, Color[] slotColors, bool locked, Color gold_, Material fxMat)
        {
            Build();
            goldColor = gold_;
            if (fxMat != null) fxMaterial = fxMat;
            for (int i = 0; i < Slots; i++)
            {
                if (i < count) colors[i] = slotColors[i];
                target[i] = i < count ? 1f : 0f;
            }
            goldTarget = locked ? 1f : 0f;

            // 처음 그릴 때나 늦게 접속해 한꺼번에 여러 칸을 받았을 때는 애니메이션 없이 바로 맞춘다.
            if (!initialized || count - displayed > 1)
            {
                for (int i = 0; i < Slots; i++) amp[i] = target[i];
                gold = goldTarget;
                initialized = true;
                SetPour(false, Color.white);
            }
            displayed = count;
            dirty = true;
        }

        void Build()
        {
            if (built) return;
            built = true;
            filters = new MeshFilter[Slots];
            renderers = new MeshRenderer[Slots];
            meshes = new Mesh[Slots];
            verts = new Vector3[Slots][];
            for (int k = 0; k < Slots; k++)
            {
                var go = new GameObject("PowderPile" + k);
                go.transform.SetParent(transform, false);
                filters[k] = go.AddComponent<MeshFilter>();
                renderers[k] = go.AddComponent<MeshRenderer>();
                renderers[k].sharedMaterial = powderMaterial;
                renderers[k].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshes[k] = new Mesh { name = "PowderPile" + k };
                meshes[k].MarkDynamic();
                verts[k] = new Vector3[1 + Rings * Segments];
                meshes[k].vertices = verts[k];
                meshes[k].triangles = BuildTriangles();
                filters[k].sharedMesh = meshes[k];
                go.SetActive(false);
            }
        }

        // 가운데 점 1개 + 링 Rings개 × Segments개. 위에서 볼 때 시계 방향(= 앞면이 위를 향함).
        static int[] BuildTriangles()
        {
            var tris = new int[(Segments + (Rings - 1) * Segments * 2) * 3];
            int t = 0;
            for (int s = 0; s < Segments; s++)
            {
                int a = 1 + s, b = 1 + (s + 1) % Segments;
                tris[t++] = 0; tris[t++] = b; tris[t++] = a;
            }
            for (int r = 0; r < Rings - 1; r++)
                for (int s = 0; s < Segments; s++)
                {
                    int a = 1 + r * Segments + s, b = 1 + r * Segments + (s + 1) % Segments;
                    int c = a + Segments, d = b + Segments;
                    tris[t++] = a; tris[t++] = b; tris[t++] = c;
                    tris[t++] = b; tris[t++] = d; tris[t++] = c;
                }
            return tris;
        }

        void Update()
        {
            if (!built) return;
            float dt = Time.deltaTime;

            // 칸은 차례로 솟는다: 앞 칸이 다 쌓인 뒤에야 다음 칸이 쏟아지기 시작한다.
            int pouring = -1;
            for (int i = 0; i < Slots; i++)
            {
                if (Mathf.Approximately(amp[i], target[i])) continue;
                if (target[i] > amp[i])
                {
                    if (i > 0 && amp[i - 1] < 0.999f) break;
                    amp[i] = Mathf.MoveTowards(amp[i], target[i], dt / Mathf.Max(0.01f, pourSeconds));
                    pouring = i;
                }
                else amp[i] = Mathf.MoveTowards(amp[i], target[i], dt / Mathf.Max(0.01f, collapseSeconds));
                dirty = true;
                if (pouring >= 0) break;
            }
            SetPour(pouring >= 0 && amp[pouring] < 0.85f, pouring >= 0 ? colors[pouring] : Color.white);

            // 금색 변화는 모든 더미가 다 쌓인 뒤에 시작한다.
            bool settled = pouring < 0;
            if (settled && !Mathf.Approximately(gold, goldTarget))
            {
                if (gold == 0f && goldTarget > 0f) Sparkle();
                gold = Mathf.MoveTowards(gold, goldTarget, dt / Mathf.Max(0.01f, goldSeconds));
                dirty = true;
            }

            if (dirty) { dirty = false; Rebuild(); }
        }

        void Rebuild()
        {
            float g = gold * gold * (3f - 2f * gold);
            for (int k = 0; k < Slots; k++)
            {
                bool on = amp[k] > 0.001f;
                renderers[k].gameObject.SetActive(on);
                if (!on) continue;

                float meshR = k == 0 ? floorRadius : Mathf.Min(floorRadius, R(k) + Off(k).magnitude);
                var v = verts[k];
                v[0] = Point(k, 0f, 0f, true);
                for (int r = 0; r < Rings; r++)
                {
                    float rr = meshR * (r + 1) / Rings;
                    for (int s = 0; s < Segments; s++)
                    {
                        float a = s * Mathf.PI * 2f / Segments;
                        v[1 + r * Segments + s] = Point(k, Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, r == Rings - 1);
                    }
                }
                meshes[k].vertices = v;
                meshes[k].RecalculateNormals();
                meshes[k].RecalculateBounds();

                var mpb = new MaterialPropertyBlock();
                renderers[k].GetPropertyBlock(mpb);
                Color c = Color.Lerp(colors[k], goldColor, g);
                mpb.SetColor(BaseColorId, c);
                mpb.SetColor(ColorId, c);
                mpb.SetFloat(SmoothId, Mathf.Lerp(0f, 0.7f, g)); // 가루는 광택 없이, 금색만 반짝이게
                mpb.SetFloat(MetalId, Mathf.Lerp(0f, 0.6f, g));
                renderers[k].SetPropertyBlock(mpb);
            }
        }

        float R(int k) { return pileRadius != null && k < pileRadius.Length ? pileRadius[k] : 0.1f; }
        float H(int k) { return pileHeight != null && k < pileHeight.Length ? pileHeight[k] : 0.03f; }
        Vector2 Off(int k) { return pileOffset != null && k < pileOffset.Length ? pileOffset[k] : Vector2.zero; }

        // 더미 하나의 단면: 가운데가 뾰족한 원뿔과 둥근 언덕을 섞은 모양, 가장자리에서 0.
        static float Profile(float x)
        {
            if (x >= 1f) return 0f;
            return 0.5f * (1f - x) + 0.5f * (1f - x * x);
        }

        // 칸 k 하나가 그 자리에 더하는 높이 (애니메이션 정도 amp 포함).
        float Bump(int k, float x, float z)
        {
            float e = amp[k] * amp[k] * (3f - 2f * amp[k]);
            if (e <= 0f) return 0f;
            Vector2 o = Off(k);
            float d = new Vector2(x - o.x, z - o.y).magnitude;
            // 쏟아지는 동안은 가운데부터 솟고 옆으로 퍼진다.
            float spread = Mathf.Lerp(0.45f, 1f, e);
            float p = Profile(d / (R(k) * spread));
            // 큰 덩어리 요철 + 고운 알갱이 요철
            float n = (Mathf.PerlinNoise(x * 45f + k * 17.3f, z * 45f + k * 5.1f) - 0.5f) * 2f * lumpiness
                    + (Mathf.PerlinNoise(x * 220f + k * 3.7f, z * 220f + k * 9.9f) - 0.5f) * 2f * lumpiness * 0.35f;
            float h = (H(k) - (k == 0 ? baseThickness : 0f)) * p + n * p;
            if (k == 0) h += baseThickness;
            return h * e;
        }

        // 칸 k까지 쌓였을 때 그 자리의 가루 표면 높이 (바닥 기준).
        float Surface(int k, float x, float z)
        {
            float y = 0f;
            for (int i = 0; i <= k; i++) y += Bump(i, x, z);
            return y;
        }

        Vector3 Point(int k, float x, float z, bool rim)
        {
            float y;
            if (k == 0) y = Surface(0, x, z);
            else
            {
                float own = Bump(k, x, z);
                // 이 칸이 덮지 않는 곳은 아래 칸 표면 밑으로 살짝 숨겨서 겹쳐 깜빡이지 않게 한다.
                y = own > 0.0005f ? Surface(k, x, z) + 0.0008f : Surface(k - 1, x, z) - 0.002f;
            }
            if (k == 0 && rim) y -= 0.004f; // 바닥 가장자리는 그릇 벽 속으로 살짝 묻는다
            return floorCenter + new Vector3(x, y, z);
        }

        // ── 가루 줄기 (위에서 쏟아지는 작은 알갱이) ──
        void SetPour(bool on, Color c)
        {
            if (!on)
            {
                if (pour != null && pour.isEmitting) pour.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                return;
            }
            if (pour == null) pour = CreatePour();
            var main = pour.main;
            main.startColor = c;
            if (!pour.isEmitting) pour.Play();
        }

        ParticleSystem CreatePour()
        {
            var go = new GameObject("PowderPour");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = floorCenter + Vector3.up * pourHeight;
            go.transform.rotation = Quaternion.LookRotation(Vector3.down);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.2f; // 가루 더미 꼭대기쯤에서 사라지게 (떨어지는 거리 약 0.35m)
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.016f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 260f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4f;
            shape.radius = 0.012f;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            if (fxMaterial != null) rend.sharedMaterial = fxMaterial;
            // 떨어지는 방향으로 늘여 그려서 점이 아니라 흘러내리는 줄기처럼 보이게 한다.
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.03f;
            rend.lengthScale = 1f;
            return ps;
        }

        void Sparkle()
        {
            Vector3 top = transform.TransformPoint(floorCenter + Vector3.up * Surface(Slots - 1, 0f, 0f));
            PotionVisuals.Burst(top, goldColor, 40, 0.6f, 0.02f, fxMaterial, -0.05f);
        }
    }
}
