using System.IO;
using UnityEditor;
using UnityEngine;

namespace CandleFx
{
    // 손 양초 불꽃을 "빛으로 그리는 불꽃"으로 구성한다. 재실행 가능.
    // 1) 불꽃·빛 번짐 텍스처를 코드로 그려 PNG로 저장 (알파 곱한 rgb)
    // 2) 가산 합성 재질 2개 생성 (이미 있으면 텍스처만 다시 연결 → 색·밝기 튜닝 보존)
    // 3) 플레이어 프리팹의 FlameRoot 밑에 FlamePivot(심지 끝) + FlameCore + FlameHalo를 다시 만들고
    //    CandleFlameMotion을 붙인다. 예전 VARCO 불꽃 메시(Flame)는 끄기만 하고 지우지 않는다.
    public static class CandleFlameBuilder
    {
        const string PrefabPath = "Assets/Objects/LGH/NetworkPlayer_LGH.prefab";
        const string Dir = "Assets/Images/KJH/Materials/HeldCandle";
        const string FlameTexPath = Dir + "/T_CandleFlame.png";
        const string HaloTexPath = Dir + "/T_CandleHalo.png";
        const string CoreMatPath = Dir + "/M_CandleFlameCore.mat";
        const string HaloMatPath = Dir + "/M_CandleFlameHalo.mat";

        [MenuItem("Tools/KJH/HeldCandle/Build Flame")]
        public static void BuildAll()
        {
            WriteTexture(FlameTexPath, DrawFlame(64, 128));
            WriteTexture(HaloTexPath, DrawHalo(64));
            var coreMat = EnsureMaterial(CoreMatPath, FlameTexPath, new Color(1f, 1f, 1f));
            var haloMat = EnsureMaterial(HaloMatPath, HaloTexPath, new Color(1f, 0.7f, 0.4f));
            BuildBodyMaps();
            ApplyBodyMaterial();
            BuildPrefab(coreMat, haloMat);
            AssetDatabase.SaveAssets();
            Debug.Log("[CandleFlameBuilder] 손 양초 불꽃 구성 완료");
        }

        // ── 텍스처 ──────────────────────────────────────────────

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // 아래가 둥글고 위로 갈수록 가늘어지는 물방울 모양. 밑동 가운데가 가장 밝고(흰 노랑),
        // 바깥은 주황, 끝은 투명하게 사라진다. 맨 아래에는 실제 촛불처럼 옅은 파란 기운.
        static Texture2D DrawFlame(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var outer = new Color(1f, 0.42f, 0.08f);
            var mid = new Color(1f, 0.72f, 0.28f);
            var coreCol = new Color(1f, 0.97f, 0.85f);
            var blue = new Color(0.25f, 0.4f, 1f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = ((x + 0.5f) / w - 0.5f) * 2f;
                float v = (y + 0.5f) / h;
                float r = 0.9f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(v, 0.55f)), 0.9f);
                float d = Mathf.Abs(u) / Mathf.Max(r, 1e-3f);
                float a = (1f - Smooth(0.5f, 1f, d)) * Smooth(0f, 0.05f, v) * (1f - Smooth(0.8f, 1f, v));
                float core = (1f - Smooth(0f, 0.6f, d)) * (1f - Smooth(0.12f, 0.6f, v));
                var c = Color.Lerp(outer, mid, 1f - Smooth(0.35f, 0.95f, d));
                c = Color.Lerp(c, coreCol, core);
                float b = (1f - Smooth(0f, 0.13f, v)) * (1f - Smooth(0.2f, 0.9f, d));
                c = Color.Lerp(c, blue, b * 0.55f);
                c *= Mathf.Lerp(1f, 0.55f, Smooth(0.45f, 1f, v));
                tex.SetPixel(x, y, new Color(c.r * a, c.g * a, c.b * a, a));
            }
            tex.Apply();
            return tex;
        }

        // 가운데가 밝고 가장자리로 부드럽게 사라지는 둥근 빛 번짐
        static Texture2D DrawHalo(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = ((x + 0.5f) / n - 0.5f) * 2f, dy = ((y + 0.5f) / n - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.4f);
                tex.SetPixel(x, y, new Color(a, a, a, a));
            }
            tex.Apply();
            return tex;
        }

        static void WriteTexture(string path, Texture2D tex)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaIsTransparency = false;   // rgb에 알파가 이미 곱해져 있음
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        static Material EnsureMaterial(string path, string texPath, Color color)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("KJH/CandleFlameAdditive"));
                mat.SetColor("_Color", color);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find("KJH/CandleFlameAdditive");
            mat.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ── 양초 몸체 맵 (밀랍/놋쇠 구분) ─────────────────────────
        // 양초 모델은 밀랍과 놋쇠 받침이 텍스처 하나를 같이 쓴다. 색으로 둘을 나눈다:
        // 밝고(명도 높음) 채도 낮은 베이지 = 밀랍, 나머지 = 놋쇠.
        // - 반사 맵(R=금속성, A=광택): 놋쇠만 금속, 밀랍은 약한 광택
        // - 발광 맵: 밀랍 부분만 원래 색, 놋쇠는 검정 → 불빛이 비쳐 보이는 건 밀랍뿐

        const string BodyBaseTexPath = "Assets/VARCO 3D/KWJ/Textures/base_color.png";
        public const string MetalSmoothTexPath = Dir + "/T_HeldCandle_MetalSmooth.png";
        public const string WaxEmissionTexPath = Dir + "/T_HeldCandle_WaxEmission.png";

        public const float BrassMetallic = 0.8f, BrassSmoothness = 0.55f, WaxSmoothness = 0.4f;

        public static void BuildBodyMaps()
        {
            const int n = 1024;
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(BodyBaseTexPath));
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = src.GetPixelBilinear((x + 0.5f) / n, (y + 0.5f) / n);
            Object.DestroyImmediate(src);

            var wax = new float[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out _, out float s, out float v);
                wax[i] = Smooth(0.5f, 0.62f, v) * (1f - Smooth(0.22f, 0.36f, s));
            }
            for (int k = 0; k < 2; k++) wax = Blur3(wax, n); // 경계를 부드럽게

            var ms = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
            var em = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var msPx = new Color[n * n];
            var emPx = new Color[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                float w = wax[i];
                msPx[i] = new Color(Mathf.Lerp(BrassMetallic, 0f, w), 0f, 0f, Mathf.Lerp(BrassSmoothness, WaxSmoothness, w));
                emPx[i] = new Color(px[i].r * w, px[i].g * w, px[i].b * w, 1f);
            }
            ms.SetPixels(msPx); ms.Apply();
            em.SetPixels(emPx); em.Apply();
            File.WriteAllBytes(MetalSmoothTexPath, ms.EncodeToPNG());
            File.WriteAllBytes(WaxEmissionTexPath, em.EncodeToPNG());
            Object.DestroyImmediate(ms);
            Object.DestroyImmediate(em);
            ImportMap(MetalSmoothTexPath, false);
            ImportMap(WaxEmissionTexPath, true);
        }

        const string BodyMatPath = Dir + "/M_HeldCandle.mat";
        static readonly Color BodyEmission = new Color(0.19f, 0.13f, 0.07f);

        // 양초 몸체 재질에 반사 맵(놋쇠만 금속)과 밀랍 발광 맵을 연결한다.
        // 맵이 있으면 URP Lit은 금속성은 맵의 R, 광택은 맵의 A × _Smoothness를 쓴다.
        static void ApplyBodyMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BodyMatPath);
            if (mat == null) return;
            mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(MetalSmoothTexPath));
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(WaxEmissionTexPath));
            mat.SetColor("_EmissionColor", BodyEmission);
            mat.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
        }

        static float[] Blur3(float[] a, int n)
        {
            var b = new float[a.Length];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float sum = 0f;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    sum += a[Mathf.Clamp(y + dy, 0, n - 1) * n + Mathf.Clamp(x + dx, 0, n - 1)];
                b[y * n + x] = sum / 9f;
            }
            return b;
        }

        static void ImportMap(string path, bool srgb)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = srgb;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }

        // ── 프리팹 ──────────────────────────────────────────────

        static void BuildPrefab(Material coreMat, Material haloMat)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var hc = root.transform.Find("Main Camera/HeldCandle");
                var flameRoot = hc.Find("FlameRoot");
                var oldFlame = flameRoot.Find("Flame");

                // 심지 끝 = 예전 불꽃 메시의 밑동 가운데 (FlameRoot 좌표), 불꽃 높이도 거기서 잰다
                Vector3 pivotLocal = Vector3.zero;
                float meshHeight = 0.034f;
                var mf = oldFlame != null ? oldFlame.GetComponent<MeshFilter>() : null;
                if (mf != null && mf.sharedMesh != null)
                {
                    var b = mf.sharedMesh.bounds;
                    Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        var q = flameRoot.InverseTransformPoint(oldFlame.TransformPoint(c));
                        min = Vector3.Min(min, q); max = Vector3.Max(max, q);
                    }
                    pivotLocal = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
                    meshHeight = max.y - min.y;
                }

                var old = flameRoot.Find("FlamePivot");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                int layer = oldFlame != null ? oldFlame.gameObject.layer : hc.gameObject.layer;
                var pivot = new GameObject("FlamePivot");
                pivot.layer = layer;
                pivot.transform.SetParent(flameRoot, false);
                pivot.transform.localPosition = pivotLocal;

                var core = MakeQuad("FlameCore", pivot.transform, coreMat, layer);
                var halo = MakeQuad("FlameHalo", pivot.transform, haloMat, layer);

                var motion = pivot.AddComponent<CandleFlameMotion>();
                motion.pivot = pivot.transform;
                motion.core = core;
                motion.halo = halo;
                var lightTr = flameRoot.Find("CandleLight");
                motion.referenceLight = lightTr != null ? lightTr.GetComponent<Light>() : null;
                var selfTr = flameRoot.Find("CandleSelfLight");
                motion.selfLight = selfTr != null ? selfTr.GetComponent<Light>() : null;
                var bodyTr = hc.Find("Model");
                motion.bodyRenderer = bodyTr != null ? bodyTr.GetComponent<Renderer>() : null;
                motion.bodyEmission = BodyEmission;
                motion.coreHeight = meshHeight * 1.35f;
                motion.coreWidth = motion.coreHeight * 0.42f;
                motion.haloSize = motion.coreHeight * 2.4f;
                // 프리팹에서도 바로 모양이 보이도록 한 번 배치
                core.localPosition = new Vector3(0f, motion.coreHeight * 0.5f, 0f);
                core.localScale = new Vector3(motion.coreWidth, motion.coreHeight, 1f);
                halo.localPosition = new Vector3(0f, motion.coreHeight * motion.haloHeight01, 0f);
                halo.localScale = new Vector3(motion.haloSize, motion.haloSize, 1f);

                if (oldFlame != null) oldFlame.gameObject.SetActive(false);
                var flicker = hc.GetComponent<LGH.CandleFlicker>();
                if (flicker != null) flicker.flame = null; // 크기 흔들림은 CandleFlameMotion이 담당

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Transform MakeQuad(string name, Transform parent, Material mat, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.layer = layer;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go.transform;
        }
    }
}
