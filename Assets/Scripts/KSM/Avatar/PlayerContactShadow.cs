using LGH;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CoopDemo
{
    // 모델의 스킨/그림자 맵 대신 바닥에만 투영하는 로컬 접지 그림자.
    // 각 클라이언트가 동기화된 플레이어 위치로 그리므로 추가 RPC/NetworkObject가 필요 없다.
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkPlayer))]
    public class PlayerContactShadow : MonoBehaviour
    {
        [SerializeField] Material shadowMaterial;
        [SerializeField] Vector2 radius = new Vector2(0.45f, 0.34f);
        [SerializeField, Range(0f, 1f)] float opacity = 0.6f;
        [SerializeField, Min(0.1f)] float maxHeight = 1.6f;
        [SerializeField, Min(0.001f)] float surfaceOffset = 0.02f;
        [SerializeField] LayerMask groundLayers = ~0;
        [Tooltip("진단용: 그림자를 밝은 자홍색으로 표시해 어두운 바닥에서의 대비 문제를 구분한다.")]
        [SerializeField] bool debugFootprint;

        const int Grid = 5;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Vector3[] vertices = new Vector3[Grid * Grid];
        readonly Vector3[] normals = new Vector3[Grid * Grid];
        readonly bool[] valid = new bool[Grid * Grid];
        readonly int[] indices = new int[(Grid - 1) * (Grid - 1) * 6];
        NetworkPlayer player;
        CharacterController controller;
        GameObject shadowObject;
        MeshRenderer shadowRenderer;
        Mesh mesh;
        MaterialPropertyBlock properties;
        string projectionStatus = "Not initialized";
        float lastHeight, lastAlpha;
        int lastIndexCount;

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
            // 별도 루트로 두어 아바타 숨김/100배 FBX 스케일의 영향을 받지 않는다.
            shadowObject = new GameObject("PlayerContactShadow", typeof(MeshFilter), typeof(MeshRenderer));
            shadowObject.layer = gameObject.layer;
            SceneManager.MoveGameObjectToScene(shadowObject, gameObject.scene);
            mesh = new Mesh { name = "Player contact shadow projection" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            var uv = new Vector2[vertices.Length];
            for (int z = 0; z < Grid; z++)
                for (int x = 0; x < Grid; x++)
                    uv[z * Grid + x] = new Vector2(x / (Grid - 1f), z / (Grid - 1f));
            mesh.uv = uv;
            shadowObject.GetComponent<MeshFilter>().sharedMesh = mesh;
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
            // 벽/급경사를 건너뛰어 그 뒤의 바닥에 투영하지 않는다.
            return found && ground.normal.y >= 0.65f;
        }

        void LateUpdate()
        {
            if (shadowRenderer == null) return;
            shadowRenderer.enabled = false;
            projectionStatus = "Waiting for network spawn";
            lastAlpha = 0f;
            lastIndexCount = 0;
            if (!player.IsSpawned) return;
            Vector3 feet = controller != null
                ? transform.TransformPoint(controller.center - Vector3.up * controller.height * 0.5f)
                : transform.position;
            Vector3 origin = feet + Vector3.up * 0.4f;
            projectionStatus = "No ground surface";
            if (!GroundAt(origin, out var ground)) return;
            float height = Mathf.Max(0f, feet.y - ground.point.y);
            lastHeight = height;
            float fade = 1f - Mathf.InverseLerp(0.05f, maxHeight, height);
            projectionStatus = "Above shadow height limit";
            if (fade <= 0.01f) return;
            // 렌더러는 실제 그림자 위치에 두고 메시 좌표는 작은 로컬 값으로 유지한다.
            // 원점에 둔 렌더러/납작한 월드 좌표 메시가 가림 판정에 의존하지 않게 한다.
            shadowObject.transform.SetPositionAndRotation(ground.point, Quaternion.identity);
            Vector3 shadowOrigin = ground.point;
            float spread = Mathf.Lerp(1f, 1.35f, 1f - fade);
            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            for (int z = 0; z < Grid; z++)
            for (int x = 0; x < Grid; x++)
            {
                int i = z * Grid + x;
                Vector3 offset = right * ((x / (Grid - 1f) * 2f - 1f) * radius.x * spread)
                    + forward * ((z / (Grid - 1f) * 2f - 1f) * radius.y * spread);
                valid[i] = GroundAt(origin + offset, out var hit);
                vertices[i] = valid[i] ? hit.point + hit.normal * surfaceOffset - shadowOrigin : Vector3.zero;
                normals[i] = valid[i] ? hit.normal : ground.normal;
            }
            int length = 0;
            for (int z = 0; z < Grid - 1; z++)
            for (int x = 0; x < Grid - 1; x++)
            {
                int a = z * Grid + x, b = a + 1, c = a + Grid, d = c + 1;
                AddTriangle(a, c, b, ref length);
                AddTriangle(b, c, d, ref length);
            }
            projectionStatus = "No connected ground triangles";
            if (length == 0) return;
            mesh.vertices = vertices;
            mesh.SetTriangles(indices, 0, length, 0, false);
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.Expand(0.05f); // 완전히 납작한 bounds의 경계 가림을 방지한다.
            mesh.bounds = bounds;
            lastAlpha = debugFootprint ? 0.9f : opacity * fade;
            properties.SetColor(ColorId, debugFootprint
                ? new Color(1f, 0f, 1f, lastAlpha)
                : new Color(0f, 0f, 0f, lastAlpha));
            shadowRenderer.SetPropertyBlock(properties);
            shadowRenderer.enabled = true;
            lastIndexCount = length;
            projectionStatus = "Rendering";
        }

        [ContextMenu("Log Contact Shadow Diagnostics")]
        void LogDiagnostics()
        {
            string shader = shadowMaterial != null && shadowMaterial.shader != null
                ? $"{shadowMaterial.shader.name} (supported={shadowMaterial.shader.isSupported})" : "missing";
            Debug.Log($"[ContactShadow] {name}: {projectionStatus}, height={lastHeight:F3}m, " +
                $"alpha={lastAlpha:F3}, triangles={lastIndexCount / 3}, shader={shader}, " +
                $"rendererEnabled={shadowRenderer != null && shadowRenderer.enabled}, " +
                $"bounds={(shadowRenderer != null ? shadowRenderer.bounds.ToString() : "none")}", this);
        }

        void AddTriangle(int a, int b, int c, ref int length)
        {
            if (!valid[a] || !valid[b] || !valid[c]) return;
            // 계단의 서로 다른 단/절벽을 삼각형으로 연결해 공중에 그림자가 뜨지 않게 한다.
            if (Vector3.Dot(normals[a], normals[b]) < 0.95f || Vector3.Dot(normals[a], normals[c]) < 0.95f) return;
            if (Mathf.Abs(Vector3.Dot(vertices[b] - vertices[a], normals[a])) > 0.04f ||
                Mathf.Abs(Vector3.Dot(vertices[c] - vertices[a], normals[a])) > 0.04f) return;
            indices[length++] = a;
            indices[length++] = b;
            indices[length++] = c;
        }

        void OnDisable() { if (shadowRenderer != null) shadowRenderer.enabled = false; }

        void OnDestroy()
        {
            if (shadowObject != null) Destroy(shadowObject);
            if (mesh != null) Destroy(mesh);
        }
    }
}
