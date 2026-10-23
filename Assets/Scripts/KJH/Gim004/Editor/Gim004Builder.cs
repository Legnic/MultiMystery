using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Gim004.EditorTools.Gim004Art;

namespace Gim004.EditorTools
{
    // GIM-004 온실 화분 퍼즐 자동 제작 도구. Unity 메뉴 Tools/KJH/GIM-004/Build All.
    // 몇 번을 다시 실행해도 된다: 기능 프리팹을 다시 만들고(같은 경로·GUID에 덮어씀) 씬의 GIM004_KJH 루트를 새로 배치한다.
    // 기존 맵 오브젝트(Ravenwood_LGH: F1_Conservatory, F2_Library/ReadingTable, 문 등)는 전혀 수정하지 않는다. 위치만 참고한다.
    //
    // 모델 교체를 위한 구조 — "기능"과 "겉모습"을 프리팹으로 나눴다:
    //  - Visuals/Gim004_PotVisual_A~G, Gim004_ShelfVisual, Gim004_PotBenchVisual, Gim004_JournalVisual
    //    = 겉모습만 있는 프리팹. **없을 때만** 만든다 → 아트 담당이 이 프리팹 안을 실제 모델로 바꾸면 Build All을 다시 돌려도 지워지지 않는다.
    //    (임시 모델로 되돌리려면 Tools/KJH/GIM-004/Rebuild Placeholder Visuals)
    //  - GIM004_Conservatory.prefab / GIM004_Journal.prefab = 기능(스크립트·콜라이더) + 위 겉모습 프리팹을 자식 "Visual"로 품는다.
    //    화분 콜라이더는 Build All 때 겉모습 크기에 맞춰 다시 잡는다 → 모델을 바꾼 뒤 Build All 한 번이면 끝.
    //  - 기록일지 사진 1~4번은 Build All 마지막에 실제 화분 겉모습 프리팹을 찍어서 만든다 (Data/Photos). 모델이 바뀌면 사진도 같이 바뀐다.
    //
    // 만드는 것 (모두 Assets/Prefabs/KJH/Gim004):
    //  - Data/Gim004Config.asset (없을 때만 → Inspector 수정값 보존), Data/Photos/Gim004_Photo1~4.png
    //  - GIM004_Conservatory.prefab  1층 온실 일체 (컨트롤러, 중앙 선반 + 자리 4개, 화분 7개 + 받침 벤치)
    //  - GIM004_Journal.prefab       2층 서고 ReadingTable 위 온실 기록일지
    //  - Gim004Sync.prefab           네트워크 동기화 (DefaultNetworkPrefabs에 등록)
    // 씬: GIM004_KJH(루트) 아래 두 프리팹 인스턴스 + 최상위 Gim004Sync_KJH
    public static class Gim004Builder
    {
        const string Root = "Assets/Prefabs/KJH/Gim004";
        const string DataDir = Root + "/Data";
        const string PhotoDir = DataDir + "/Photos";
        const string VisualDir = Root + "/Visuals";

        // 배치 위치 (KJH 씬 Ravenwood_LGH 맵 기준으로 측정, Develop의 Ravenwood_LGH도 같은 좌표)
        // 1층 온실 Floor_Conservatory: x 55~68, z -16~-9 (2개 층 높이). 서쪽 벽 x=55에 복도로 통하는 문(Door_Conservatory, z -13.6~-12.1).
        //  가운데 서쪽에 원탁·의자(x 57~59.7), 동쪽 벽에 LGH 화단(x 66.8~). → 중앙 선반은 x 62.5, 정면이 서쪽(입구) 쪽을 보게 한다.
        //  후보 화분 7개는 북쪽 벽(z=-9) 앞 받침 벤치 위 (x 59.5~65.5, LGH 화분·의자와 겹치지 않는 빈 자리).
        public static readonly Vector3 ConservatoryOrigin = new Vector3(61.5f, 0f, -12.5f);
        static readonly Vector3 ShelfLocal = new Vector3(1f, 0f, 0f);
        const float ShelfYaw = 90f;                                       // 선반 정면(-Z)이 월드 서쪽(-X, 입구)을 본다
        static readonly Vector3 BenchLocal = new Vector3(1f, 0f, 2.55f);  // 월드 (62.5, 0, -9.95). 북쪽 내벽 면 z=-9.13, 큰 잎(E·F)이 벽에 박히지 않게 띄움
        // 2층 서고 ReadingTable 상판 윗면: 중심 (62.55, 4.66, 3.5), x 60.4~64.7, z 2.8~4.2
        public static readonly Vector3 JournalPos = new Vector3(62.55f, 4.665f, 3.5f);
        const float JournalYaw = 180f;                                    // 남쪽 의자에서 읽는 방향

        // 받침 벤치 위 화분 순서 (서→동). 비슷한 쌍(A·B, C·D, E·F)이 바로 옆에 붙지 않게 섞었다. 순서는 자유 (명세서 6장)
        static readonly Gim004PotId[] BenchOrder =
            { Gim004PotId.C, Gim004PotId.A, Gim004PotId.F, Gim004PotId.G, Gim004PotId.D, Gim004PotId.E, Gim004PotId.B };

        const float SlotSpacing = 0.7f;
        const float ShelfTopY = 0.78f;

        [MenuItem("Tools/KJH/GIM-004/Build All")]
        public static void BuildAll() { Build(false); }

        [MenuItem("Tools/KJH/GIM-004/Rebuild Placeholder Visuals (임시 모델로 덮어쓰기)")]
        public static void RebuildVisuals()
        {
            if (!EditorUtility.DisplayDialog("GIM-004", "Visuals 폴더의 겉모습 프리팹을 임시 모델로 다시 만듭니다.\n실제 모델로 교체해 둔 내용이 있다면 사라집니다. 계속할까요?", "다시 만들기", "취소")) return;
            Build(true);
        }

        [MenuItem("Tools/KJH/GIM-004/Capture Journal Photos")]
        public static void CapturePhotosMenu()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<Gim004Config>(DataDir + "/Gim004Config.asset");
            if (cfg == null) { Debug.LogError("[GIM-004] Config가 없습니다. 먼저 Build All을 실행하세요."); return; }
            CapturePhotos(cfg);
        }

        static void Build(bool forceVisuals)
        {
            Begin();
            EnsureFolder(Root); EnsureFolder(DataDir); EnsureFolder(PhotoDir); EnsureFolder(VisualDir);

            var config = BuildData();
            var potVisuals = new Dictionary<Gim004PotId, GameObject>();
            foreach (Gim004PotId id in System.Enum.GetValues(typeof(Gim004PotId))) potVisuals[id] = PotVisual(id, forceVisuals);
            var shelfVisual = VisualPrefab("Gim004_ShelfVisual", forceVisuals, BuildShelfVisual);
            var benchVisual = VisualPrefab("Gim004_PotBenchVisual", forceVisuals, BuildBenchVisual);
            var journalVisual = VisualPrefab("Gim004_JournalVisual", forceVisuals, BuildJournalVisual);
            var syncPrefab = BuildSyncPrefab();

            foreach (var go in SceneRoots().Where(g => g.name == "GIM004_KJH" || g.name == "Gim004Sync_KJH").ToList()) Object.DestroyImmediate(go);

            var root = new GameObject("GIM004_KJH").transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root.gameObject, ActiveScene());

            var cons = BuildConservatory(config, potVisuals, shelfVisual, benchVisual);
            cons.transform.SetParent(root, true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(cons, Root + "/GIM004_Conservatory.prefab", InteractionMode.AutomatedAction);

            var journal = BuildJournal(config, journalVisual);
            journal.transform.SetParent(root, true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(journal, Root + "/GIM004_Journal.prefab", InteractionMode.AutomatedAction);

            var sync = (GameObject)PrefabUtility.InstantiatePrefab(syncPrefab, ActiveScene());
            sync.name = "Gim004Sync_KJH";
            RegisterNetworkPrefab(syncPrefab);

            CapturePhotos(config);

            EditorSceneManager.MarkSceneDirty(ActiveScene());
            Selection.activeObject = root.gameObject;
            Debug.Log("[GIM-004] Build All 완료. 씬을 저장(Ctrl+S)하세요.");
        }

        // =====================================================================
        // 데이터
        // =====================================================================

        static Gim004Config BuildData()
        {
            string path = DataDir + "/Gim004Config.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<Gim004Config>(path);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<Gim004Config>();
                AssetDatabase.CreateAsset(cfg, path);
            }
            AssetDatabase.SaveAssets();
            return cfg;
        }

        static GameObject BuildSyncPrefab()
        {
            string path = Root + "/Gim004Sync.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;   // 이미 있으면 그대로 (GlobalObjectIdHash가 바뀌지 않게)
            var go = new GameObject("Gim004Sync");
            go.AddComponent<NetworkObject>();
            go.AddComponent<Gim004NetSync>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void RegisterNetworkPrefab(GameObject prefab)
        {
            var nm = Object.FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (nm == null || nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Count == 0)
            {
                Debug.LogWarning("[GIM-004] NetworkManager의 네트워크 프리팹 목록을 찾지 못했습니다. Gim004Sync.prefab을 직접 등록해 주세요.");
                return;
            }
            var list = nm.NetworkConfig.Prefabs.NetworkPrefabsLists[0];
            if (list.PrefabList.Any(p => p.Prefab == prefab)) return;
            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log("[GIM-004] Gim004Sync를 " + AssetDatabase.GetAssetPath(list) + "에 등록했습니다.");
        }

        // =====================================================================
        // 겉모습 프리팹 (없을 때만 만든다)
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

        static GameObject PotVisual(Gim004PotId id, bool force)
        {
            return VisualPrefab("Gim004_PotVisual_" + id, force, t => BuildPotVisual(t, id));
        }

        // ---------- 화분 ----------
        // 공통: 토분(높이 0.35m) + 흙. 식물은 흙 높이(y 0.32)에서 시작한다.

        const float SoilY = 0.32f;

        static void BuildPotVisual(Transform t, Gim004PotId id)
        {
            var clay = Mat("Terracotta", new Color(0.62f, 0.33f, 0.2f));
            var soil = Mat("Soil", new Color(0.2f, 0.13f, 0.08f));
            var potMesh = Lathe("PotBody", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.14f, 0f), new Vector2(0.145f, 0.01f), new Vector2(0.19f, 0.29f),
                new Vector2(0.215f, 0.3f), new Vector2(0.215f, 0.35f), new Vector2(0.19f, 0.35f), new Vector2(0.185f, SoilY), new Vector2(0f, SoilY),
            }, 24, false);
            MeshObj(t, "Pot", potMesh, clay, Vector3.zero, Quaternion.identity, Vector3.one);
            Prim(PrimitiveType.Cylinder, t, "Soil", new Vector3(0f, SoilY + 0.003f, 0f), new Vector3(0.37f, 0.004f, 0.37f), soil);

            var plant = new GameObject("Plant").transform;
            plant.SetParent(t, false);
            switch (id)
            {
                case Gim004PotId.A: RoundLeaves(plant, 7); BellStalks(plant, 2); break;
                case Gim004PotId.B: PointedLeaves(plant, 9); BellStalks(plant, 3); break;
                case Gim004PotId.C: PointedLeaves(plant, 9); StarStems(plant, 3, PaleYellowStar()); break;
                case Gim004PotId.D: RoundLeaves(plant, 7); StarStems(plant, 3, WhiteStar()); break;
                case Gim004PotId.E: BigLeaves(plant, LeafTexture("Leaf_InnerHoles", LeafShape.InnerHoles, new Color(0.13f, 0.4f, 0.16f)), "LeafInnerHoles"); break;
                case Gim004PotId.F: BigLeaves(plant, LeafTexture("Leaf_EdgeSplits", LeafShape.EdgeSplits, new Color(0.13f, 0.4f, 0.16f)), "LeafEdgeSplits"); break;
                case Gim004PotId.G: Cactus(plant); break;
            }
        }

        static Material Stem() { return Mat("Stem", new Color(0.32f, 0.5f, 0.22f)); }
        static Material WhiteStar() { return CutoutMat("StarWhite", StarFlowerTexture("Flower_StarWhite", new Color(0.97f, 0.97f, 0.95f), new Color(0.95f, 0.62f, 0.02f))); }
        static Material PaleYellowStar() { return CutoutMat("StarPaleYellow", StarFlowerTexture("Flower_StarPaleYellow", new Color(1f, 0.9f, 0.48f), new Color(0.95f, 0.62f, 0.02f))); }

        // 잎 하나: 흙 위 base에서 yaw 방향 바깥쪽으로 tilt도 기울어진 잎 카드
        static void Leaf(Transform parent, Material mat, Mesh mesh, Vector3 basePos, float yaw, float tilt, float width, float length)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            MeshObj(parent, "Leaf", mesh, mat, basePos, rot, new Vector3(width, length, length));
        }

        static Vector3 Out(float yaw) { return Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; }

        // 둥근 잎 (A, D)
        static void RoundLeaves(Transform p, int count)
        {
            var mat = CutoutMat("LeafRound", LeafTexture("Leaf_Round", LeafShape.Round, new Color(0.3f, 0.58f, 0.24f)));
            var mesh = LeafCard("LeafCard", 0.15f);
            for (int i = 0; i < count; i++)
            {
                float yaw = i * 360f / count + 10f;
                float tilt = i % 2 == 0 ? 62f : 48f;
                Leaf(p, mat, mesh, new Vector3(0f, SoilY + 0.01f, 0f) + Out(yaw) * 0.03f, yaw, tilt, 0.17f, 0.18f);
            }
        }

        // 뾰족한 잎 (B, C)
        static void PointedLeaves(Transform p, int count)
        {
            var mat = CutoutMat("LeafPointed", LeafTexture("Leaf_Pointed", LeafShape.Pointed, new Color(0.24f, 0.5f, 0.21f)));
            var mesh = LeafCard("LeafCard", 0.15f);
            for (int i = 0; i < count; i++)
            {
                float yaw = i * 360f / count + 20f;
                float tilt = 22f + (i % 3) * 12f;
                Leaf(p, mat, mesh, new Vector3(0f, SoilY + 0.01f, 0f) + Out(yaw) * 0.02f, yaw, tilt, 0.12f, 0.34f);
            }
        }

        // 흰 종 모양 꽃이 휘어진 꽃대에 매달림 (A: 2송이, B: 3송이). 꽃 개수가 구별 포인트라 한 꽃대에 한 송이씩, 서로 겹치지 않게 벌린다.
        static void BellStalks(Transform p, int count)
        {
            var stem = Stem();
            var bellMat = Mat("BellWhite", new Color(0.97f, 0.97f, 0.95f), 0.3f);
            var bell = Lathe("BellFlower", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.2f, -0.05f), new Vector2(0.42f, -0.2f), new Vector2(0.5f, -0.45f),
                new Vector2(0.58f, -0.75f), new Vector2(0.8f, -0.95f), new Vector2(0.86f, -1f),
            }, 16, true);
            for (int i = 0; i < count; i++)
            {
                float yaw = i * 360f / count + 30f;
                Vector3 o = Out(yaw);
                Vector3 a = new Vector3(0f, SoilY, 0f) + o * 0.02f;
                Vector3 b = a + Vector3.up * (0.4f + 0.04f * i) + o * 0.04f;
                Vector3 c = b + Vector3.up * 0.06f + o * 0.1f;
                Vector3 d = c + Vector3.down * 0.05f + o * 0.06f;
                Segment(p, "Stalk", a, b, 0.006f, stem);
                Segment(p, "Stalk", b, c, 0.005f, stem);
                Segment(p, "Stalk", c, d, 0.004f, stem);
                MeshObj(p, "Bell", bell, bellMat, d, Quaternion.identity, Vector3.one * 0.075f);
            }
        }

        // 별 모양 꽃 (C: 옅은 노랑, D: 흰색 — 둘 다 가운데 진노랑)
        static void StarStems(Transform p, int count, Material flower)
        {
            var stem = Stem();
            var card = FlatCard("FlowerCard");
            for (int i = 0; i < count; i++)
            {
                float yaw = i * 360f / count + 60f;
                Vector3 o = Out(yaw);
                Vector3 a = new Vector3(0f, SoilY, 0f) + o * 0.02f;
                Vector3 tip = a + Vector3.up * (0.3f + 0.05f * i) + o * 0.08f;
                Segment(p, "Stem", a, tip, 0.005f, stem);
                Vector3 face = (Vector3.up * Mathf.Cos(14f * Mathf.Deg2Rad) + o * Mathf.Sin(14f * Mathf.Deg2Rad)).normalized;   // 거의 위를 보게: 위에서 찍은 사진에 3송이가 모두 보이도록
                MeshObj(p, "Star", card, flower, tip + face * 0.004f, Quaternion.LookRotation(face, o), Vector3.one * 0.14f);
            }
        }

        // 큰 잎 3장 (E: 안쪽 구멍, F: 가장자리 갈라짐). 꽃 없음
        static void BigLeaves(Transform p, Texture2D tex, string matName)
        {
            var stem = Stem();
            var mat = CutoutMat(matName, tex);
            var mesh = LeafCard("BigLeafCard", 0.08f);
            for (int i = 0; i < 3; i++)
            {
                float yaw = i * 120f + 15f;
                Vector3 o = Out(yaw);
                Vector3 a = new Vector3(0f, SoilY, 0f);
                Vector3 b = a + Vector3.up * (0.32f + 0.05f * i) + o * 0.08f;
                Segment(p, "Petiole", a, b, 0.009f, stem);
                Leaf(p, mat, mesh, b, yaw, 58f, 0.44f, 0.5f);
            }
        }

        // 기둥 모양 선인장 (G): 굵은 줄기 + 촘촘한 가시
        static void Cactus(Transform p)
        {
            var body = Mat("Cactus", new Color(0.24f, 0.44f, 0.24f));
            var spikeMat = Mat("Spike", new Color(0.93f, 0.9f, 0.75f));
            var spike = Lathe("Spike", new[] { new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f) }, 6, false);
            const float r = 0.1f, h = 0.52f;
            Prim(PrimitiveType.Cylinder, p, "Column", new Vector3(0f, SoilY + h * 0.5f, 0f), new Vector3(r * 2f, h * 0.5f, r * 2f), body);
            Prim(PrimitiveType.Sphere, p, "Top", new Vector3(0f, SoilY + h, 0f), new Vector3(r * 2f, r * 1.6f, r * 2f), body);
            for (int row = 0; row < 8; row++)
                for (int col = 0; col < 10; col++)
                {
                    float yaw = col * 36f + (row % 2) * 18f;
                    Vector3 n = Out(yaw);
                    Vector3 pos = new Vector3(0f, SoilY + 0.05f + row * 0.065f, 0f) + n * r;
                    MeshObj(p, "Spike", spike, spikeMat, pos, Quaternion.FromToRotation(Vector3.up, n + Vector3.up * 0.15f), new Vector3(0.012f, 0.04f, 0.012f));
                }
            for (int i = 0; i < 6; i++)
            {
                Vector3 n = Quaternion.Euler(0f, i * 60f, 0f) * (Vector3.forward * 0.6f + Vector3.up).normalized;
                MeshObj(p, "Spike", spike, spikeMat, new Vector3(0f, SoilY + h, 0f) + n * r * 0.85f, Quaternion.FromToRotation(Vector3.up, n), new Vector3(0.012f, 0.04f, 0.012f));
            }
        }

        // ---------- 중앙 선반 (정면 = -Z, 뒤쪽·양옆 난간) ----------

        static void BuildShelfVisual(Transform t)
        {
            var wood = Mat("ShelfWood", new Color(0.42f, 0.28f, 0.16f));
            var dark = Mat("ShelfWoodDark", new Color(0.28f, 0.18f, 0.1f));
            Box(t, "Top", new Vector3(0f, ShelfTopY - 0.03f, 0f), new Vector3(2.9f, 0.06f, 0.72f), wood, true);
            foreach (float x in new[] { -1.38f, 1.38f })
                foreach (float z in new[] { -0.3f, 0.3f })
                    Box(t, "Leg", new Vector3(x, (ShelfTopY - 0.06f) * 0.5f, z), new Vector3(0.08f, ShelfTopY - 0.06f, 0.08f), dark, true);
            Box(t, "LowerBoard", new Vector3(0f, 0.2f, 0f), new Vector3(2.8f, 0.03f, 0.62f), dark);

            // 난간: 뒤쪽(+Z)과 양옆. 앞쪽에는 난간이 없어서 "난간이 없는 쪽 = 정면"이 된다 (명세서 8장 추천안)
            const float railTop = ShelfTopY + 0.3f, railMid = ShelfTopY + 0.15f, back = 0.33f, side = 1.42f;
            foreach (float x in new[] { -side, -0.47f, 0.47f, side })
                Box(t, "Post_Back", new Vector3(x, ShelfTopY + 0.15f, back), new Vector3(0.04f, 0.3f, 0.04f), dark);
            Box(t, "Rail_Back_Top", new Vector3(0f, railTop, back), new Vector3(2.88f, 0.035f, 0.035f), dark);
            Box(t, "Rail_Back_Mid", new Vector3(0f, railMid, back), new Vector3(2.88f, 0.025f, 0.025f), dark);
            foreach (float x in new[] { -side, side })
            {
                Box(t, "Post_SideFront", new Vector3(x, ShelfTopY + 0.15f, -back), new Vector3(0.04f, 0.3f, 0.04f), dark);
                Box(t, "Rail_Side_Top", new Vector3(x, railTop, 0f), new Vector3(0.035f, 0.035f, 0.7f), dark);
                Box(t, "Rail_Side_Mid", new Vector3(x, railMid, 0f), new Vector3(0.025f, 0.025f, 0.7f), dark);
            }
        }

        // ---------- 화분 받침 벤치 (북쪽 벽 앞) ----------

        const float BenchTopY = 0.45f;

        static void BuildBenchVisual(Transform t)
        {
            var wood = Mat("BenchWood", new Color(0.45f, 0.36f, 0.24f));
            var dark = Mat("ShelfWoodDark", new Color(0.28f, 0.18f, 0.1f));
            Box(t, "Top", new Vector3(0f, BenchTopY - 0.025f, 0f), new Vector3(7.3f, 0.05f, 0.5f), wood, true);
            for (int i = 0; i < 6; i++)
            {
                float x = -3.55f + i * 1.42f;
                foreach (float z in new[] { -0.2f, 0.2f })
                    Box(t, "Leg", new Vector3(x, (BenchTopY - 0.05f) * 0.5f, z), new Vector3(0.06f, BenchTopY - 0.05f, 0.06f), dark, true);
            }
        }

        // ---------- 온실 기록일지 (펼친 책) ----------

        static void BuildJournalVisual(Transform t)
        {
            var cover = Mat("JournalCover", new Color(0.3f, 0.36f, 0.22f));
            var paper = Mat("JournalPaper", new Color(0.93f, 0.89f, 0.78f));
            var ink = Mat("JournalInk", new Color(0.35f, 0.28f, 0.2f));
            foreach (float s in new[] { -1f, 1f })
            {
                var c = Box(t, "Cover", new Vector3(s * 0.122f, 0.006f, 0f), new Vector3(0.24f, 0.012f, 0.33f), cover);
                c.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 3f);
                var pg = Box(t, "Pages", new Vector3(s * 0.115f, 0.018f, 0f), new Vector3(0.22f, 0.012f, 0.31f), paper);
                pg.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 3f);
            }
            // 왼쪽: 작은 사진 4장, 오른쪽: 글 줄 (멀리서도 "사진+기록" 책으로 보이게)
            for (int i = 0; i < 4; i++)
                Box(t, "PhotoMark", new Vector3(-0.17f + (i % 2) * 0.1f, 0.0255f, 0.06f - (i / 2) * 0.12f), new Vector3(0.075f, 0.002f, 0.075f), ink);
            for (int i = 0; i < 6; i++)
                Box(t, "LineMark", new Vector3(0.115f, 0.0255f, 0.1f - i * 0.04f), new Vector3(0.16f, 0.002f, 0.006f), ink);
        }

        // =====================================================================
        // 기능 프리팹
        // =====================================================================

        static GameObject BuildConservatory(Gim004Config config, Dictionary<Gim004PotId, GameObject> potVisuals, GameObject shelfVisual, GameObject benchVisual)
        {
            var go = new GameObject("GIM004_Conservatory");
            go.transform.position = ConservatoryOrigin;
            var t = go.transform;

            var ctrl = go.AddComponent<Gim004Controller>();
            SetRef(ctrl, "config", config);

            // ---- 중앙 선반 ----
            var shelf = new GameObject("OBJ_CENTER_SHELF").transform;
            shelf.SetParent(t, false);
            shelf.localPosition = ShelfLocal;
            shelf.localRotation = Quaternion.Euler(0f, ShelfYaw, 0f);
            AddVisual(shelf, shelfVisual);
            shelf.gameObject.AddComponent<Gim004ShelfFeedback>();

            var padMat = Mat("SlotPad", new Color(0.22f, 0.15f, 0.09f));
            var brass = Mat("Brass", new Color(0.78f, 0.6f, 0.28f), 0.55f);
            var ringMat = Mat("ButtonRing", new Color(0.15f, 0.12f, 0.1f));
            for (int i = 0; i < Gim004Rules.SlotCount; i++)
            {
                // 정면(-Z)에서 바라본 왼쪽 = 로컬 -X → 0번이 로컬 x가 가장 작은 쪽
                float x = (i - 1.5f) * SlotSpacing;
                var slot = new GameObject("OBJ_SHELF_SLOT_" + (i + 1)).transform;
                slot.SetParent(shelf, false);
                slot.localPosition = new Vector3(x, ShelfTopY, 0.02f);
                var col = slot.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.06f, 0f);
                col.size = new Vector3(0.6f, 0.12f, 0.6f);

                Prim(PrimitiveType.Cylinder, slot, "Pad", new Vector3(0f, 0.004f, 0f), new Vector3(0.44f, 0.004f, 0.44f), padMat);
                var anchor = new GameObject("Anchor").transform;
                anchor.SetParent(slot, false);
                anchor.localPosition = new Vector3(0f, 0.008f, 0f);

                // 자리별 버튼 (앞쪽 가장자리): 화분이 놓이면 눌린다
                Prim(PrimitiveType.Cylinder, slot, "ButtonRing", new Vector3(0f, 0.003f, -0.3f), new Vector3(0.1f, 0.003f, 0.1f), ringMat);
                var button = new GameObject("Button").transform;
                button.SetParent(slot, false);
                button.localPosition = new Vector3(0f, 0.016f, -0.3f);
                Prim(PrimitiveType.Cylinder, button, "Cap", Vector3.zero, new Vector3(0.075f, 0.012f, 0.075f), brass);

                var s = slot.gameObject.AddComponent<Gim004ShelfSlot>();
                SetInt(s, "index", i);
                SetRef(s, "anchor", anchor);
                SetRef(s, "button", button);
            }

            // ---- 후보 화분 7개 + 받침 벤치 ----
            var bench = new GameObject("OBJ_POT_BENCH").transform;
            bench.SetParent(t, false);
            bench.localPosition = BenchLocal;
            AddVisual(bench, benchVisual);
            var homes = new GameObject("PotHomes").transform;
            homes.SetParent(bench, false);
            for (int i = 0; i < BenchOrder.Length; i++)
            {
                var id = BenchOrder[i];
                var pot = BuildPot(id, potVisuals[id]);
                pot.transform.SetParent(homes, false);
                pot.transform.localPosition = new Vector3((i - 3f) * 1f, BenchTopY, 0f);
                pot.transform.localRotation = Quaternion.Euler(0f, i * 47f, 0f);   // 화분마다 조금씩 돌려 놓아 똑같이 보이지 않게
            }
            return go;
        }

        // 화분 하나: 루트(기능: Gim004Pot + 콜라이더) + 자식 "Visual"(겉모습 프리팹)
        static GameObject BuildPot(Gim004PotId id, GameObject visualPrefab)
        {
            var go = new GameObject("OBJ_POT_" + id);
            var visual = AddVisual(go.transform, visualPrefab);

            // 콜라이더를 겉모습 크기에 맞춘다 (모델을 바꾼 뒤 Build All을 다시 돌리면 다시 맞춰진다)
            var b = LocalRendererBounds(go.transform, visual);
            var col = go.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;

            var pot = go.AddComponent<Gim004Pot>();
            var so = new SerializedObject(pot);
            so.FindProperty("potId").enumValueIndex = (int)id;
            so.FindProperty("displayName").stringValue = "화분";
            so.FindProperty("holdOffset").vector3Value = new Vector3(-0.38f, -0.6f, 0.95f);
            so.FindProperty("returnSeconds").floatValue = 0.6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        static GameObject BuildJournal(Gim004Config config, GameObject visualPrefab)
        {
            var go = new GameObject("GIM004_Journal");
            go.transform.position = JournalPos;
            go.transform.rotation = Quaternion.Euler(0f, JournalYaw, 0f);
            AddVisual(go.transform, visualPrefab);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.04f, 0f);
            col.size = new Vector3(0.52f, 0.08f, 0.36f);
            var j = go.AddComponent<Gim004Journal>();
            SetRef(j, "config", config);
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

        static Bounds LocalRendererBounds(Transform root, GameObject visual)
        {
            bool first = true;
            var b = new Bounds(Vector3.up * 0.3f, Vector3.one * 0.4f);
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
        // 기록일지 사진 촬영 (실제 화분 겉모습 프리팹을 찍는다)
        // =====================================================================

        // 사진마다 구별 특징이 잘 보이는 각도 (명세서 8장): 높이각, 방위각(도)
        static void PhotoAngle(Gim004PotId id, out float elevation, out float azimuth)
        {
            switch (id)
            {
                case Gim004PotId.E:
                case Gim004PotId.F: elevation = 62f; azimuth = 20f; break;   // 잎 안쪽 구멍 / 가장자리 갈라짐이 보이게 위에서
                case Gim004PotId.A:
                case Gim004PotId.B: elevation = 14f; azimuth = -20f; break;  // 매달린 종꽃 개수가 보이게 옆에서
                default: elevation = 52f; azimuth = 0f; break;               // 별꽃 색(3송이 모두)과 잎 모양
            }
        }

        public static void CapturePhotos(Gim004Config cfg)
        {
            EnsureFolder(PhotoDir);
            if (cfg.photos == null || cfg.photos.Length != 4) cfg.photos = new Texture2D[4];
            for (int i = 0; i < 4; i++)
            {
                var id = cfg.photoPots != null && i < cfg.photoPots.Length ? cfg.photoPots[i] : Gim004PotId.A;
                var visual = AssetDatabase.LoadAssetAtPath<GameObject>(VisualDir + "/Gim004_PotVisual_" + id + ".prefab");
                if (visual == null) { Debug.LogWarning("[GIM-004] 화분 겉모습 프리팹이 없어 사진 " + (i + 1) + "을 찍지 못했습니다: " + id); continue; }
                string path = PhotoDir + "/Gim004_Photo" + (i + 1) + ".png";
                if (!Capture(visual, id, path)) continue;
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Default;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.sRGBTexture = true;
                imp.SaveAndReimport();
                cfg.photos[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            EditorUtility.SetDirty(cfg);
            AssetDatabase.SaveAssets();
            Debug.Log("[GIM-004] 기록일지 사진 4장을 실제 화분 모델로 찍었습니다 (" + PhotoDir + ").");
        }

        static bool Capture(GameObject visualPrefab, Gim004PotId id, string path)
        {
            const int size = 512;
            var pru = new PreviewRenderUtility();
            try
            {
                var go = Object.Instantiate(visualPrefab);
                pru.AddSingleGO(go);
                var bounds = new Bounds(go.transform.position + Vector3.up * 0.4f, Vector3.one * 0.2f);
                bool first = true;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    // 화분 몸통보다 식물을 크게 담는다: 식물 부분 위주로 영역을 잡는다
                    if (r.name == "Pot" || r.name == "Soil") continue;
                    if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
                }
                bounds.Encapsulate(new Vector3(bounds.center.x, SoilY - 0.05f, bounds.center.z));

                PhotoAngle(id, out float elev, out float azim);
                var cam = pru.camera;
                cam.fieldOfView = 28f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 20f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.74f, 0.77f, 0.7f);
                Vector3 dir = Quaternion.Euler(elev, 180f + azim, 0f) * Vector3.forward;   // 카메라가 바라보는 방향
                float radius = bounds.extents.magnitude;
                // 큰 잎(E·F)은 위에서 찍어 빈 공간이 많으므로 더 가까이 (잎 모양이 사진에 크게 보이게)
                float zoom = id == Gim004PotId.E || id == Gim004PotId.F ? 0.72f : 0.95f;
                float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * zoom;
                cam.transform.position = bounds.center - dir * dist;
                cam.transform.rotation = Quaternion.LookRotation(dir);

                // 흰 꽃잎이 노랗게 보이지 않게 중성 흰빛 조명 (명세서 8장 사진 제작 메모)
                pru.lights[0].color = Color.white;
                pru.lights[0].intensity = 1.25f;
                pru.lights[0].transform.rotation = Quaternion.Euler(50f, 150f, 0f);
                pru.lights[1].color = new Color(0.9f, 0.95f, 1f);
                pru.lights[1].intensity = 0.6f;
                pru.lights[1].transform.rotation = Quaternion.Euler(20f, -40f, 0f);
                pru.ambientColor = new Color(0.42f, 0.42f, 0.42f);

                pru.BeginStaticPreview(new Rect(0, 0, size, size));
                pru.Render(true);
                var tex = pru.EndStaticPreview();
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[GIM-004] 사진 촬영 실패 (" + id + "): " + e);
                return false;
            }
            finally
            {
                pru.Cleanup();
            }
        }

        // =====================================================================
        // 도우미
        // =====================================================================

        static UnityEngine.SceneManagement.Scene ActiveScene() { return UnityEngine.SceneManagement.SceneManager.GetActiveScene(); }
        static IEnumerable<GameObject> SceneRoots() { return ActiveScene().GetRootGameObjects(); }

        static void SetRef(Object o, string field, Object value) { var s = new SerializedObject(o); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetInt(Object o, string field, int value) { var s = new SerializedObject(o); s.FindProperty(field).intValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
