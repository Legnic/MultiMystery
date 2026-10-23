using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gim004.EditorTools
{
    // GIM-004 임시 아트(코드로 그린 텍스처·메시·머티리얼) 생성 도우미. 진짜 모델이 오면 쓰이지 않는다.
    // 화분 구별 포인트(명세서 2장 "확정 외형")가 임시 모델에서도 반드시 보이도록 잎·꽃 모양을 텍스처로 그린다:
    //  둥근 잎 / 뾰족한 잎 / 안쪽 구멍 잎(E, 구멍이 가장자리에 닿지 않음) / 가장자리 갈라짐 잎(F, 안쪽 구멍 없음)
    //  흰 별꽃(가운데 진노랑) / 옅은 노란 별꽃(가운데 진노랑)
    static class Gim004Art
    {
        public const string ArtDir = "Assets/Prefabs/KJH/Gim004/Art";
        const string TexDir = ArtDir + "/Textures";
        const string MeshDir = ArtDir + "/Meshes";
        const string MatDir = ArtDir + "/Materials";

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        public static void Begin()
        {
            mats.Clear();
            meshes.Clear();
            EnsureFolder(TexDir); EnsureFolder(MeshDir); EnsureFolder(MatDir);
        }

        // =====================================================================
        // 텍스처
        // =====================================================================

        public enum LeafShape { Round, Pointed, InnerHoles, EdgeSplits }

        // 잎 폭(반폭, UV 단위). t = 잎 아래(0) ~ 끝(1)
        static float LeafHalfWidth(LeafShape shape, float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            switch (shape)
            {
                case LeafShape.Round: return 0.47f * Mathf.Sqrt(1f - Mathf.Pow(2f * t - 1f, 2f));
                case LeafShape.Pointed: return 0.2f * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.75f) * (1f - 0.4f * t);
                default: return 0.47f * Mathf.Pow(Mathf.Sqrt(1f - Mathf.Pow(2f * t - 1f, 2f)), 0.85f);
            }
        }

        public static Texture2D LeafTexture(string name, LeafShape shape, Color baseColor)
        {
            const int size = 256;
            const float y0 = 0.02f, y1 = 0.98f;
            var px = new Color[size * size];
            for (int py = 0; py < size; py++)
            {
                float v = (py + 0.5f) / size;
                float t = (v - y0) / (y1 - y0);
                float w = LeafHalfWidth(shape, t);
                for (int pxi = 0; pxi < size; pxi++)
                {
                    float x = (pxi + 0.5f) / size - 0.5f;
                    float ax = Mathf.Abs(x);
                    bool inside = w > 0f && ax <= w;
                    // 잎자루: 잎 아래쪽 가운데 얇은 줄기
                    bool petiole = v < y0 + 0.02f && ax < 0.012f;
                    if (inside && shape == LeafShape.InnerHoles && InHole(x, t, w)) inside = false;
                    if (inside && shape == LeafShape.EdgeSplits && InSplit(ax, t, w)) inside = false;
                    if (!inside && !petiole) { px[py * size + pxi] = new Color(baseColor.r, baseColor.g, baseColor.b, 0f); continue; }

                    // 색: 아래가 조금 진하고, 가운데 잎맥과 곁맥은 밝게
                    Color c = baseColor * (0.82f + 0.25f * t);
                    if (ax < 0.01f) c = Color.Lerp(c, new Color(0.75f, 0.85f, 0.5f), 0.55f);
                    else
                    {
                        float vein = Mathf.Repeat((t - ax * 0.9f) * 7f, 1f);
                        if (vein < 0.05f && ax < w * 0.9f) c = Color.Lerp(c, new Color(0.7f, 0.82f, 0.45f), 0.3f);
                    }
                    // 가장자리는 살짝 어둡게 (모양이 잘 보이게)
                    if (w - ax < 0.012f) c *= 0.75f;
                    c.a = 1f;
                    px[py * size + pxi] = c;
                }
            }
            return SaveTexture(name, size, px);
        }

        // E: 잎 안쪽 구멍. 가운데 잎맥 양옆에 4줄, 구멍 바깥쪽 끝이 잎 가장자리에 닿지 않는다 (|cx|+rx = 0.75w < w)
        static bool InHole(float x, float t, float w)
        {
            float[] rows = { 0.3f, 0.46f, 0.62f, 0.77f };
            foreach (float cy in rows)
            {
                float cx = 0.52f * w;
                float rx = 0.2f * w, ry = 0.055f;
                float dx = (Mathf.Abs(x) - cx) / rx, dy = (t - cy) / ry;
                if (dx * dx + dy * dy < 1f) return true;
            }
            return false;
        }

        // F: 가장자리에서 안쪽으로 깊게 갈라진 틈. 잎맥 쪽 30%는 남겨 둔다 → 독립된 구멍은 생기지 않는다
        static bool InSplit(float ax, float t, float w)
        {
            if (ax < 0.3f * w) return false;
            float[] starts = { 0.2f, 0.36f, 0.52f, 0.68f, 0.84f };
            foreach (float s in starts)
            {
                float line = s + 0.5f * ax;                     // 바깥쪽으로 갈수록 위로 비스듬히
                float hw = 0.006f + 0.045f * (ax / Mathf.Max(0.01f, w));
                if (Mathf.Abs(t - line) < hw) return true;
            }
            return false;
        }

        // 별 모양 꽃 (꽃잎 5장, 가운데 진노랑)
        public static Texture2D StarFlowerTexture(string name, Color petal, Color center)
        {
            const int size = 256;
            var px = new Color[size * size];
            for (int py = 0; py < size; py++)
                for (int pxi = 0; pxi < size; pxi++)
                {
                    float x = (pxi + 0.5f) / size - 0.5f, y = (py + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    float a = Mathf.Atan2(y, x) + Mathf.PI * 0.5f;
                    float edge = 0.48f * (0.3f + 0.7f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 2.2f));
                    Color c;
                    if (r < 0.1f) c = center * (0.85f + 0.15f * Mathf.Repeat((x * 37f + y * 23f) * 3f, 1f));   // 암술·수술
                    else if (r < 0.13f) c = Color.Lerp(center, petal, (r - 0.1f) / 0.03f);
                    else if (r < edge)
                    {
                        c = petal * (0.9f + 0.1f * (1f - r / edge));
                        if (edge - r < 0.01f) c *= 0.85f;
                    }
                    else { px[py * size + pxi] = new Color(petal.r, petal.g, petal.b, 0f); continue; }
                    c.a = 1f;
                    px[py * size + pxi] = c;
                }
            return SaveTexture(name, size, px);
        }

        static Texture2D SaveTexture(string name, int size, Color[] px)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            string path = TexDir + "/" + name + ".png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =====================================================================
        // 머티리얼
        // =====================================================================

        public static Material Mat(string name, Color color, float smooth = 0.15f)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            var m = LoadOrCreateMat(name);
            m.SetColor("_BaseColor", color);
            m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        // 잎·꽃 카드용: 텍스처 투명 부분을 잘라낸다 (알파 클립)
        public static Material CutoutMat(string name, Texture2D tex)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            var m = LoadOrCreateMat(name);
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            m.SetColor("_BaseColor", Color.white);
            m.color = Color.white;
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static Material LoadOrCreateMat(string name)
        {
            string path = MatDir + "/Gim004_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        // =====================================================================
        // 메시
        // =====================================================================

        // 회전체 (profile: (반지름, 높이)를 아래→위 순서로). doubleSided면 안쪽 면도 만든다 (종 모양 꽃 안쪽이 보이게)
        public static Mesh Lathe(string name, Vector2[] profile, int seg, bool doubleSided)
        {
            if (meshes.TryGetValue(name, out var cached)) return cached;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int ring = seg + 1;
            for (int i = 0; i < profile.Length; i++)
                for (int j = 0; j <= seg; j++)
                {
                    float a = 2f * Mathf.PI * j / seg;
                    verts.Add(new Vector3(profile[i].x * Mathf.Cos(a), profile[i].y, profile[i].x * Mathf.Sin(a)));
                    uvs.Add(new Vector2((float)j / seg, (float)i / (profile.Length - 1)));
                }
            for (int i = 0; i < profile.Length - 1; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * ring + j, b = a + 1, c = a + ring, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            if (doubleSided) AddBackFaces(verts, uvs, tris);
            return SaveMesh(name, verts, uvs, tris);
        }

        // 잎 카드: 잎자루(0) → 잎 끝(1) 방향이 +Y, 앞면 +Z. 끝으로 갈수록 살짝 휘어진다. 앞뒤 양면.
        public static Mesh LeafCard(string name, float bend)
        {
            return Card(name, 0f, bend);
        }

        // 꽃 카드: 가운데 기준 정사각형, 앞면 +Z. 앞뒤 양면.
        public static Mesh FlatCard(string name)
        {
            return Card(name, -0.5f, 0f);
        }

        static Mesh Card(string name, float yOffset, float bend)
        {
            if (meshes.TryGetValue(name, out var cached)) return cached;
            const int rows = 4;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int r = 0; r <= rows; r++)
            {
                float v = (float)r / rows;
                float z = bend * v * v;
                verts.Add(new Vector3(-0.5f, v + yOffset, z)); uvs.Add(new Vector2(0f, v));
                verts.Add(new Vector3(0.5f, v + yOffset, z)); uvs.Add(new Vector2(1f, v));
            }
            for (int r = 0; r < rows; r++)
            {
                int a = r * 2, b = a + 1, c = a + 2, d = a + 3;
                // 앞면(+Z)에서 볼 때 시계 방향
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
            AddBackFaces(verts, uvs, tris);
            return SaveMesh(name, verts, uvs, tris);
        }

        // 같은 위치에 뒤집힌 면을 하나 더 만든다 (법선이 반대라 뒷면도 제대로 밝게 보인다)
        static void AddBackFaces(List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            int n = verts.Count, tn = tris.Count;
            for (int i = 0; i < n; i++) { verts.Add(verts[i]); uvs.Add(uvs[i]); }
            for (int i = 0; i < tn; i += 3) { tris.Add(tris[i] + n); tris.Add(tris[i + 2] + n); tris.Add(tris[i + 1] + n); }
        }

        static Mesh SaveMesh(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            // 앞면/뒷면 정점을 따로 두었으므로 RecalculateNormals가 면마다 올바른 방향을 낸다
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            string path = MeshDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // 같은 에셋(GUID)에 덮어써서 프리팹 참조가 끊기지 않게
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                mesh = existing;
            }
            else AssetDatabase.CreateAsset(mesh, path);
            meshes[name] = mesh;
            return mesh;
        }

        // =====================================================================
        // 도형 배치
        // =====================================================================

        public static GameObject MeshObj(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos, Quaternion localRot, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material m, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
            if (!keepCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material m, bool keepCollider = false)
        {
            return Prim(PrimitiveType.Cube, parent, name, localPos, size, m, keepCollider);
        }

        // from → to 를 잇는 원기둥 (줄기·가지)
        public static GameObject Segment(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material m)
        {
            Vector3 d = to - from;
            var go = Prim(PrimitiveType.Cylinder, parent, name, (from + to) * 0.5f, new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f), m);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            return go;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
