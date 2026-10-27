using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gim007.EditorTools
{
    // GIM-007 함께 미는 안방 가구 자동 제작 도구. Unity 메뉴 Tools/KJH/GIM-007/Build All.
    // 몇 번을 다시 실행해도 된다: 기능 프리팹을 다시 만들고(같은 경로·GUID에 덮어씀) 씬의 GIM007_KJH 루트를 새로 배치한다.
    // 기존 맵 오브젝트(Ravenwood_LGH: F2_Master 가구, 드레스룸 양문 등)는 전혀 수정하지 않는다. 위치만 참고한다.
    //
    // 모델 교체를 위한 구조 — "기능"과 "겉모습"을 프리팹으로 나눴다 (GIM-004와 같은 방식):
    //  - Visuals/Gim007_FurnitureVisual (서랍장), Gim007_PlateVisual (무게 발판), Gim007_ScratchVisual (바닥 긁힌 자국)
    //    = 겉모습만 있는 프리팹. **없을 때만** 만든다 → 아트 담당이 이 프리팹 안을 실제 모델로 바꾸면 Build All을 다시 돌려도 지워지지 않는다.
    //    (임시 모델로 되돌리려면 Tools/KJH/GIM-007/Rebuild Placeholder Visuals)
    //    · 서랍장 모델: 피벗 = 바닥 가운데, 앞(+Z) = 미는 방향(발판 쪽), 뒷면(-Z) = 사람이 잡는 쪽
    //    · 발판 모델: 피벗 = 바닥 가운데, 내려앉을 부분의 이름을 "PlateTop"으로 (없으면 내려앉는 연출만 빠짐)
    //  - GIM007_MasterBedroom.prefab = 기능(스크립트·콜라이더·손잡이) + 위 겉모습 프리팹을 자식 "Visual"로 품는다.
    //    가구 콜라이더·손잡이 위치는 Build All 때 서랍장 겉모습 크기에 맞춰 다시 잡는다 → 모델을 바꾼 뒤 Build All 한 번이면 끝.
    //
    // 만드는 것 (모두 Assets/Prefabs/KJH/Gim007):
    //  - Data/Gim007Config.asset (없을 때만 → Inspector 수정값 보존)
    //  - GIM007_MasterBedroom.prefab  2층 안방 일체 (컨트롤러, 미는 플레이어, 서랍장 + 손잡이 2개 + 경로, 무게 발판, 긁힌 자국)
    //  - Gim007Sync.prefab            네트워크 동기화 (DefaultNetworkPrefabs에 등록)
    // 씬: GIM007_KJH(루트) 아래 GIM007_MasterBedroom 인스턴스 + 최상위 Gim007Sync_KJH
    public static class Gim007Builder
    {
        const string Root = "Assets/Prefabs/KJH/Gim007";
        const string DataDir = Root + "/Data";
        const string VisualDir = Root + "/Visuals";
        const string MatDir = Root + "/Art/Materials";

        // 배치 위치 (KJH 씬 Ravenwood_LGH 맵 기준으로 측정, Develop의 Ravenwood_LGH도 같은 좌표)
        // 2층 안방 F2_Master: x 20~36, z 3~16, 바닥 윗면 y=3.9. 북쪽 벽 z=3에 복도 양문(Door_Master), 동쪽 벽 x=36에 드레스룸 양문
        //  (Door_Master_Dressing, z 12.2~13.7 = 비밀문). 서쪽에 침대(x~24.4), 동쪽에 소파·벽난로(z 7.5~9.9), 남쪽에 러그(납작).
        //  → 서랍장은 안방 가운데(x 29)에서 동쪽(+X)으로 4m 밀어 드레스룸 문 앞 발판(x 33)에 올린다. 경로 z=12.9(문 가운데 높이)에는 장애물이 없다.
        public static readonly Vector3 Origin = new Vector3(31f, 3.9f, 12.9f);
        const float Yaw = 90f;                    // 루트 로컬 +Z(미는 방향) = 월드 +X(동쪽, 드레스룸 문)
        const float PathHalf = 2f;                // 경로 길이 4m = 8칸 × 0.5m (명세서 제안)

        // 임시 서랍장 크기 (로컬: X 폭, Y 높이, Z 깊이)
        const float ChestW = 1.7f, ChestH = 1.05f, ChestD = 0.55f;
        // 손잡이: 미는 사람 기준 왼쪽(-X)이 0번. 서는 곳은 뒷면에서 0.68m 뒤 (손잡이를 40° 정도 내려다보게)
        const float HandleX = 0.45f, StandBack = 0.68f;

        [MenuItem("Tools/KJH/GIM-007/Build All")]
        public static void BuildAll() { Build(false); }

        [MenuItem("Tools/KJH/GIM-007/Rebuild Placeholder Visuals (임시 모델로 덮어쓰기)")]
        public static void RebuildVisuals()
        {
            if (!EditorUtility.DisplayDialog("GIM-007", "Visuals 폴더의 겉모습 프리팹을 임시 모델로 다시 만듭니다.\n실제 모델로 교체해 둔 내용이 있다면 사라집니다. 계속할까요?", "다시 만들기", "취소")) return;
            Build(true);
        }

        static void Build(bool forceVisuals)
        {
            mats.Clear();
            EnsureFolder(Root); EnsureFolder(DataDir); EnsureFolder(VisualDir); EnsureFolder(MatDir);

            var config = BuildData();
            var furnitureVisual = VisualPrefab("Gim007_FurnitureVisual", forceVisuals, BuildChestVisual);
            var plateVisual = VisualPrefab("Gim007_PlateVisual", forceVisuals, BuildPlateVisual);
            var scratchVisual = VisualPrefab("Gim007_ScratchVisual", forceVisuals, BuildScratchVisual);
            var syncPrefab = BuildSyncPrefab();

            foreach (var go in SceneRoots().Where(g => g.name == "GIM007_KJH" || g.name == "Gim007Sync_KJH").ToList()) Object.DestroyImmediate(go);

            var root = new GameObject("GIM007_KJH").transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, ActiveScene());

            var room = BuildRoom(config, furnitureVisual, plateVisual, scratchVisual);
            room.transform.SetParent(root, true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(room, Root + "/GIM007_MasterBedroom.prefab", InteractionMode.AutomatedAction);

            var sync = (GameObject)PrefabUtility.InstantiatePrefab(syncPrefab, ActiveScene());
            sync.name = "Gim007Sync_KJH";
            RegisterNetworkPrefab(syncPrefab);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(ActiveScene());
            Selection.activeObject = root.gameObject;
            Debug.Log("[GIM-007] Build All 완료. 씬을 저장(Ctrl+S)하세요.");
        }

        // =====================================================================
        // 데이터 / 네트워크
        // =====================================================================

        static Gim007Config BuildData()
        {
            string path = DataDir + "/Gim007Config.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<Gim007Config>(path);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<Gim007Config>();
                AssetDatabase.CreateAsset(cfg, path);
            }
            AssetDatabase.SaveAssets();
            return cfg;
        }

        static GameObject BuildSyncPrefab()
        {
            string path = Root + "/Gim007Sync.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;   // 이미 있으면 그대로 (GlobalObjectIdHash가 바뀌지 않게)
            var go = new GameObject("Gim007Sync");
            go.AddComponent<NetworkObject>();
            go.AddComponent<Gim007NetSync>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void RegisterNetworkPrefab(GameObject prefab)
        {
            var nm = Object.FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (nm == null || nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Count == 0)
            {
                Debug.LogWarning("[GIM-007] NetworkManager의 네트워크 프리팹 목록을 찾지 못했습니다. Gim007Sync.prefab을 직접 등록해 주세요.");
                return;
            }
            var list = nm.NetworkConfig.Prefabs.NetworkPrefabsLists[0];
            if (list.PrefabList.Any(p => p.Prefab == prefab)) return;
            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log("[GIM-007] Gim007Sync를 " + AssetDatabase.GetAssetPath(list) + "에 등록했습니다.");
        }

        // =====================================================================
        // 기능 프리팹: GIM007_MasterBedroom
        // =====================================================================
        //  GIM007_MasterBedroom (Gim007Controller, Gim007PushPlayer)   ← 월드 Origin, 로컬 +Z = 미는 방향
        //   ├ Furniture (Gim007Furniture)
        //   │  ├ PathStart / PathEnd
        //   │  └ Body (BoxCollider, 운동학 Rigidbody)
        //   │     ├ Visual (Gim007_FurnitureVisual)
        //   │     └ Handle_0 / Handle_1 (Gim007Handle, BoxCollider) ─ StandPoint, LookPoint
        //   ├ WeightPlate (Gim007WeightPlate) └ Visual (Gim007_PlateVisual)
        //   └ FloorScratches └ Visual (Gim007_ScratchVisual)

        static GameObject BuildRoom(Gim007Config config, GameObject furnitureVisual, GameObject plateVisual, GameObject scratchVisual)
        {
            var go = new GameObject("GIM007_MasterBedroom");
            go.transform.position = Origin;
            go.transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
            var t = go.transform;

            var controller = go.AddComponent<Gim007Controller>();
            SetRef(controller, "config", config);
            go.AddComponent<Gim007PushPlayer>();

            // ---- 가구 + 경로 ----
            var furnitureGo = Child(t, "Furniture", Vector3.zero);
            var start = Child(furnitureGo.transform, "PathStart", new Vector3(0f, 0f, -PathHalf));
            var end = Child(furnitureGo.transform, "PathEnd", new Vector3(0f, 0f, PathHalf));
            var body = Child(furnitureGo.transform, "Body", start.transform.localPosition);
            var visual = AddVisual(body.transform, furnitureVisual);

            // 콜라이더·손잡이를 겉모습 크기에 맞춘다 (모델을 바꾼 뒤 Build All을 다시 돌리면 다시 맞춰진다)
            var b = LocalRendererBounds(body.transform, visual, new Bounds(new Vector3(0f, ChestH * 0.5f, 0f), new Vector3(ChestW, ChestH, ChestD)));
            var col = body.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;
            var rb = body.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            float back = b.min.z;
            float hx = Mathf.Min(HandleX, b.extents.x * 0.55f);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -hx : hx;
                var handle = Child(body.transform, "Handle_" + i, new Vector3(x, b.size.y * 0.55f, back - 0.03f));
                var hc = handle.AddComponent<BoxCollider>();
                // 뒷면 절반을 덮고 위로 조금 더 높게: 잡는 동안 손잡이를 내려다보는 시선이 확실히 맞도록
                hc.size = new Vector3(hx * 2f * 0.95f, b.size.y * 1.1f + 0.2f, 0.06f);
                hc.center = new Vector3(0f, 0.1f, 0f);
                var stand = Child(handle.transform, "StandPoint", Vector3.zero);
                stand.transform.localPosition = new Vector3(0f, -handle.transform.localPosition.y, -StandBack);
                var look = Child(handle.transform, "LookPoint", Vector3.zero);
                look.transform.localPosition = new Vector3(0f, b.max.y - 0.1f - handle.transform.localPosition.y, 0.03f);
                var h = handle.AddComponent<Gim007Handle>();
                SetInt(h, "index", i);
                SetRef(h, "standPoint", stand.transform);
                SetRef(h, "lookPoint", look.transform);
            }

            var furniture = furnitureGo.AddComponent<Gim007Furniture>();
            SetRef(furniture, "body", body.transform);
            SetRef(furniture, "pathStart", start.transform);
            SetRef(furniture, "pathEnd", end.transform);

            // ---- 무게 발판 (경로 끝, 비밀문 앞) ----
            var plate = Child(t, "WeightPlate", new Vector3(0f, 0f, PathHalf));
            AddVisual(plate.transform, plateVisual);
            var wp = plate.AddComponent<Gim007WeightPlate>();
            var so = new SerializedObject(wp);
            so.FindProperty("areaSize").vector3Value = new Vector3(PlateW, 1f, PlateD);
<<<<<<< HEAD
            so.FindProperty("pressDepth").floatValue = 0.012f;   // 발판 모델(Prop_WeightPlate) 높이 0.025m에 맞춘 깊이
=======
>>>>>>> 59f30c0e01c910926ccee6b1a0e22ef4a1b30780
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---- 바닥 긁힌 자국 (가구 → 발판 방향) ----
            var scratches = Child(t, "FloorScratches", Vector3.zero);
            AddVisual(scratches.transform, scratchVisual);
            return go;
        }

        static GameObject Child(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }

        static GameObject AddVisual(Transform parent, GameObject visualPrefab)
        {
            var v = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, parent);
            v.name = "Visual";
            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
            v.transform.localScale = Vector3.one;
            return v;
        }

        static Bounds LocalRendererBounds(Transform root, GameObject visual, Bounds fallback)
        {
            bool first = true;
            var b = fallback;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? wb.min.x : wb.max.x, (i & 2) == 0 ? wb.min.y : wb.max.y, (i & 4) == 0 ? wb.min.z : wb.max.z);
                    var lc = root.InverseTransformPoint(corner);
                    if (first) { b = new Bounds(lc, Vector3.zero); first = false; } else b.Encapsulate(lc);
                }
            }
            return b;
        }

        // =====================================================================
        // 겉모습 프리팹 (없을 때만 만든다) — 임시 모델
        // =====================================================================

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

        // 서랍장: 피벗 = 바닥 가운데, 서랍(앞) = +Z(발판 쪽), 뒷면 = -Z(사람이 잡는 쪽)
        static void BuildChestVisual(Transform t)
        {
            var wood = Mat("ChestWood", new Color(0.36f, 0.22f, 0.13f));
            var dark = Mat("ChestWoodDark", new Color(0.22f, 0.13f, 0.08f));
            var brass = Mat("Brass", new Color(0.62f, 0.5f, 0.26f), 0.6f);
            const float legH = 0.1f;
            float bodyH = ChestH - legH - 0.04f;
            Box(t, "Carcass", new Vector3(0f, legH + bodyH * 0.5f, 0f), new Vector3(ChestW, bodyH, ChestD), wood);
            Box(t, "Top", new Vector3(0f, ChestH - 0.02f, 0f), new Vector3(ChestW + 0.06f, 0.04f, ChestD + 0.05f), dark);
            foreach (float x in new[] { -ChestW * 0.5f + 0.06f, ChestW * 0.5f - 0.06f })
                foreach (float z in new[] { -ChestD * 0.5f + 0.06f, ChestD * 0.5f - 0.06f })
                    Box(t, "Leg", new Vector3(x, legH * 0.5f, z), new Vector3(0.08f, legH, 0.08f), dark);

            // 앞면(발판 쪽) 서랍 3단 × 2열 + 손잡이
            const int rows = 3;
            float rowH = (bodyH - 0.06f) / rows;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < 2; c++)
                {
                    float x = (c == 0 ? -1f : 1f) * ChestW * 0.25f;
                    float y = legH + 0.03f + rowH * (r + 0.5f);
                    Box(t, "DrawerFront", new Vector3(x, y, ChestD * 0.5f + 0.01f), new Vector3(ChestW * 0.5f - 0.05f, rowH - 0.04f, 0.02f), dark);
                    Box(t, "DrawerPull", new Vector3(x, y, ChestD * 0.5f + 0.035f), new Vector3(0.14f, 0.025f, 0.025f), brass);
                }

            // 뒷면(사람 쪽): 판자 + 잡을 곳 2개 (닳은 손잡이 레일). 기능 손잡이(Handle_0/1) 위치와 맞춘다
            Box(t, "BackPanel", new Vector3(0f, legH + bodyH * 0.5f, -ChestD * 0.5f - 0.005f), new Vector3(ChestW - 0.04f, bodyH - 0.04f, 0.01f), dark);
            foreach (float x in new[] { -HandleX, HandleX })
            {
                Box(t, "GripRail", new Vector3(x, ChestH - 0.12f, -ChestD * 0.5f - 0.04f), new Vector3(0.32f, 0.035f, 0.035f), brass);
                foreach (float dx in new[] { -0.15f, 0.15f })
                    Box(t, "GripPost", new Vector3(x + dx, ChestH - 0.12f, -ChestD * 0.5f - 0.02f), new Vector3(0.03f, 0.03f, 0.04f), brass);
            }
        }

        // 무게 발판: 피벗 = 바닥 가운데. 윗판(PlateTop)은 바닥과 거의 같은 높이 → 서랍장이 그대로 미끄러져 올라탄다
        const float PlateW = 2.1f, PlateD = 1.1f;
        static void BuildPlateVisual(Transform t)
        {
            var iron = Mat("PlateIron", new Color(0.16f, 0.16f, 0.17f), 0.4f);
            var steel = Mat("PlateSteel", new Color(0.42f, 0.4f, 0.36f), 0.55f);
            const float frame = 0.07f;
            Box(t, "Frame_L", new Vector3(-PlateW * 0.5f + frame * 0.5f, 0.007f, 0f), new Vector3(frame, 0.014f, PlateD), iron);
            Box(t, "Frame_R", new Vector3(PlateW * 0.5f - frame * 0.5f, 0.007f, 0f), new Vector3(frame, 0.014f, PlateD), iron);
            Box(t, "Frame_F", new Vector3(0f, 0.007f, PlateD * 0.5f - frame * 0.5f), new Vector3(PlateW, 0.014f, frame), iron);
            Box(t, "Frame_B", new Vector3(0f, 0.007f, -PlateD * 0.5f + frame * 0.5f), new Vector3(PlateW, 0.014f, frame), iron);
            var top = new GameObject("PlateTop").transform;
            top.SetParent(t, false);
            top.localPosition = new Vector3(0f, 0.004f, 0f);
            Box(top, "Plate", new Vector3(0f, 0f, 0f), new Vector3(PlateW - frame * 2f - 0.01f, 0.012f, PlateD - frame * 2f - 0.01f), steel);
            // 미끄럼 방지 줄무늬 (금속 발판처럼 보이게)
            for (int i = -6; i <= 6; i++)
                Box(top, "Ridge", new Vector3(i * 0.14f, 0.007f, 0f), new Vector3(0.025f, 0.004f, PlateD - frame * 2f - 0.1f), iron);
        }

        // 바닥 긁힌 자국: 서랍장 다리 두 줄이 지나간 자리 (시작 위치 뒤 → 발판 바로 앞)
        static void BuildScratchVisual(Transform t)
        {
            var mark = Mat("Scratch", new Color(0.13f, 0.09f, 0.06f), 0f);
            var rng = new System.Random(7);
            foreach (float x in new[] { -ChestW * 0.5f + 0.06f, ChestW * 0.5f - 0.06f })
            {
                for (int k = 0; k < 3; k++)
                {
                    float ox = x + (k - 1) * 0.025f + (float)(rng.NextDouble() - 0.5) * 0.01f;
                    float z0 = -PathHalf - 0.2f + (float)rng.NextDouble() * 0.3f;
                    float z1 = PathHalf - PlateD * 0.5f - 0.05f - (float)rng.NextDouble() * 0.4f;
                    var s = Box(t, "Scratch", new Vector3(ox, 0.002f, (z0 + z1) * 0.5f), new Vector3(0.012f, 0.002f, z1 - z0), mark);
                    s.transform.localRotation = Quaternion.Euler(0f, (float)(rng.NextDouble() - 0.5) * 0.6f, 0f);
                }
            }
        }

        // =====================================================================
        // 도우미
        // =====================================================================

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        static Material Mat(string name, Color color, float smooth = 0.15f)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            string path = MatDir + "/Gim007_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 콜라이더는 기능 프리팹이 따로 가진다
            return go;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static UnityEngine.SceneManagement.Scene ActiveScene() { return UnityEngine.SceneManagement.SceneManager.GetActiveScene(); }
        static IEnumerable<GameObject> SceneRoots() { return ActiveScene().GetRootGameObjects(); }

        static void SetRef(Object o, string field, Object value) { var s = new SerializedObject(o); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetInt(Object o, string field, int value) { var s = new SerializedObject(o); s.FindProperty(field).intValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
