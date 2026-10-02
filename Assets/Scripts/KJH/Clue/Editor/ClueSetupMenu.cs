using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 씬에서 오브젝트를 선택하고 메뉴 한 번만 누르면 "말린 양피지 살펴보기 단서"로 설정해 주는 에디터 도구.
// 새 양피지/편지/지도 모델을 가져올 때마다 손으로 하던 설정(Read/Write, 컴포넌트 추가, 축, 콜라이더,
// 단서 데이터, 참조 연결, 살펴보기 각도)을 자동으로 처리한다.
// Editor 폴더 안에 있으므로 게임 빌드에는 포함되지 않는다.
//
// 사용법: Hierarchy에서 단서로 쓸 오브젝트(모델의 최상위)를 선택 → 메뉴 Tools/KJH/Clue/Make Inspectable Parchment.
// 여러 개를 한꺼번에 선택해도 된다. 이미 설정된 부분은 건드리지 않고 빠진 것만 채운다(다시 눌러도 안전).
public static class ClueSetupMenu
{
    private const string MenuPath = "Tools/KJH/Clue/Make Inspectable Parchment";
    private const string ClueDataFolder = "Assets/Scripts/KJH/Clue/ClueItems";
    private const string InspectProfilePath = "Assets/Scripts/KJH/Clue/Inspect_VolumeProfile.asset";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string InspectVolumeName = "Inspect_Volume";

    // 메뉴를 누를 수 있는 조건: 플레이 중이 아니고, 씬 오브젝트 중 메시가 있는 것이 하나 이상 선택되어 있을 때.
    [MenuItem(MenuPath, true)]
    private static bool Validate()
    {
        return !EditorApplication.isPlaying && GetTargets().Any();
    }

    [MenuItem(MenuPath, false, 100)]
    private static void Run()
    {
        foreach (GameObject root in GetTargets().ToList())
        {
            Setup(root);
        }
    }

    // 선택한 것 중 "씬에 있는" 오브젝트이면서 메시가 있는 것만 고른다 (Project 창의 프리팹 에셋 등은 제외).
    private static IEnumerable<GameObject> GetTargets()
    {
        return Selection.gameObjects.Where(go => !EditorUtility.IsPersistent(go) && go.GetComponentInChildren<MeshFilter>(true) != null);
    }

    private static void Setup(GameObject root)
    {
        var log = new List<string>();
        Undo.SetCurrentGroupName($"Make Inspectable Parchment: {root.name}");
        int undoGroup = Undo.GetCurrentGroup();

        // 1) 말릴 메시 찾기. 자식이 여러 개면 정점이 가장 많은 것을 양피지 본체로 본다.
        MeshFilter meshFilter = FindMainMeshFilter(root, log);
        ParchmentRoller roller = meshFilter.GetComponent<ParchmentRoller>();
        Mesh sourceMesh = roller != null && roller.SourceMesh != null ? roller.SourceMesh : meshFilter.sharedMesh;

        // 2) FBX의 Read/Write 켜기 (정점을 코드로 읽어야 말 수 있다). 원본 메시 데이터는 바뀌지 않고 임포트 설정(.meta)만 바뀐다.
        if (!EnsureReadable(sourceMesh, log))
        {
            Debug.LogError($"[ClueSetup] '{root.name}': 메시를 읽을 수 없어 설정을 중단했습니다.\n- " + string.Join("\n- ", log), root);
            return;
        }

        // 3) ParchmentRoller 추가. 붙이는 순간 Reset()이 호출되어 축이 자동 판별된다.
        if (roller == null)
        {
            roller = Undo.AddComponent<ParchmentRoller>(meshFilter.gameObject);
            log.Add($"ParchmentRoller 추가 ({meshFilter.name}) — 축 자동 판별: 길이 {roller.LengthAxis}, 두께 {roller.ThicknessAxis}");
        }
        else
        {
            log.Add("ParchmentRoller는 이미 있어서 축 설정을 유지했습니다 (다시 판별하려면 컴포넌트 ⋮ 메뉴 → 축 자동 판별)");
        }
        var rollerSo = new SerializedObject(roller);
        rollerSo.FindProperty("rollAmount").floatValue = 1f; // 처음엔 말린 상태로 배치
        rollerSo.ApplyModifiedProperties();

        // 4) 콜라이더가 하나도 없으면 BoxCollider를 붙인다 (조준점 레이캐스트가 맞을 수 있게). 크기는 ParchmentRoller가 말린 모양에 맞춘다.
        if (root.GetComponentInChildren<Collider>(true) == null)
        {
            Undo.AddComponent<BoxCollider>(meshFilter.gameObject);
            log.Add("BoxCollider 추가 (말린 크기에 자동으로 맞춰짐)");
        }
        roller.Rebuild(); // 축/콜라이더가 바뀌었을 수 있으니 말린 메시와 콜라이더 크기를 다시 계산

        // 5) InspectableClue를 최상위에 추가하고 빠진 참조를 채운다.
        InspectableClue clue = root.GetComponent<InspectableClue>();
        bool clueIsNew = clue == null;
        if (clueIsNew)
        {
            clue = Undo.AddComponent<InspectableClue>(root);
            log.Add("InspectableClue 추가");
        }

        var clueSo = new SerializedObject(clue);
        SerializedProperty clueDataProp = clueSo.FindProperty("clueData");
        if (clueDataProp.objectReferenceValue == null)
        {
            clueDataProp.objectReferenceValue = CreateClueData(root.name, log);
        }

        SerializedProperty lookProp = clueSo.FindProperty("lookAction");
        if (lookProp.objectReferenceValue == null)
        {
            lookProp.objectReferenceValue = FindActionReference("Player/Look");
            log.Add(lookProp.objectReferenceValue != null ? "lookAction = Player/Look" : "⚠ Player/Look 액션을 찾지 못했습니다. 직접 연결해 주세요.");
        }

        SerializedProperty volumeProp = clueSo.FindProperty("inspectVolume");
        if (volumeProp.objectReferenceValue == null)
        {
            volumeProp.objectReferenceValue = EnsureInspectVolume(root, log);
        }

        SerializedProperty journalProp = clueSo.FindProperty("clueJournal");
        if (journalProp.objectReferenceValue == null)
        {
            journalProp.objectReferenceValue = EnsureClueJournal(log);
        }

        SerializedProperty toastProp = clueSo.FindProperty("acquiredToast");
        if (toastProp.objectReferenceValue == null)
        {
            toastProp.objectReferenceValue = Object.FindAnyObjectByType<PhotoAcquiredToast>(FindObjectsInactive.Include);
        }

        // 6) 살펴볼 때의 각도: 새로 붙인 경우에만 자동 계산한다 (이미 손으로 맞춰둔 값을 덮어쓰지 않기 위해).
        if (clueIsNew)
        {
            Vector3 offset = ComputeInspectRotationOffset(root.transform, meshFilter.transform, roller);
            clueSo.FindProperty("inspectRotationOffset").vector3Value = offset;
            log.Add($"살펴보기 각도 자동 계산: ({offset.x:0}, {offset.y:0}, {offset.z:0})");
        }
        clueSo.ApplyModifiedProperties();

        // 7) Player의 InteractionController가 Default 레이어만 감지하므로 레이어를 확인한다.
        int lookMask = GetInteractionLookMask();
        if (lookMask != 0 && (lookMask & (1 << meshFilter.gameObject.layer)) == 0)
        {
            log.Add($"⚠ 콜라이더 레이어 '{LayerMask.LayerToName(meshFilter.gameObject.layer)}'가 InteractionController의 감지 레이어에 없습니다. Default로 바꾸거나 마스크에 추가해 주세요.");
        }

        Undo.CollapseUndoOperations(undoGroup); // Ctrl+Z 한 번에 전체가 되돌아가도록 묶는다
        EditorSceneManager.MarkSceneDirty(root.scene);

        Debug.Log($"[ClueSetup] '{root.name}' 설정 완료\n- " + string.Join("\n- ", log) +
                  "\n\n▶ 플레이해서 살펴보기로 꼭 확인하세요: 글자가 위아래로 뒤집히면 InspectableClue의 'Flip Upside Down', " +
                  "뒷면이 보이면 'Flip Face'를 체크하고, 말릴 때 글자 면이 바깥으로 나오면 ParchmentRoller의 'Roll Toward Positive'를 반대로 바꾸세요.", root);
    }

    // 자식 중 정점이 가장 많은 메시를 양피지 본체로 고른다.
    private static MeshFilter FindMainMeshFilter(GameObject root, List<string> log)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
        MeshFilter best = filters.OrderByDescending(f =>
        {
            ParchmentRoller r = f.GetComponent<ParchmentRoller>();
            Mesh m = r != null && r.SourceMesh != null ? r.SourceMesh : f.sharedMesh;
            return m.vertexCount;
        }).First();
        if (filters.Length > 1)
        {
            log.Add($"⚠ 메시가 {filters.Length}개라 정점이 가장 많은 '{best.name}'을 양피지로 골랐습니다. 다르면 그 오브젝트에 직접 ParchmentRoller를 붙여 주세요.");
        }
        return best;
    }

    // 메시를 코드로 읽을 수 있게 FBX 임포트 설정의 Read/Write를 켠다.
    private static bool EnsureReadable(Mesh mesh, List<string> log)
    {
        if (mesh.isReadable) return true;

        string path = AssetDatabase.GetAssetPath(mesh);
        if (AssetImporter.GetAtPath(path) is ModelImporter importer)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            log.Add($"Read/Write 켬: {path}");
            return true;
        }

        log.Add($"⚠ '{mesh.name}'은 FBX 같은 모델 파일이 아니라서 Read/Write를 자동으로 켤 수 없습니다 ({path}).");
        return false;
    }

    // 단서 데이터 에셋을 새로 만든다. clueId는 오브젝트 이름 기반으로, 기존 단서와 겹치지 않게 번호를 붙인다.
    private static ClueData CreateClueData(string objectName, List<string> log)
    {
        if (!AssetDatabase.IsValidFolder(ClueDataFolder))
        {
            AssetDatabase.CreateFolder("Assets/Scripts/KJH/Clue", "ClueItems");
        }

        string baseId = new string(objectName.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
        if (string.IsNullOrEmpty(baseId)) baseId = "clue";

        var usedIds = new HashSet<string>(AssetDatabase.FindAssets("t:ClueData")
            .Select(g => AssetDatabase.LoadAssetAtPath<ClueData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null)
            .Select(d => d.clueId));

        string id = baseId;
        for (int n = 2; usedIds.Contains(id); n++) id = $"{baseId}_{n:00}";

        ClueData data = ScriptableObject.CreateInstance<ClueData>();
        data.clueId = id;
        data.displayName = objectName;
        data.description = "(임시) 내용 미정";

        string path = AssetDatabase.GenerateUniqueAssetPath($"{ClueDataFolder}/Clue_{objectName}.asset");
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();
        log.Add($"단서 데이터 생성: {path} (clueId = {id}, 이름은 Inspector에서 바꿔 주세요)");
        return data;
    }

    // 프로젝트의 InputSystem_Actions에서 "Player/Look" 같은 액션 참조를 찾는다.
    private static InputActionReference FindActionReference(string actionPath)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(InputActionsPath))
        {
            if (o is InputActionReference r && r.name == actionPath) return r;
        }
        return null;
    }

    // 씬에 Inspect_Volume이 없으면 기존 프로필로 하나 만든다 (살펴볼 때 배경이 흐려지는 연출).
    private static Volume EnsureInspectVolume(GameObject root, List<string> log)
    {
        Volume existing = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
            .FirstOrDefault(v => v.gameObject.scene == root.scene && v.name == InspectVolumeName);
        if (existing != null) return existing;

        var go = new GameObject(InspectVolumeName);
        MoveToSameScene(go, root);
        Undo.RegisterCreatedObjectUndo(go, "Create Inspect_Volume");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1;  // 뷰파인더 Volume(0)과 겹치지 않게
        volume.weight = 0f;   // 평소엔 꺼져 있고, 살펴볼 때만 InspectableClue가 1로 올린다
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(InspectProfilePath);
        log.Add(volume.sharedProfile != null ? "Inspect_Volume 생성" : $"⚠ Inspect_Volume을 만들었지만 프로필({InspectProfilePath})이 없어 연출이 보이지 않습니다.");
        return volume;
    }

    // 새로 만든 오브젝트를 단서와 같은 씬으로 옮긴다 (씬을 여러 개 열어둔 경우 대비).
    private static void MoveToSameScene(GameObject go, GameObject sameSceneAs)
    {
        if (go.scene != sameSceneAs.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, sameSceneAs.scene);
    }

    // 씬에 ClueJournal이 없으면 InteractionController가 있는 오브젝트(Player)에 붙인다.
    private static ClueJournal EnsureClueJournal(List<string> log)
    {
        ClueJournal journal = Object.FindAnyObjectByType<ClueJournal>(FindObjectsInactive.Include);
        if (journal != null) return journal;

        InteractionController controller = Object.FindAnyObjectByType<InteractionController>(FindObjectsInactive.Include);
        if (controller == null)
        {
            log.Add("⚠ 씬에 InteractionController(Player)가 없어 ClueJournal을 붙이지 못했습니다.");
            return null;
        }
        journal = Undo.AddComponent<ClueJournal>(controller.gameObject);
        log.Add($"ClueJournal 추가 ({controller.name})");
        return journal;
    }

    private static int GetInteractionLookMask()
    {
        InteractionController controller = Object.FindAnyObjectByType<InteractionController>(FindObjectsInactive.Include);
        if (controller == null) return 0;
        return new SerializedObject(controller).FindProperty("lookLayerMask").intValue;
    }

    // 살펴볼 때 "글자 면이 카메라를 향하고, 길이 방향이 화면 세로가 되도록" 하는 회전값을 계산한다.
    // 가정: 말릴 때 안쪽으로 감기는 면(rollTowardPositive 쪽)이 글자 면이다. 두루마리는 보통 글자가 안쪽에 있기 때문.
    // 글자가 위아래로 뒤집히는지는 텍스처에 따라 달라 코드로 알 수 없으므로, 그건 Flip Upside Down 체크로 고친다.
    private static Vector3 ComputeInspectRotationOffset(Transform root, Transform meshTransform, ParchmentRoller roller)
    {
        Vector3 faceLocal = AxisVector(roller.ThicknessAxis) * (roller.RollTowardPositive ? 1f : -1f);
        Vector3 lengthLocal = AxisVector(roller.LengthAxis);

        // 메시가 자식이고 회전되어 있을 수 있으므로, 메시 기준 방향을 "회전시킬 루트" 기준 방향으로 바꾼다.
        Vector3 face = root.InverseTransformDirection(meshTransform.TransformDirection(faceLocal)).normalized;
        Vector3 length = root.InverseTransformDirection(meshTransform.TransformDirection(lengthLocal)).normalized;

        // 카메라 기준 목표: 글자 면 → 카메라 쪽(-Z), 길이 방향 → 화면 아래(-Y).
        // (현재 양피지 모델에서 손으로 맞춘 값 (180, 0, 0)과 같은 결과가 나오도록 정한 기준)
        Quaternion offset = Quaternion.LookRotation(Vector3.back, Vector3.down) * Quaternion.Inverse(Quaternion.LookRotation(face, length));
        Vector3 euler = offset.eulerAngles;
        return new Vector3(Mathf.Round(euler.x), Mathf.Round(euler.y), Mathf.Round(euler.z));
    }

    private static Vector3 AxisVector(ParchmentRoller.Axis axis)
    {
        switch (axis)
        {
            case ParchmentRoller.Axis.X: return Vector3.right;
            case ParchmentRoller.Axis.Y: return Vector3.up;
            default: return Vector3.forward;
        }
    }
}
