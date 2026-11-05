using System.Collections.Generic;
using UnityEngine;

namespace CoopDemo
{
    // 밝은 외곽선(OutlineRendererFeature)을 두를 물건. 켜져 있는 동안 자기 렌더러들에 "외곽선 렌더링 레이어" 비트를 얹고,
    // 꺼지면 그 비트만 뺀다(원래 레이어는 건드리지 않아 조명·그림자는 그대로다).
    //  - Always          : 항상 켠다 (책, 쪽지, 기록일지)
    //  - WhenInteractable: 지금 집을 수 있을 때만 켠다 (열쇠: 숨겨져 있을 땐 꺼 두었다가 집을 수 있게 되면 켠다)
    // 보통 직접 붙이지 않는다. OutlineAutoApply가 게임이 시작될 때 해당하는 물건을 종류별로 찾아 붙인다.
    [DisallowMultipleComponent]
    public class OutlineTarget : MonoBehaviour
    {
        public enum Mode { Always, WhenInteractable }

        // 외곽선 전용 렌더링 레이어 (URP 설정의 "Light Layer 5", 값 32). 이 비트를 가진 렌더러만 마스크에 그려진다.
        public const int RenderingLayerIndex = 5;
        public const uint RenderingLayerBit = 1u << RenderingLayerIndex;

        [SerializeField] Mode mode = Mode.Always;
        [Tooltip("비우면 이 물건 아래의 모든 렌더러(꺼진 것 포함)")]
        [SerializeField] Renderer[] renderers;
        [Tooltip("플레이어 시점에서 이 거리보다 멀면 외곽선을 끈다 (0 = 제한 없음)")]
        [SerializeField] float maxDistance = 20f;

        static readonly List<OutlineTarget> all = new List<OutlineTarget>();
        static int appliedCount;
        // 지금 외곽선이 켜진 물건이 하나라도 있는가 (없으면 렌더러 피처가 아무 일도 안 한다)
        public static bool AnyApplied { get { return appliedCount > 0; } }

        bool applied;
        IInteractable interactable;
        LGH.WorldItem worldItem;
        Collider[] colliders;

        public Mode CurrentMode { get { return mode; } set { mode = value; } }
        public bool IsApplied { get { return applied; } }

        public void Configure(Mode m, float maxDist)
        {
            mode = m;
            maxDistance = maxDist;
        }

        // 외곽선을 두를 렌더러를 직접 정한다 (예: 받침대는 빼고 책·쪽지만). 이전에 얹은 레이어 비트는 걷어 낸다.
        public void SetRenderers(Renderer[] list)
        {
            if (applied) ApplyBit(false);
            renderers = list;
            if (applied) ApplyBit(true);
        }

        void Awake()
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            interactable = GetComponent<IInteractable>();
            worldItem = GetComponent<LGH.WorldItem>();
            colliders = GetComponentsInChildren<Collider>(true);
        }

        void OnEnable() { all.Add(this); }

        void OnDisable()
        {
            all.Remove(this);
            SetApplied(false);
        }

        void Update()
        {
            SetApplied(ShouldShow());
        }

        bool ShouldShow()
        {
            if (!HasVisibleRenderer()) return false;
            if (mode == Mode.WhenInteractable && !Pickable()) return false;
            if (maxDistance > 0f)
            {
                var cam = ViewCamera();
                if (cam != null && (transform.position - cam.position).sqrMagnitude > maxDistance * maxDistance) return false;
            }
            return true;
        }

        // 집을 수 있는 상태인가. 들기 시스템/별 상자 물건은 자기 규칙(CanInteract)을 따르고, LGH 바닥 아이템은 "있고 줍을 수 있을 때".
        bool Pickable()
        {
            if (interactable != null) return interactable.CanInteract;
            if (worldItem != null)
            {
                if (!worldItem.IsPresent || worldItem.item == null) return false;
                foreach (var c in colliders) if (c != null && c.enabled) return true;
                return false;
            }
            foreach (var c in colliders) if (c != null && c.enabled) return true;
            return false;
        }

        bool HasVisibleRenderer()
        {
            if (!gameObject.activeInHierarchy) return false;
            foreach (var r in renderers) if (r != null && r.enabled && r.gameObject.activeInHierarchy) return true;
            return false;
        }

        static Transform ViewCamera()
        {
            var pi = LGH.PlayerInteractor.Active;
            if (pi != null && pi.viewCamera != null) return pi.viewCamera.transform;
            return null;
        }

        void SetApplied(bool on)
        {
            if (applied == on && !(on && NeedsRefresh())) return;
            ApplyBit(on);
            if (applied != on) appliedCount += on ? 1 : -1;
            applied = on;
        }

        void ApplyBit(bool on)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (on) r.renderingLayerMask |= RenderingLayerBit;
                else r.renderingLayerMask &= ~RenderingLayerBit;
            }
        }

        // 다른 스크립트가 렌더러의 레이어를 되돌렸으면 다시 얹는다 (드물지만 안전장치)
        bool NeedsRefresh()
        {
            if (renderers == null) return false;
            foreach (var r in renderers)
                if (r != null && r.enabled && (r.renderingLayerMask & RenderingLayerBit) == 0) return true;
            return false;
        }

        void OnDestroy()
        {
            if (applied) { appliedCount--; applied = false; }
        }
    }
}
