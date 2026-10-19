using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools/KJH/Dumbwaiter/Build Rig: 씬의 Dumbwaiter_D_Shaft(통짜 큐브)를 기준으로
// "속이 빈 샤프트 + 창 구멍(1·2층) + 케이지 + 카운터웨이트 + 도르래 + 로프 + 호출 버튼"을 만들어 준다.
// 원본 큐브는 지우지 않고 렌더러/콜라이더만 끈다. 이미 만들어진 Dumbwaiter_Rig가 있으면 지우고 다시 만든다.
public static class DumbwaiterBuilder
{
    private const string ShaftName = "Dumbwaiter_D_Shaft";
    private const string RigName = "Dumbwaiter_Rig";
    private const string MaterialFolder = "Assets/Materials/KJH";

    // 샤프트 치수 (원본 큐브: 가로·세로 1.1, 높이 7.5)
    private const float Outer = 1.1f;
    private const float Height = 7.5f;
    private const float Wall = 0.06f;

    // 창 구멍 (샤프트 -X 면). 층 바닥에서 0.9m 위부터 1.2m 높이, 폭 0.7m.
    private static readonly float[] WindowBottoms = { 0.9f, 4.8f };
    private const float WindowHeight = 1.2f;
    private const float WindowWidth = 0.7f;

    [MenuItem("Tools/KJH/Dumbwaiter/Build Rig")]
    public static void Build()
    {
        GameObject shaft = FindShaft();
        if (shaft == null)
        {
            Debug.LogError($"[DumbwaiterBuilder] 열린 씬에서 '{ShaftName}'를 찾지 못했습니다.");
            return;
        }

        Transform parent = shaft.transform.parent;
        Transform old = parent != null ? parent.Find(RigName) : null;
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Images/LGH/Materials/M_WoodTrim.mat");
        Material brass = GetMaterial("Dumbwaiter_Brass", new Color(0.72f, 0.55f, 0.22f), 0.9f, 0.6f);
        Material rope = GetMaterial("Dumbwaiter_Rope", new Color(0.35f, 0.27f, 0.18f), 0f, 0.1f);
        Material cabinWood = GetMaterial("Dumbwaiter_CabinWood", new Color(0.45f, 0.3f, 0.18f), 0f, 0.3f);
        Material cargo = GetMaterial("Dumbwaiter_Cargo", new Color(0.95f, 0.9f, 0.75f), 0f, 0.3f);

        // 샤프트 바닥 중앙에 회전/스케일이 없는 리그 루트를 만든다 (원본은 스케일이 (1.1,7.5,1.1)이라 자식으로 쓰면 비율이 깨짐).
        var rig = new GameObject(RigName);
        Undo.RegisterCreatedObjectUndo(rig, "Build Dumbwaiter Rig");
        rig.transform.SetParent(parent, false);
        rig.transform.position = shaft.transform.position - Vector3.up * (Height * 0.5f);
        rig.transform.rotation = Quaternion.identity;
        rig.layer = shaft.layer;

        float half = Outer * 0.5f;
        float wallCenter = half - Wall * 0.5f;

        // ── 샤프트 벽 ───────────────────────────────
        Transform shell = Child(rig.transform, "Shell");
        Box(shell, "Wall_East", new Vector3(wallCenter, Height * 0.5f, 0f), new Vector3(Wall, Height, Outer), wood);
        Box(shell, "Wall_North", new Vector3(0f, Height * 0.5f, wallCenter), new Vector3(Outer, Height, Wall), wood);
        Box(shell, "Wall_South", new Vector3(0f, Height * 0.5f, -wallCenter), new Vector3(Outer, Height, Wall), wood);

        // -X 면은 창 구멍이 뚫린 면: 구멍 사이 구간은 통판, 구멍 구간은 좌우 기둥만.
        var bands = new List<(float y0, float y1, bool hole)>();
        float cursor = 0f;
        foreach (float bottom in WindowBottoms)
        {
            bands.Add((cursor, bottom, false));
            bands.Add((bottom, bottom + WindowHeight, true));
            cursor = bottom + WindowHeight;
        }
        bands.Add((cursor, Height, false));

        float pillar = (Outer - WindowWidth) * 0.5f;
        float pillarOffset = (WindowWidth + pillar) * 0.5f;
        int index = 0;
        foreach (var band in bands)
        {
            float h = band.y1 - band.y0;
            float cy = (band.y0 + band.y1) * 0.5f;
            if (!band.hole)
            {
                Box(shell, $"Wall_West_{index}", new Vector3(-wallCenter, cy, 0f), new Vector3(Wall, h, Outer), wood);
            }
            else
            {
                Box(shell, $"Wall_West_{index}_L", new Vector3(-wallCenter, cy, -pillarOffset), new Vector3(Wall, h, pillar), wood);
                Box(shell, $"Wall_West_{index}_R", new Vector3(-wallCenter, cy, pillarOffset), new Vector3(Wall, h, pillar), wood);
            }
            index++;
        }

        // ── 움직이는 부품 ───────────────────────────
        const float cabinW = 0.4f, cabinD = 0.6f, cabinH = 0.7f, cabinX = -0.15f;
        const float cwW = 0.16f, cwD = 0.3f, cwH = 0.5f, cwX = 0.15f;
        const float pulleyY = 7.2f, pulleyR = 0.15f;
        const float f0 = 1.0f, f1 = 4.9f;

        // 케이지: 바닥판 + 4면 얇은 판 (윗면과 -X 면은 열려 있다 = 창에서 안이 들여다보인다).
        Transform cabin = Child(rig.transform, "Cabin");
        cabin.localPosition = new Vector3(cabinX, f1, 0f);
        Box(cabin, "Floor", new Vector3(0f, 0.02f, 0f), new Vector3(cabinW, 0.04f, cabinD), cabinWood);
        Box(cabin, "Side_East", new Vector3(cabinW * 0.5f - 0.015f, cabinH * 0.5f, 0f), new Vector3(0.03f, cabinH, cabinD), cabinWood);
        Box(cabin, "Side_North", new Vector3(0f, cabinH * 0.5f, cabinD * 0.5f - 0.015f), new Vector3(cabinW, cabinH, 0.03f), cabinWood);
        Box(cabin, "Side_South", new Vector3(0f, cabinH * 0.5f, -cabinD * 0.5f + 0.015f), new Vector3(cabinW, cabinH, 0.03f), cabinWood);
        Box(cabin, "Lip_West", new Vector3(-cabinW * 0.5f + 0.015f, 0.07f, 0f), new Vector3(0.03f, 0.14f, cabinD), cabinWood);
        // 화물 자리표시: 양초 모양 작은 실린더 (실린 개수만큼 켜진다).
        var cargoVisuals = new GameObject[3];
        for (int i = 0; i < cargoVisuals.Length; i++)
        {
            GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            c.name = $"Cargo_{i}";
            c.transform.SetParent(cabin, false);
            c.transform.localPosition = new Vector3(0f, 0.04f + 0.09f, -0.2f + 0.2f * i);
            c.transform.localScale = new Vector3(0.07f, 0.09f, 0.07f); // 실린더 기본 높이 2 → 약 0.18m
            c.GetComponent<Renderer>().sharedMaterial = cargo;
            Object.DestroyImmediate(c.GetComponent<Collider>()); // 화물은 조준을 가로막지 않게 콜라이더 없음
            c.SetActive(false);
            cargoVisuals[i] = c;
        }

        Transform counterweight = Child(rig.transform, "Counterweight");
        counterweight.localPosition = new Vector3(cwX, f0 /* 케이지가 2층에 있을 때 카운터웨이트는 1층 높이 */, 0f);
        GameObject cwBody = Box(counterweight, "Body", new Vector3(0f, cwH * 0.5f, 0f), new Vector3(cwW, cwH, cwD), brass);
        Object.DestroyImmediate(cwBody.GetComponent<Collider>());

        // 도르래 바퀴: 실린더(기본 축 Y)를 Z축 방향으로 눕힌다. 회전은 Dumbwaiter가 부모 로컬 Z축으로 돌린다.
        Transform wheelPivot = Child(rig.transform, "PulleyWheel");
        wheelPivot.localPosition = new Vector3(0f, pulleyY, 0f);
        GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wheel.name = "Disc";
        wheel.transform.SetParent(wheelPivot, false);
        wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        wheel.transform.localScale = new Vector3(pulleyR * 2f, 0.03f, pulleyR * 2f);
        wheel.GetComponent<Renderer>().sharedMaterial = brass;
        Object.DestroyImmediate(wheel.GetComponent<Collider>());
        // 바퀴가 도는 게 보이도록 바퀴살 하나를 붙인다.
        GameObject spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spoke.name = "Spoke";
        spoke.transform.SetParent(wheelPivot, false);
        spoke.transform.localScale = new Vector3(pulleyR * 2f - 0.02f, 0.02f, 0.045f);
        spoke.GetComponent<Renderer>().sharedMaterial = wood;
        Object.DestroyImmediate(spoke.GetComponent<Collider>());
        // 축
        GameObject axle = Box(rig.transform, "PulleyAxle", new Vector3(0f, pulleyY, 0f), new Vector3(0.03f, 0.03f, 0.5f), brass);
        Object.DestroyImmediate(axle.GetComponent<Collider>());

        // 로프: 높이 2짜리 실린더, 길이는 Dumbwaiter가 매 프레임 늘린다.
        Transform cabinRope = Rope(rig.transform, "Rope_Cabin", cabinX, rope);
        Transform cwRope = Rope(rig.transform, "Rope_Counterweight", cwX, rope);

        // ── 창 콜라이더 + 버튼 ──────────────────────
        var dumbwaiter = rig.AddComponent<Dumbwaiter>();
        for (int f = 0; f < WindowBottoms.Length; f++)
        {
            float cy = WindowBottoms[f] + WindowHeight * 0.5f;
            GameObject window = new GameObject($"Window_F{f + 1}");
            window.transform.SetParent(rig.transform, false);
            window.transform.localPosition = new Vector3(-wallCenter, cy, 0f);
            var col = window.AddComponent<BoxCollider>();
            col.size = new Vector3(Wall, WindowHeight, WindowWidth);
            var w = window.AddComponent<DumbwaiterWindow>();
            SetFields(w, ("dumbwaiter", dumbwaiter), ("floor", f), ("canLoad", f == 1));

            // 버튼은 창 옆 기둥(+Z쪽), 창 가운데 높이보다 조금 아래.
            GameObject button = Box(rig.transform, $"Button_F{f + 1}", new Vector3(-half - 0.01f, cy - 0.15f, pillarOffset), new Vector3(0.04f, 0.14f, 0.14f), brass);
            var b = button.AddComponent<DumbwaiterButton>();
            SetFields(b, ("dumbwaiter", dumbwaiter), ("floor", f));
        }

        SetFields(dumbwaiter,
            ("cabin", cabin), ("counterweight", counterweight), ("pulleyWheel", wheelPivot),
            ("cabinRope", cabinRope), ("counterweightRope", cwRope));
        var so = new SerializedObject(dumbwaiter);
        var visuals = so.FindProperty("cargoVisuals");
        visuals.arraySize = cargoVisuals.Length;
        for (int i = 0; i < cargoVisuals.Length; i++) visuals.GetArrayElementAtIndex(i).objectReferenceValue = cargoVisuals[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        // 원본 통짜 큐브는 가려서 안을 볼 수 있게 한다 (삭제하지 않음).
        var shaftRenderer = shaft.GetComponent<Renderer>();
        if (shaftRenderer != null) { Undo.RecordObject(shaftRenderer, "Hide Shaft"); shaftRenderer.enabled = false; }
        foreach (var c in shaft.GetComponents<Collider>()) { Undo.RecordObject(c, "Hide Shaft"); c.enabled = false; }

        // 초기 위치 반영 (Awake는 플레이 때 도니 에디터에서도 미리 보이게 한 번 호출).
        cabin.localPosition = new Vector3(cabinX, f1, 0f);
        counterweight.localPosition = new Vector3(cwX, f0, 0f);

        // 에디터에서 보이는 초기 로프 길이 (플레이 중에는 Dumbwaiter가 매 프레임 갱신한다). 케이지 윗면 5.6, 카운터웨이트 윗면 1.5.
        PlaceRope(cabinRope, pulleyY, f1 + cabinH);
        PlaceRope(cwRope, pulleyY, f0 + cwH);

        EditorSceneManager.MarkSceneDirty(rig.scene);
        Selection.activeGameObject = rig;
        Debug.Log("[DumbwaiterBuilder] Dumbwaiter_Rig 생성 완료. 씬을 저장하세요.");
    }

    private static GameObject FindShaft()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == ShaftName && t.gameObject.scene.IsValid()) return t.gameObject;
        return null;
    }

    private static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static void PlaceRope(Transform rope, float upper, float lower)
    {
        float length = upper - lower;
        Vector3 p = rope.localPosition; p.y = lower + length * 0.5f; rope.localPosition = p;
        Vector3 sc = rope.localScale; sc.y = length * 0.5f; rope.localScale = sc;
    }

    private static Transform Rope(Transform parent, string name, float x, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 5f, 0f);
        go.transform.localScale = new Vector3(0.012f, 1f, 0.012f);
        go.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go.transform;
    }

    private static Material GetMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "KJH");
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // 이름으로 [SerializeField] private 필드를 채운다 (int/bool/Object 지원).
    private static void SetFields(Object target, params (string name, object value)[] fields)
    {
        var so = new SerializedObject(target);
        foreach (var (name, value) in fields)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null) { Debug.LogError($"[DumbwaiterBuilder] {target.GetType().Name}.{name} 필드를 찾지 못했습니다."); continue; }
            switch (value)
            {
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case Object o: p.objectReferenceValue = o; break;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
