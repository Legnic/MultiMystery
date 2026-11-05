using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

namespace CoopDemo
{
    // 밝은 외곽선 렌더러 피처. OutlineTarget이 켜져 있는 물건(= 외곽선 렌더링 레이어를 가진 렌더러)만 대상이다.
    //  1) 투명 물체까지 그린 뒤: 대상을 단색 마스크 텍스처에 그린다. 장면 깊이로 가려진 부분은 빠진다(벽 뒤 물건은 안 보임).
    //  2) 후처리가 끝난 뒤: 마스크를 가로·세로로 번지게(가우시안 팽창) 해서 카메라 화면 위에 밝은 띠로 얹는다.
    //     후처리 뒤에 얹어서 톤매핑에 색이 바래지 않고, 물체 안쪽은 그대로 둔다.
    // 대상이 하나도 없으면 패스를 아예 등록하지 않아 비용이 없다.
    // 설정 값(색·두께)은 이 피처의 Inspector에서 바꾼다. 셰이더는 빌드에 포함되도록 직접 연결해 둔다.
    public class OutlineRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader maskShader;
        [SerializeField] Shader compositeShader;

        [Header("모양")]
        [Tooltip("외곽선 색")]
        public Color color = new Color(0.25f, 1f, 0.6f, 1f);
        [Tooltip("번짐의 크기(픽셀, 1080p 기준). 클수록 띠가 두껍고 번짐이 넓다")]
        [Range(1f, 16f)] public float widthPixels = 5f;
        [Tooltip("띠가 단단해지기 시작하는 문턱(낮을수록 두껍다)")]
        [Range(0f, 1f)] public float bandLow = 0.3f;
        [Tooltip("띠가 완전히 단단해지는 문턱")]
        [Range(0f, 1f)] public float bandHigh = 0.65f;
        [Tooltip("띠 바깥으로 옅게 번지는 빛의 세기")]
        [Range(0f, 1f)] public float haloStrength = 0.35f;
        [Tooltip("진단용: 켜면 마스크(빨강)와 번짐(초록)을 그대로 보여 준다")]
        public bool debugView;

        Material maskMaterial, compositeMaterial;
        MaskPass maskPass;
        CompositePass compositePass;
        // 주의: 이 Unity 버전은 ScriptableObject의 생성자·필드 초기값을 건너뛴다(에디터에서 CreateInstance로 만들 때).
        // 그래서 목록 같은 것은 Create()에서 만들고, 직렬화 값(색·두께 등)은 에셋에 저장된 값을 쓴다.
        List<ShaderTagId> tags;

        // URP가 렌더러 피처마다 읽는 내부 속성(supportedRenderingFeatures)은 부모 클래스의 생성자에서 채워진다.
        // 그런데 이 Unity 버전은 Assembly-CSharp의 ScriptableObject를 만들 때 생성자를 건너뛰어서 null로 남고,
        // URP가 그걸 읽다가 매 프레임 NullReferenceException이 나며 화면이 까맣게 된다 → 직접 채워 준다.
        static readonly System.Reflection.PropertyInfo SupportedFeaturesProp = typeof(ScriptableRendererFeature).GetProperty(
            "supportedRenderingFeatures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        void EnsureSupportedFeatures()
        {
            if (SupportedFeaturesProp != null && SupportedFeaturesProp.GetValue(this) == null)
                SupportedFeaturesProp.SetValue(this, new ScriptableRenderer.RenderingFeatures());
        }

        public override void Create()
        {
            EnsureSupportedFeatures();
            tags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
            };
#if UNITY_EDITOR
            if (maskShader == null) maskShader = Shader.Find("Hidden/KSM/OutlineMask");
            if (compositeShader == null) compositeShader = Shader.Find("Hidden/KSM/OutlineComposite");
#endif
            DestroyMaterials();
            if (maskShader != null) maskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            if (compositeShader != null) compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
            maskPass = new MaskPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
            compositePass = new CompositePass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!OutlineTarget.AnyApplied || maskMaterial == null || compositeMaterial == null) return;
            var cam = renderingData.cameraData.camera;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
            // MSAA를 켜면 마스크와 깊이의 샘플 수가 달라져 이 방식이 안 맞는다(현재 PC/모바일 설정은 모두 끔)
            if (renderingData.cameraData.cameraTargetDescriptor.msaaSamples > 1) return;

            maskPass.Setup(this, maskMaterial, tags);
            compositePass.Setup(this, compositeMaterial, maskPass);
            renderer.EnqueuePass(maskPass);
            renderer.EnqueuePass(compositePass);
        }

        protected override void Dispose(bool disposing)
        {
            DestroyMaterials();
        }

        void DestroyMaterials()
        {
            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(compositeMaterial);
            maskMaterial = null;
            compositeMaterial = null;
        }

        // ---------------- 1) 마스크 ----------------
        class MaskPass : ScriptableRenderPass
        {
            class PassData { public RendererListHandle list; }

            Material material;
            List<ShaderTagId> tags;
            // 이번 프레임에 만든 마스크 (합성 패스가 이어서 쓴다)
            public TextureHandle Mask;
            public bool Recorded;

            public void Setup(OutlineRendererFeature f, Material m, List<ShaderTagId> t)
            {
                material = m;
                tags = t;
                profilingSampler = new ProfilingSampler("Outline Mask");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                Recorded = false;
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var lightData = frameData.Get<UniversalLightData>();

                var desc = cameraData.cameraTargetDescriptor;
                var td = new TextureDesc(desc.width, desc.height)
                {
                    format = GraphicsFormat.R8_UNorm,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    name = "_OutlineMask",
                };
                Mask = renderGraph.CreateTexture(td);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Outline Mask", out var data, profilingSampler))
                {
                    var draw = RenderingUtils.CreateDrawingSettings(tags, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                    draw.overrideMaterial = material;
                    draw.overrideMaterialPassIndex = 0;
                    var filter = new FilteringSettings(RenderQueueRange.all, -1, OutlineTarget.RenderingLayerBit);
                    data.list = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, draw, filter));

                    builder.UseRendererList(data.list);
                    builder.SetRenderAttachment(Mask, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(d.list);
                    });
                }
                Recorded = true;
            }
        }

        // ---------------- 2) 번짐 + 합성 ----------------
        class CompositePass : ScriptableRenderPass
        {
            class BlurData
            {
                public Material material;
                public TextureHandle source;
                public MaterialPropertyBlock block;
            }

            class ComposeData
            {
                public Material material;
                public TextureHandle blurred;
                public TextureHandle mask;
                public MaterialPropertyBlock block;
            }

            static readonly int OutlineParams = Shader.PropertyToID("_OutlineParams");
            static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
            static readonly int OutlineDebug = Shader.PropertyToID("_OutlineDebug");
            static readonly int OutlineMaskTex = Shader.PropertyToID("_OutlineMaskTex");
            static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
            static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");

            OutlineRendererFeature feature;
            Material material;
            MaskPass mask;
            readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

            public void Setup(OutlineRendererFeature f, Material m, MaskPass maskPass)
            {
                feature = f;
                material = m;
                mask = maskPass;
                profilingSampler = new ProfilingSampler("Outline Composite");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (!mask.Recorded) return;
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                // 화면 높이에 맞춰 두께를 키운다 (1080p 기준)
                float scale = cameraData.cameraTargetDescriptor.height / 1080f;
                material.SetVector(OutlineParams, new Vector4(feature.widthPixels * scale, feature.bandLow, feature.bandHigh, feature.haloStrength));
                material.SetColor(OutlineColor, feature.color);
                material.SetFloat(OutlineDebug, feature.debugView ? 1f : 0f);

                var desc = cameraData.cameraTargetDescriptor;
                var td = new TextureDesc(desc.width, desc.height)
                {
                    format = GraphicsFormat.R8_UNorm,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    clearBuffer = false,
                    name = "_OutlineDilateH",
                };
                TextureHandle dilated = renderGraph.CreateTexture(td);
                TextureHandle maskHandle = mask.Mask;

                using (var builder = renderGraph.AddRasterRenderPass<BlurData>("Outline Dilate H", out var d, profilingSampler))
                {
                    d.material = material;
                    d.source = maskHandle;
                    d.block = block;
                    builder.UseTexture(maskHandle, AccessFlags.Read);
                    builder.SetRenderAttachment(dilated, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (BlurData data, RasterGraphContext ctx) =>
                    {
                        data.block.Clear();
                        data.block.SetTexture(BlitTexture, data.source);
                        data.block.SetVector(BlitScaleBias, new Vector4(1, 1, 0, 0));
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, data.block);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<ComposeData>("Outline Compose", out var d, profilingSampler))
                {
                    d.material = material;
                    d.blurred = dilated;
                    d.mask = maskHandle;
                    d.block = block;
                    builder.UseTexture(dilated, AccessFlags.Read);
                    builder.UseTexture(maskHandle, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (ComposeData data, RasterGraphContext ctx) =>
                    {
                        data.block.Clear();
                        data.block.SetTexture(BlitTexture, data.blurred);
                        data.block.SetTexture(OutlineMaskTex, data.mask);
                        data.block.SetVector(BlitScaleBias, new Vector4(1, 1, 0, 0));
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, 1, MeshTopology.Triangles, 3, 1, data.block);
                    });
                }
            }
        }
    }
}
