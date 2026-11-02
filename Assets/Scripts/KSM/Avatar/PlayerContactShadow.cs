using LGH;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CoopDemo
{
    // 모델의 스킨/그림자 맵 대신 바닥에만 그리는 로컬 타원형 접지 그림자.
    // 각 클라이언트가 동기화된 플레이어 위치로 그리므로 추가 RPC/NetworkObject가 필요 없다.
    //
    // 2026-10-30: 바닥에 메시를 덮어씌우던 방식(계단에서 삼각형이 단차마다 끊겨 평행사변형 조각으로 갈라졌다)을 버리고,
    // 발 주변을 감싸는 상자 하나를 그려 깊이 버퍼에서 바닥 위치를 복원하는 투영(데칼) 셰이더(PlayerContactShadowDecal)로 바꿨다.
    // 바닥 모양에 상관없이 타원이 그대로 얹히고, 계단에서는 서 있는 단이 가장 진하고 아랫단으로 갈수록 옅어진다.
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkPlayer))]
    public class PlayerContactShadow : MonoBehaviour
    {
        [SerializeField] Material shadowMaterial;
        [SerializeField] Vector2 radius = new Vector2(0.85f, 0.7f);
        [SerializeField, Range(0f, 1f)] float opacity = 0.9f;
        [SerializeField, Min(0.1f)] float maxHeight = 1.6f;
        [SerializeField] LayerMask groundLayers = ~0;
        [Tooltip("서 있는 바닥보다 이만큼 위까지는 그림자가 그대로 보인다(그 위는 사라짐). 캐릭터 신발이 칠해지지 않을 만큼 작게.")]
        [SerializeField, Min(0f)] float upAllowance = 0.03f;
        [SerializeField, Min(0.01f)] float upFade = 0.12f;
        [Tooltip("서 있는 바닥보다 아래(아랫단)로 이만큼까지 옅어지며 이어진다.")]
        [SerializeField, Min(0.05f)] float downFade = 0.5f;
        [Tooltip("진단용: 그림자를 밝은 자홍색으로 표시해 어두운 바닥에서의 대비 문제를 구분한다.")]
        [SerializeField] bool debugFootprint;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int CenterId = Shader.PropertyToID("_Center");
        static readonly int AxesId = Shader.PropertyToID("_Axes");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static Mesh cubeMesh;

        readonly RaycastHit[] hits = new RaycastHit[32];
        NetworkPlayer player;
        CharacterController controller;
        GameObject shadowObject;
        MeshRenderer shadowRenderer;
        MaterialPropertyBlock properties;
        string projectionStatus = "Not initialized";
        float lastHeight, lastAlpha;

        void Awake()
        {
            player = GetComponent<NetworkPlayer>();
            controller = GetComponent<CharacterController>();
            if (shadowMaterial == null)
            {
                projectionStatus = "Missing material";
                Debug.LogWarning("[ContactShadow] 접지 그림자 머티리얼이 없습니다.", this);
                enabled = false;
                return;
            }
            if (cubeMesh == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
            }
            // 별도 루트로 두어 아바타 숨김/FBX 스케일의 영향을 받지 않는다.
            shadowObject = new GameObject("PlayerContactShadow", typeof(MeshFilter), typeof(MeshRenderer));
            shadowObject.layer = gameObject.layer;
            SceneManager.MoveGameObjectToScene(shadowObject, gameObject.scene);
            shadowObject.GetComponent<MeshFilter>().sharedMesh = cubeMesh;
            shadowRenderer = shadowObject.GetComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = shadowMaterial;
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;
            shadowRenderer.lightProbeUsage = LightProbeUsage.Off;
            shadowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shadowRenderer.allowOcclusionWhenDynamic = false;
            shadowRenderer.enabled = false;
            properties = new MaterialPropertyBlock();
        }

        // RaycastNonAlloc의 반환 순서는 보장되지 않는다. 다른 플레이어/자신을 제외하고 가장 가까운 표면을 고른다.
        bool GroundAt(Vector3 origin, out RaycastHit ground)
        {
            ground = default;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, maxHeight + 0.4f,
                groundLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false; // 잘린 결과로 아래 층을 잘못 선택하지 않는다.
            float nearest = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.GetComponentInParent<NetworkPlayer>() != null) continue;
                if (hit.distance >= nearest) continue;
                nearest = hit.distance;
                ground = hit;
                found = true;
            }
            return found;
        }

        void LateUpdate()
        {
            if (shadowRenderer == null) return;
            shadowRenderer.enabled = false;
            projectionStatus = "Waiting for network spawn";
            lastAlpha = 0f;
            if (!player.IsSpawned) return;
            Vector3 feet = controller != null
                ? transform.TransformPoint(controller.center - Vector3.up * controller.height * 0.5f)
                : transform.position;
            projectionStatus = "No ground surface";
            if (!GroundAt(feet + Vector3.up * 0.4f, out var ground)) return;
            float height = Mathf.Max(0f, feet.y - ground.point.y);
            lastHeight = height;
            float fade = 1f - Mathf.InverseLerp(0.05f, maxHeight, height);
            projectionStatus = "Above shadow height limit";
            if (fade <= 0.01f) return;

            float spread = Mathf.Lerp(1f, 1.35f, 1f - fade);   // 높이 뜰수록 넓고 옅게
            float yaw = transform.eulerAngles.y;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            float rx = radius.x * spread, rz = radius.y * spread;

            // 상자: 가로세로는 타원이 들어갈 만큼, 높이는 서 있는 바닥 위 upAllowance+upFade ~ 아래 downFade
            float top = upAllowance + upFade, bottom = downFade;
            float boxHeight = top + bottom;
            // 회전한 타원이 상자 밖으로 잘리지 않게 대각선 길이만큼 여유를 둔다
            float half = Mathf.Max(rx, rz) * 1.05f;
            var t = shadowObject.transform;
            t.SetPositionAndRotation(new Vector3(feet.x, ground.point.y + (top - bottom) * 0.5f, feet.z), Quaternion.identity);
            t.localScale = new Vector3(half * 2f, boxHeight, half * 2f);

            lastAlpha = (debugFootprint ? 0.9f : opacity) * fade;
            properties.SetColor(ColorId, debugFootprint ? new Color(1f, 0f, 1f, 1f) : new Color(0f, 0f, 0f, 1f));
            properties.SetVector(CenterId, new Vector4(feet.x, ground.point.y, feet.z, lastAlpha));
            properties.SetVector(AxesId, new Vector4(right.x, right.z, rx, rz));
            properties.SetVector(FadeId, new Vector4(upAllowance, upFade, downFade, 0f));
            shadowRenderer.SetPropertyBlock(properties);
            shadowRenderer.enabled = true;
            projectionStatus = "Rendering";
        }

        [ContextMenu("Log Contact Shadow Diagnostics")]
        void LogDiagnostics()
        {
            string shader = shadowMaterial != null && shadowMaterial.shader != null
                ? $"{shadowMaterial.shader.name} (supported={shadowMaterial.shader.isSupported})" : "missing";
            Debug.Log($"[ContactShadow] {name}: {projectionStatus}, height={lastHeight:F3}m, " +
                $"alpha={lastAlpha:F3}, shader={shader}, isOwner={player != null && player.IsOwner}, radius={radius}, " +
                $"rendererEnabled={shadowRenderer != null && shadowRenderer.enabled}", this);
        }

        void OnDisable() { if (shadowRenderer != null) shadowRenderer.enabled = false; }

        void OnDestroy()
        {
            if (shadowObject != null) Destroy(shadowObject);
        }
    }
}
