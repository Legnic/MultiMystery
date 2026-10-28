using System.Linq;
using LGH;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AtticTreasure.EditorTools
{
    // 다락 별 상자 속 황금상·현관 열쇠 자동 제작 도구. Unity 메뉴 Tools/KJH/AtticTreasure/Build All.
    // 몇 번을 다시 실행해도 된다: 기능 프리팹을 다시 만들고(같은 경로·GUID에 덮어씀) 씬의 AtticTreasure_KJH를 새로 배치한다.
    // LGH 오브젝트(StarLockBox_LGH, Door_Front_L/R)는 수정하지 않는다: 상자는 위치만 참고하고, 문은 실행 중에 이름으로 찾아 열쇠만 지정한다.
    //
    // 모델 교체를 위한 구조:
    //  - Visuals/AtticTreasure_StatueVisual, AtticTreasure_KeyVisual = 겉모습 프리팹(지금은 큐브). **없을 때만** 만든다
    //    → 모델링이 오면 이 프리팹 안을 바꾸고 Build All을 다시 돌리면 콜라이더가 새 모델 크기에 맞춰진다.
    //    피벗 = 바닥 가운데. 열쇠는 길이 방향이 X.
    //
    // 만드는 것 (모두 Assets/Prefabs/KJH/AtticTreasure):
    //  - Items/Item_GoldStatue.asset, Items/Item_FrontKey.asset (없을 때만) → ItemDatabase_LGH 목록 "끝"에 추가 (기존 번호 유지)
    //  - AtticTreasure_Contents.prefab  상자 속 황금상 + 현관 열쇠 + 내려놓기용 숨은 자리 2개 + 현관문 연결
    //  - AtticTreasureSync.prefab       황금상 상태 동기화 (DefaultNetworkPrefabs에 등록)
    // 씬: AtticTreasure_KJH(상자 위치) + 최상위 AtticTreasureSync_KJH
    public static class AtticTreasureBuilder
    {
        const string Root = "Assets/Prefabs/KJH/AtticTreasure";
        const string ItemDir = Root + "/Items";
        const string VisualDir = Root + "/Visuals";
        const string MatDir = Root + "/Materials";
        const string BoxName = "StarLockBox_LGH";

        // 상자 기준 로컬 배치 (상자 피벗 = 바닥, 안쪽 바닥 높이 y≈0.028, 안쪽 폭 X ±0.25 / 깊이 Z ±0.16, 자물쇠 = -Z 쪽)
        static readonly Vector3 StatueLocal = new Vector3(-0.11f, 0.028f, 0.01f);
        static readonly Vector3 KeyLocal = new Vector3(0.14f, 0.028f, 0.02f);

        [MenuItem("Tools/KJH/AtticTreasure/Build All")]
        public static void BuildAll() { Build(false); }

        [MenuItem("Tools/KJH/AtticTreasure/Rebuild Placeholder Visuals (임시 큐브로 덮어쓰기)")]
        public static void RebuildVisuals()
        {
            if (!EditorUtility.DisplayDialog("AtticTreasure", "Visuals 폴더의 겉모습 프리팹을 임시 큐브로 다시 만듭니다.\n실제 모델로 교체해 둔 내용이 있다면 사라집니다. 계속할까요?", "다시 만들기", "취소")) return;
            Build(true);
        }

        static void Build(bool forceVisuals)
        {
            EnsureFolder(Root); EnsureFolder(ItemDir); EnsureFolder(VisualDir); EnsureFolder(MatDir);

            var box = Object.FindObjectsByType<StarLockBox>(FindObjectsInactive.Include).FirstOrDefault(b => b.name == BoxName);
            if (box == null) { Debug.LogError("[AtticTreasure] 씬에서 " + BoxName + "을(를) 찾지 못했습니다. 상자가 있는 씬(KJH)에서 실행하세요."); return; }

            var statueItem = ItemAsset("Item_GoldStatue", "gold_statue", "황금상", "묵직한 황금 조각상. 다락 별 상자 안에 숨겨져 있었다.");
            var keyItem = ItemAsset("Item_FrontKey", "key_front", "현관 열쇠", "저택 현관문을 여는 큼직한 열쇠.");
            AddToDatabase(statueItem);
            AddToDatabase(keyItem);

            var statueVisual = VisualPrefab("AtticTreasure_StatueVisual", forceVisuals, t =>
                Cube(t, new Vector3(0f, 0.1f, 0f), new Vector3(0.13f, 0.2f, 0.13f), Mat("Gold", new Color(0.83f, 0.66f, 0.2f), 0.75f)));
            var keyVisual = VisualPrefab("AtticTreasure_KeyVisual", forceVisuals, t =>
                Cube(t, new Vector3(0f, 0.01f, 0f), new Vector3(0.14f, 0.02f, 0.045f), Mat("KeyIron", new Color(0.45f, 0.42f, 0.36f), 0.5f)));
            var syncPrefab = BuildSyncPrefab(statueItem, keyItem);

            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                         .Where(g => g.name == "AtticTreasure_KJH" || g.name == "AtticTreasureSync_KJH").ToList())
                Object.DestroyImmediate(go);

            var contents = new GameObject("AtticTreasure_KJH");
            contents.transform.SetPositionAndRotation(box.transform.position, box.transform.rotation);

            // 황금상 (획득 연출 → 왼손)
            var statue = Item(contents.transform, "GoldStatue", StatueLocal, statueVisual);
            var statueC = statue.AddComponent<AtticTreasureStatue>();
            SetPrompt(statueC, "[E] " + statueItem.NameOrFallback + " 집기");
            SetHold(statueC, StatueHoldOffset, StatueHoldEuler);

            // 현관 열쇠 (E → 바로 왼손)
            var key = Item(contents.transform, "FrontKey", KeyLocal, keyVisual);
            key.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            var keyC = key.AddComponent<AtticTreasureKey>();
            SetPrompt(keyC, "[E] " + keyItem.NameOrFallback + " 집기");
            SetHold(keyC, KeyHoldOffset, KeyHoldEuler);

            // 내려놓기용 숨은 자리 (처음엔 숨김). LGH ItemNetworkSync가 있는 씬에서 인벤토리의 물건을 내려놓을 때 다시 꺼내 쓴다
            DropSpot(contents.transform, "GoldStatue_DropSpot", StatueLocal, statueVisual, statueItem);
            DropSpot(contents.transform, "FrontKey_DropSpot", KeyLocal, keyVisual, keyItem);

            // 현관문 연결
            var door = contents.AddComponent<AtticTreasureFrontDoor>();
            var so = new SerializedObject(door);
            so.FindProperty("keyItem").objectReferenceValue = keyItem;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAssetAndConnect(contents, Root + "/AtticTreasure_Contents.prefab", InteractionMode.AutomatedAction);

            var sync = (GameObject)PrefabUtility.InstantiatePrefab(syncPrefab, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            sync.name = "AtticTreasureSync_KJH";
            RegisterNetworkPrefab(syncPrefab);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeObject = contents;
            Debug.Log("[AtticTreasure] Build All 완료. 씬을 저장(Ctrl+S)하세요.");
        }

        static void DropSpot(Transform parent, string name, Vector3 localPos, GameObject visual, ItemData item)
        {
            var go = Item(parent, name, localPos, visual);
            go.AddComponent<WorldItem>().item = item;
            go.AddComponent<AtticTreasureHiddenAtStart>();
        }

        // 왼손에 들었을 때 카메라 기준 위치·회전 (2026-10-28 실제 모델 크기로 화면 보고 맞춘 값)
        public static readonly Vector3 StatueHoldOffset = new Vector3(-0.36f, -0.42f, 0.9f), StatueHoldEuler = new Vector3(0f, 25f, 0f);
        public static readonly Vector3 KeyHoldOffset = new Vector3(-0.24f, -0.22f, 0.62f), KeyHoldEuler = new Vector3(-35f, -35f, 0f);

        static void SetHold(Object c, Vector3 offset, Vector3 euler)
        {
            var so = new SerializedObject(c);
            so.FindProperty("holdOffset").vector3Value = offset;
            so.FindProperty("holdEuler").vector3Value = euler;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetPrompt(Object c, string text)
        {
            var so = new SerializedObject(c);
            so.FindProperty("prompt").stringValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 기능 오브젝트 하나: 루트(콜라이더) + 자식 "Visual"(겉모습 프리팹)
        static GameObject Item(Transform parent, string name, Vector3 localPos, GameObject visualPrefab)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var v = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, go.transform);
            v.name = "Visual";
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
            v.transform.localScale = Vector3.one;

            // 콜라이더를 겉모습 크기에 맞춘다 (모델 교체 후 Build All이면 다시 맞춰짐)
            bool first = true;
            var b = new Bounds(Vector3.up * 0.05f, Vector3.one * 0.1f);
            foreach (var r in v.GetComponentsInChildren<Renderer>(true))
            {
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? wb.min.x : wb.max.x, (i & 2) == 0 ? wb.min.y : wb.max.y, (i & 4) == 0 ? wb.min.z : wb.max.z);
                    var lc = go.transform.InverseTransformPoint(c);
                    if (first) { b = new Bounds(lc, Vector3.zero); first = false; } else b.Encapsulate(lc);
                }
            }
            var col = go.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = Vector3.Max(b.size, Vector3.one * 0.03f);   // 납작한 열쇠도 조준하기 쉽게 최소 두께
            return go;
        }

        static ItemData ItemAsset(string file, string id, string displayName, string description)
        {
            string path = ItemDir + "/" + file + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null) return item;   // 이미 있으면 그대로 (Inspector에서 바꾼 이름·아이콘 보존)
            item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = id;
            item.displayName = displayName;
            item.description = description;
            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        // 아이템은 네트워크에서 목록 번호로 오가므로 "끝에만" 추가한다 (기존 아이템 번호가 바뀌지 않게)
        static void AddToDatabase(ItemData item)
        {
            var db = Resources.Load<ItemDatabase>("ItemDatabase_LGH");
            if (db == null) { Debug.LogError("[AtticTreasure] ItemDatabase_LGH를 찾지 못했습니다. " + item.name + "을(를) 직접 목록 끝에 추가해 주세요."); return; }
            if (db.items.Contains(item)) return;
            db.items.Add(item);
            EditorUtility.SetDirty(db);
            Debug.Log("[AtticTreasure] " + item.name + "을(를) ItemDatabase_LGH 목록 끝(" + (db.items.Count - 1) + "번)에 추가했습니다.");
        }

        static GameObject BuildSyncPrefab(ItemData statueItem, ItemData keyItem)
        {
            string path = Root + "/AtticTreasureSync.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null)   // 이미 있으면 다시 만들지 않는다 (GlobalObjectIdHash가 바뀌지 않게)
            {
                var go = new GameObject("AtticTreasureSync");
                go.AddComponent<NetworkObject>();
                go.AddComponent<AtticTreasureSync>();
                go.AddComponent<AtticTreasurePlaceholderCutscene>();
                existing = PrefabUtility.SaveAsPrefabAsset(go, path);
                Object.DestroyImmediate(go);
            }
            var sync = existing.GetComponent<AtticTreasureSync>();
            var so = new SerializedObject(sync);
            bool changed = false;
            if (so.FindProperty("statueItem").objectReferenceValue == null) { so.FindProperty("statueItem").objectReferenceValue = statueItem; changed = true; }
            if (so.FindProperty("keyItem").objectReferenceValue == null) { so.FindProperty("keyItem").objectReferenceValue = keyItem; changed = true; }
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(existing);
            }
            return existing;
        }

        static void RegisterNetworkPrefab(GameObject prefab)
        {
            var nm = Object.FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (nm == null || nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Count == 0)
            {
                Debug.LogWarning("[AtticTreasure] NetworkManager의 네트워크 프리팹 목록을 찾지 못했습니다. AtticTreasureSync.prefab을 직접 등록해 주세요.");
                return;
            }
            var list = nm.NetworkConfig.Prefabs.NetworkPrefabsLists[0];
            if (list.PrefabList.Any(p => p.Prefab == prefab)) return;
            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log("[AtticTreasure] AtticTreasureSync를 " + AssetDatabase.GetAssetPath(list) + "에 등록했습니다.");
        }

        static GameObject VisualPrefab(string name, bool force, System.Action<Transform> build)
        {
            string path = VisualDir + "/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !force) return existing;
            var go = new GameObject(name);
            build(go.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void Cube(Transform parent, Vector3 localPos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Cube";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 콜라이더는 기능 오브젝트가 가진다
        }

        static Material Mat(string name, Color color, float smooth)
        {
            string path = MatDir + "/AtticTreasure_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
