using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CoopDemo
{
    // 그래픽 품질 "낮음"일 때, 지붕 아래(실내)에서는 그림자 거리를 짧게 줄인다 (최적화, 2026-11-05).
    // 달빛 방향광은 야외 전용(렌더링 레이어)이라 실내에서 보이는 건 창밖의 먼 그림자뿐인데, 그걸 위해 그림자 맵에
    // 건물 바깥 나무·울타리 수백 개를 계속 그리고 있었다. 실내에서는 가까운 것만 그리게 하면 드로우콜·삼각형이 크게 준다
    // (측정: 1층 현관 홀에서 달빛 그림자 비용 드로우콜 약 560, 삼각형 약 210만).
    // 높음 단계에서는 아무것도 하지 않는다(저사양이 아닌 PC는 그림자를 그대로 둔다).
    // URP 에셋의 shadowDistance를 직접 바꾸므로, 끝날 때/단계가 바뀔 때 원래 값으로 되돌린다.
    public class IndoorShadowDistance : MonoBehaviour
    {
        const float IndoorDistance = 12f;       // 실내에서의 그림자 거리(m)
        const float ChangeSpeed = 30f;          // 거리가 바뀌는 속도(m/s) — 문턱을 지날 때 그림자가 툭 튀지 않게
        const float CheckInterval = 0.2f;
        const float CeilingCheckHeight = 20f;   // 머리 위 이 높이 안에 뭔가 있으면 실내로 본다

        UniversalRenderPipelineAsset tracked;
        float baseDistance;
        float current;
        float target;
        float nextCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (FindFirstObjectByType<IndoorShadowDistance>() != null) return;
            var go = new GameObject("IndoorShadowDistance (runtime)");
            go.AddComponent<IndoorShadowDistance>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            var asset = UniversalRenderPipeline.asset;
            if (asset != tracked)
            {
                Restore();   // 단계가 바뀌어 다른 에셋이 됐다 → 이전 에셋을 되돌리고 새 에셋을 기준으로 시작
                tracked = asset;
                if (asset == null) return;
                baseDistance = asset.shadowDistance;
                current = target = baseDistance;
            }
            if (tracked == null) return;

            if (GraphicsQuality.AppliedTier != GraphicsQuality.Low)
            {
                if (!Mathf.Approximately(tracked.shadowDistance, baseDistance)) tracked.shadowDistance = baseDistance;
                current = target = baseDistance;
                return;
            }

            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + CheckInterval;
                target = IsIndoors() ? Mathf.Min(IndoorDistance, baseDistance) : baseDistance;
            }
            current = Mathf.MoveTowards(current, target, ChangeSpeed * Time.unscaledDeltaTime);
            if (!Mathf.Approximately(tracked.shadowDistance, current)) tracked.shadowDistance = current;
        }

        // 머리 위에 지붕·천장이 있으면 실내
        static bool IsIndoors()
        {
            var pi = LGH.PlayerInteractor.Active;
            if (pi == null || pi.viewCamera == null) return false;
            return Physics.Raycast(pi.viewCamera.transform.position, Vector3.up, CeilingCheckHeight, ~0, QueryTriggerInteraction.Ignore);
        }

        void Restore()
        {
            if (tracked != null && !Mathf.Approximately(tracked.shadowDistance, baseDistance)) tracked.shadowDistance = baseDistance;
        }

        void OnDisable() { Restore(); }
        void OnApplicationQuit() { Restore(); }
    }
}
