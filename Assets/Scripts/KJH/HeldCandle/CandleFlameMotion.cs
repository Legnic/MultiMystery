using UnityEngine;

namespace CandleFx
{
    // 손 양초 불꽃을 "진짜 불"처럼 움직인다.
    // - 손(카메라)이 기울어도 불꽃은 항상 월드 위쪽을 향해 선다.
    // - 양초가 움직이면(걷기, 고개 돌리기) 불꽃 끝이 움직임 반대쪽으로 끌려가 기울었다가 스프링처럼 돌아온다.
    // - 위아래로 빠르게 늘었다 줄었다 하고(늘어나면 가늘어짐), 천천히 좌우로 일렁인다.
    // - 불꽃 판은 위쪽 축을 유지한 채 보는 카메라 쪽으로 돌고(원기둥 빌보드), 빛 번짐은 카메라를 정면으로 본다.
    // 밝기는 referenceLight(손 양초 빛)의 현재 밝기 비율을 따라가서, 흔들림과 다 타 가는 어두워짐이 같이 보인다.
    // 이 컴포넌트는 심지 끝(pivot) 오브젝트에 붙는다. 크기 값은 pivot 기준 단위(1인칭에서 미터).
    public class CandleFlameMotion : MonoBehaviour
    {
        [Header("구성")]
        public Transform pivot;
        public Transform core;
        public Transform halo;
        public Light referenceLight;
        [Tooltip("비워 두면 Camera.main")]
        public Camera viewCamera;

        [Header("크기 (pivot 기준)")]
        public float coreHeight = 0.045f;
        public float coreWidth = 0.019f;
        public float haloSize = 0.11f;
        [Range(0f, 1f)] public float haloHeight01 = 0.4f;

        [Header("밝기")]
        public float coreIntensity = 2.2f;
        public float haloIntensity = 0.3f;

        [Header("기울기 (움직임 반대쪽으로 끌림)")]
        [Tooltip("초당 1m 움직일 때 기우는 각도(도)")]
        public float leanPerSpeed = 10f;
        public float maxLean = 30f;
        [Tooltip("클수록 빨리 제자리로 돌아온다")]
        public float spring = 70f;
        [Tooltip("작을수록 돌아올 때 더 출렁인다")]
        public float damping = 8f;

        [Header("일렁임")]
        public float swayAngle = 3.5f;
        public float swaySpeed = 1.3f;
        public float flickerSpeed = 11f;
        [Range(0f, 0.4f)] public float flickerStretch = 0.13f;
        [Tooltip("빠르게 움직일 때 길쭉해지는 최대 비율")]
        public float maxSpeedStretch = 0.3f;

        [Header("양초 몸체 (선택)")]
        [Tooltip("밀랍 발광을 불꽃 밝기에 맞춰 흔들 렌더러 (발광 맵이 밀랍에만 있어야 한다)")]
        public Renderer bodyRenderer;
        [ColorUsage(false, true)] public Color bodyEmission = new Color(0.19f, 0.13f, 0.07f);
        [Tooltip("양초만 비추는 작은 빛. 불꽃이 기울고 늘어나는 만큼 같이 움직여 하이라이트가 일렁인다")]
        public Light selfLight;
        [Range(0f, 2f)] public float selfLightFollow = 1f;

        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        Vector3 lastPos, smoothVel, selfRestLocal;
        bool hasLast;
        Vector2 lean, leanVel;
        float seed, refLight;
        Renderer coreR, haloR;
        MaterialPropertyBlock mpb, bodyMpb;

        void Awake()
        {
            if (pivot == null) pivot = transform;
            seed = Random.value * 100f;
            coreR = core != null ? core.GetComponent<Renderer>() : null;
            haloR = halo != null ? halo.GetComponent<Renderer>() : null;
            mpb = new MaterialPropertyBlock();
            bodyMpb = new MaterialPropertyBlock();
            if (referenceLight != null) refLight = referenceLight.intensity;
            if (selfLight != null) selfRestLocal = pivot.InverseTransformPoint(selfLight.transform.position);
            flickerSource = GetComponentInParent<LGH.CandleFlicker>();
        }

        LGH.CandleFlicker flickerSource;

        // 흔들림·꺼져감 밝기 비율 (1 = 새 양초). 상대 화면에서 방을 비추는 빛만 어둡게 해도(intensityScale) 불꽃은 그대로 보이게,
        // CandleFlicker가 있으면 배율을 뺀 비율을 쓴다.
        float CurrentRatio()
        {
            if (flickerSource != null && flickerSource.isActiveAndEnabled) return flickerSource.Ratio;
            return referenceLight != null && refLight > 0f ? referenceLight.intensity / refLight : 1f;
        }

        void OnEnable()
        {
            // 다시 켜질 때(양초에 불 붙임, 순간이동 뒤 등) 이전 위치로 큰 속도가 계산되지 않게 초기화
            hasLast = false;
            lean = leanVel = Vector2.zero;
            smoothVel = Vector3.zero;
        }

        void OnDisable()
        {
            // 불이 꺼지면 밀랍도 더 이상 빛나지 않는다
            if (bodyRenderer != null && bodyMpb != null)
            {
                bodyRenderer.GetPropertyBlock(bodyMpb);
                bodyMpb.SetColor(EmissionId, Color.black);
                bodyRenderer.SetPropertyBlock(bodyMpb);
            }
        }

        void LateUpdate() => Step(Time.deltaTime, Time.time);

        public void Step(float dt, float time)
        {
            if (pivot == null) return;
            dt = Mathf.Max(dt, 1e-4f);

            Vector3 p = pivot.position;
            if (hasLast)
            {
                Vector3 v = (p - lastPos) / dt;
                if (v.sqrMagnitude > 100f) v = Vector3.zero; // 초속 10m 넘으면 순간이동으로 보고 무시
                smoothVel = Vector3.Lerp(smoothVel, v, 1f - Mathf.Exp(-dt * 12f));
            }
            lastPos = p;
            hasLast = true;

            // 불꽃 끝이 향할 방향(월드 XZ, 도). 움직임 반대쪽 + 느린 일렁임
            Vector2 target = new Vector2(-smoothVel.x, -smoothVel.z) * leanPerSpeed;
            target += new Vector2(Mathf.PerlinNoise(seed, time * swaySpeed) - 0.5f,
                                  Mathf.PerlinNoise(time * swaySpeed, seed + 7f) - 0.5f) * (2f * swayAngle);
            target = Vector2.ClampMagnitude(target, maxLean);

            Vector2 acc = (target - lean) * spring - leanVel * damping;
            leanVel += acc * dt;
            lean = Vector2.ClampMagnitude(lean + leanVel * dt, maxLean);

            Vector3 dir = new Vector3(lean.x, 0f, lean.y);
            float ang = dir.magnitude;
            Vector3 up = ang > 1e-3f
                ? Quaternion.AngleAxis(ang, Vector3.Cross(Vector3.up, dir / ang)) * Vector3.up
                : Vector3.up;

            float flicker = (Mathf.PerlinNoise(time * flickerSpeed, seed + 3f) - 0.5f) * 2f;
            float stretch = (1f + flicker * flickerStretch) * (1f + Mathf.Min(smoothVel.magnitude * 0.08f, maxSpeedStretch));
            float h = coreHeight * stretch;
            float w = coreWidth / Mathf.Sqrt(stretch);
            float s = pivot.lossyScale.x;

            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : p - pivot.forward;

            if (core != null)
            {
                Vector3 toCam = Vector3.ProjectOnPlane(camPos - p, up);
                if (toCam.sqrMagnitude < 1e-8f) toCam = Vector3.ProjectOnPlane(-pivot.forward, up);
                core.SetPositionAndRotation(p + up * (h * 0.5f * s), Quaternion.LookRotation(toCam, up));
                core.localScale = new Vector3(w, h, 1f);
            }
            if (halo != null)
            {
                Vector3 hp = p + up * (h * haloHeight01 * s);
                Vector3 look = camPos - hp;
                if (look.sqrMagnitude < 1e-8f) look = -pivot.forward;
                halo.SetPositionAndRotation(hp, Quaternion.LookRotation(look, cam != null ? cam.transform.up : Vector3.up));
                float hs = haloSize * (1f + flicker * 0.05f);
                halo.localScale = new Vector3(hs, hs, 1f);
            }

            float ratio = CurrentRatio();
            if (coreR != null) { mpb.SetFloat(IntensityId, coreIntensity * ratio); coreR.SetPropertyBlock(mpb); }
            if (haloR != null) { mpb.SetFloat(IntensityId, haloIntensity * ratio); haloR.SetPropertyBlock(mpb); }

            if (selfLight != null)
            {
                // 빛은 항상 "보는 사람 쪽"에 둔다 (프리팹에 둔 거리·높이 그대로). 1인칭에선 원래 자리와 거의 같고,
                // 상대 화면(아바타 손)에선 몸 쪽이 아니라 보는 사람 쪽을 비춰서 양초가 까맣게 보이지 않는다.
                float horiz = new Vector2(selfRestLocal.x, selfRestLocal.z).magnitude * s;
                Vector3 toViewer = Vector3.ProjectOnPlane(camPos - p, Vector3.up);
                if (toViewer.sqrMagnitude < 1e-8f) toViewer = Vector3.ProjectOnPlane(pivot.TransformDirection(new Vector3(selfRestLocal.x, 0f, selfRestLocal.z)), Vector3.up);
                Vector3 rest = p + Vector3.up * (selfRestLocal.y * s) + toViewer.normalized * horiz;
                // 불꽃 가운데가 움직인 만큼 빛도 옮긴다 (가만히 있을 때의 불꽃 가운데 기준)
                Vector3 flameShift = up * (h * 0.5f * s) - Vector3.up * (coreHeight * 0.5f * s);
                selfLight.transform.position = rest + flameShift * selfLightFollow;
            }
            if (bodyRenderer != null)
            {
                bodyRenderer.GetPropertyBlock(bodyMpb);
                bodyMpb.SetColor(EmissionId, bodyEmission * (ratio * (1f + flicker * 0.08f)));
                bodyRenderer.SetPropertyBlock(bodyMpb);
            }
        }
    }
}
