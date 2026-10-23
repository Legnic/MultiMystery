using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// LHJ 장면의 두 캐릭터 메시에 입 닫기 모양(블렌드 셰이프 "MouthClose")을 만든다.
// 입은 텍스처에 그려져 있어서, 입 주변 점들을 위아래로 모아 그려진 입을 얇은 선으로 누른다.
// 원본 FBX는 건드리지 않고, 모양이 들어간 메시를 따로 저장해서 장면의 캐릭터에 끼운다.
public static class MouthShapeMaker
{
    const string ShapeName = "MouthClose";
    static readonly string[] Characters = { "Char_Blue", "Char_Orange" };

    [MenuItem("LHJ/Make Mouth Close Shape")]
    static void MakeAll()
    {
        var bodies = new List<SkinnedMeshRenderer>();
        foreach (var name in Characters)
        {
            var root = GameObject.Find(name + "_Moving");
            var body = root != null ? root.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault() : null;
            if (body == null) { Debug.LogError($"[MouthShape] {name}_Moving 캐릭터를 찾지 못했어요."); continue; }
            if (Make(name, root.transform, body)) bodies.Add(body);
        }

        var tester = Object.FindAnyObjectByType<MouthFlapTest>();
        if (tester == null) tester = new GameObject("MouthFlapTest").AddComponent<MouthFlapTest>();
        var so = new SerializedObject(tester);
        var list = so.FindProperty("bodies");
        list.arraySize = bodies.Count;
        for (int i = 0; i < bodies.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = bodies[i];
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(tester.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
    }

    static bool Make(string name, Transform root, SkinnedMeshRenderer body)
    {
        // 다시 만들 때도 늘 원본 FBX 메시에서 시작한다.
        var source = body.sharedMesh;
        if (source.GetBlendShapeIndex(ShapeName) >= 0)
        {
            string fbx = $"Assets/VARCO 3D/LHJ/{name}/{name}_Rigged.fbx";
            source = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().FirstOrDefault();
            if (source == null) { Debug.LogError($"[MouthShape] {name}: 원본 메시를 {fbx}에서 찾지 못했어요."); return false; }
        }

        var pixels = ReadTexture(body.sharedMaterial, out int texW, out int texH);
        if (pixels == null) { Debug.LogError($"[MouthShape] {name}: 텍스처를 읽지 못했어요."); return false; }

        var verts = source.vertices;
        var normals = source.normals;
        var uvs = source.uv;

        // 메시 안에서의 위·앞·오른쪽 방향
        var up = body.transform.InverseTransformDirection(root.up).normalized;
        var front = body.transform.InverseTransformDirection(root.forward).normalized;
        var right = Vector3.Cross(up, front).normalized;

        // 1. 입 찾기: 앞을 보는 검은 점 중에서 눈동자(흰자 옆)가 아닌 것
        var dark = new List<int>();
        for (int i = 0; i < verts.Length; i++)
        {
            if (Vector3.Dot(normals[i], front) < 0.3f) continue;
            int px = Mathf.Clamp((int)(uvs[i].x * texW), 0, texW - 1);
            int py = Mathf.Clamp((int)(uvs[i].y * texH), 0, texH - 1);
            if (!IsDark(pixels[py * texW + px])) continue;
            if (NearWhite(pixels, texW, texH, px, py, texW / 80)) continue;
            dark.Add(i);
        }
        if (dark.Count < 3) { Debug.LogError($"[MouthShape] {name}: 입을 찾지 못했어요 (검은 점 {dark.Count}개)."); return false; }

        // 검은 점을 붙어 있는 덩어리로 나누고, 얼굴에서 가장 아래 덩어리를 입으로 고른다(위쪽은 눈동자).
        float height = source.bounds.size.magnitude;
        var clusters = Cluster(dark, verts, height * 0.02f).Where(c => c.Count >= 10).ToList();
        foreach (var c in clusters)
        {
            var cc = c.Aggregate(Vector3.zero, (s, i) => s + verts[i]) / c.Count;
            Debug.Log($"[MouthShape] {name}: 검은 덩어리 점 {c.Count}개, 위치 {cc:F4}");
        }
        var mouth = clusters.OrderBy(c => c.Average(i => Vector3.Dot(verts[i], up))).First();
        var median = mouth.Aggregate(Vector3.zero, (s, i) => s + verts[i]) / mouth.Count;

        var center = Vector3.zero;
        foreach (var i in mouth) center += verts[i];
        center /= mouth.Count;
        float halfW = mouth.Max(i => Mathf.Abs(Vector3.Dot(verts[i] - center, right)));
        float halfH = mouth.Max(i => Mathf.Abs(Vector3.Dot(verts[i] - center, up)));
        halfW = Mathf.Max(halfW, height * 0.01f);
        halfH = Mathf.Max(halfH, height * 0.004f);
        float centerDepth = Vector3.Dot(center, front);

        // 2. 입 닫기 모양: 입 높이 안쪽 점은 가운데 선으로 모으고, 바깥은 조금씩 덜 움직인다.
        var deltas = new Vector3[verts.Length];
        int moved = 0;
        float innerH = halfH * 1.2f, outerH = halfH * 3f, outerW = halfW * 1.4f;
        for (int i = 0; i < verts.Length; i++)
        {
            var d = verts[i] - center;
            float u = Mathf.Abs(Vector3.Dot(d, right));
            float v = Vector3.Dot(d, up);
            float av = Mathf.Abs(v);
            if (u > outerW || av > outerH) continue;
            if (Vector3.Dot(verts[i], front) < centerDepth - Mathf.Max(halfW, halfH)) continue;
            if (Vector3.Dot(normals[i], front) < 0.1f) continue;

            float side = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfW, outerW, u));
            float pull = av <= innerH ? av * 0.92f : innerH * 0.92f * (1f - (av - innerH) / (outerH - innerH));
            if (side * pull <= 0f) continue;
            deltas[i] = -Mathf.Sign(v) * side * pull * up;
            moved++;
        }

        Debug.Log($"[MouthShape] {name}: up {up:F2} front {front:F2} right {right:F2} bounds {source.bounds.size:F4} center {center:F4} median {median:F4}");

        var mesh = Object.Instantiate(source);
        mesh.name = source.name + "_Mouth";
        mesh.AddBlendShapeFrame(ShapeName, 100f, deltas, null, null);

        string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(source)).Replace('\\', '/');
        string path = $"{folder}/{name}_MouthMesh.asset";
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(body, "Assign mouth mesh");
        body.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        EditorUtility.SetDirty(body);

        Debug.Log($"[MouthShape] {name}: 점 {verts.Length}개, 입 점 {mouth.Count}개, 움직이는 점 {moved}개, 입 크기 {halfW * 2:F4} x {halfH * 2:F4} → {path}");
        return true;
    }

    static Color32[] ReadTexture(Material material, out int w, out int h)
    {
        w = h = 0;
        var tex = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : material.mainTexture;
        if (tex == null) return null;
        w = tex.width; h = tex.height;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(tex, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var pixels = copy.GetPixels32();
        Object.DestroyImmediate(copy);
        return pixels;
    }

    static bool IsDark(Color32 c) => c.r < 50 && c.g < 50 && c.b < 50;

    static bool NearWhite(Color32[] pixels, int w, int h, int x, int y, int radius)
    {
        for (int dy = -radius; dy <= radius; dy += 2)
        for (int dx = -radius; dx <= radius; dx += 2)
        {
            int px = x + dx, py = y + dy;
            if (px < 0 || py < 0 || px >= w || py >= h) continue;
            var c = pixels[py * w + px];
            if (c.r > 220 && c.g > 220 && c.b > 220) return true;
        }
        return false;
    }

    // 서로 maxGap 안쪽으로 이어진 점끼리 한 덩어리로 묶는다.
    static List<List<int>> Cluster(List<int> points, Vector3[] verts, float maxGap)
    {
        var result = new List<List<int>>();
        var left = new HashSet<int>(points);
        while (left.Count > 0)
        {
            int start = left.First();
            left.Remove(start);
            var group = new List<int> { start };
            for (int k = 0; k < group.Count; k++)
            {
                var p = verts[group[k]];
                var near = left.Where(i => (verts[i] - p).sqrMagnitude < maxGap * maxGap).ToList();
                foreach (var i in near) { left.Remove(i); group.Add(i); }
            }
            result.Add(group);
        }
        return result;
    }
}
