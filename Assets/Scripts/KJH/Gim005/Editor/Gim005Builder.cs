using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gim005.EditorTools
{
    // GIM-005 덤불 정원 미로 자동 제작 도구. Unity 메뉴 Tools/KJH/GIM-005/Build All.
    // 몇 번을 다시 실행해도 된다: 프리팹을 다시 만들고(같은 경로·GUID에 덮어씀) 씬의 GIM005_KJH 루트를 새로 배치한다.
    // 데이터 에셋(Config, 문양 목록)은 "없을 때만" 만든다 → Inspector에서 바꾼 수치·문구·이미지가 재실행으로 지워지지 않는다.
    // 기존 맵 오브젝트(Courtyard, F2_MaidRoom, Rail_StairFront 등)는 전혀 수정하지 않는다. 위치만 참고한다.
    //
    // 만드는 것 (모두 Assets/Prefabs/KJH/Gim005, 임시 도형 — 아트가 오면 교체):
    //  - Data/Gim005Config.asset, Data/Gim005SymbolData.asset (문양 13종, 숫자 1~13)
    //  - Gim005GateBush.prefab     문양 덤불 (양면 명판 Quad + 숫자)
    //  - Gim005SymbolButton.prefab 문양 버튼 (Quad + 숫자 + 불빛)
    //  - GIM005_Maze.prefab        1층 정원 미로 일체 (컨트롤러, 덤불 벽, 문양 덤불 8개, 입구, 시작점, 중앙 버튼)
    //  - GIM005_GardenRoom.prefab  2층 정원 관리실 패널 일체 (버튼 13개, 남은 시간, 상태 표시등)
    //  - Gim005Sync.prefab         네트워크 동기화 (DefaultNetworkPrefabs에 등록)
    // 씬: GIM005_KJH(루트) 아래 두 프리팹 인스턴스 + 최상위 Gim005Sync_KJH
    public static class Gim005Builder
    {
        const string Root = "Assets/Prefabs/KJH/Gim005";
        const string DataDir = Root + "/Data";
        const string MatDir = Root + "/Materials";

        // 배치 위치 (KJH 씬 Ravenwood_LGH 맵 기준으로 측정)
        // 1층 정원 Courtyard: x 36~52, z -16~0 (16×16m), 분수대 중심 (44, 0, -8). 북쪽 z=0에 남쪽 복도로 통하는 양문.
        public static readonly Vector3 MazeOrigin = new Vector3(44f, 0f, -8f);
        // 2층 정원 관리실(F2_MaidRoom): x 20~33, z -16~-9, 바닥 y 3.9. 북쪽 내벽(z=-9) 방 쪽 면에 패널을 붙인다.
        public static readonly Vector3 RoomOrigin = new Vector3(26.5f, 3.9f, -9.13f);

        const float Half = 8f;                                          // 정원 반 폭
        const float Cell = 2f * Half / Gim005MazeLayout.Cols;          // 칸 크기 ≈ 2.29m
        const float HedgeT = 0.4f;                                      // 덤불 벽 두께
        const float HedgeH = 3.2f;                                      // 덤불 높이 (점프해도 넘겨볼 수 없게)
        const float OuterInset = 0.33f;                                 // 바깥 덤불을 건물 벽 안쪽으로 들이는 거리
        const float GateT = 0.55f;                                      // 문양 덤불 두께

        // 중앙 광장 버튼 모델 (VARCO)
        const string CenterPedestalPath = "Assets/VARCO 3D/KWJ/Prefabs/_NeedsCollider/Prop_PuzzleButtonPedestal.prefab";
        const string CenterButtonPath = "Assets/VARCO 3D/KWJ/Prefabs/_NeedsCollider/Prop_PuzzleButton.prefab";

        // 2층 패널 모델 (VARCO)
        const string PanelModelPath = "Assets/VARCO 3D/KWJ/Prefabs/_NeedsCollider/Prop_ButtonPanel.prefab";
        const float PanelCenterY = 1.75f;                               // 패널 모델 가운데 높이 (바닥 기준)
        const float PanelFaceZ = 0.415f;                                // 앞면(버튼 붙는 면) 벽에서의 최소 깊이
        const float PanelFaceMaxZ = 0.427f;                             // 앞면이 살짝 기울어 가장 튀어나온 곳

        static Font font;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [MenuItem("Tools/KJH/GIM-005/Build All")]
        public static void BuildAll()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mats.Clear();
            EnsureFolder(Root); EnsureFolder(DataDir); EnsureFolder(MatDir);

            var config = BuildData();
            var gatePrefab = BuildGatePrefab();
            var buttonPrefab = BuildButtonPrefab();
            var syncPrefab = BuildSyncPrefab();

            foreach (var go in SceneRoots().Where(g => g.name == "GIM005_KJH" || g.name == "Gim005Sync_KJH").ToList()) Object.DestroyImmediate(go);

            var root = new GameObject("GIM005_KJH").transform;
            SceneManagerMove(root.gameObject);

            var maze = BuildMaze(config, gatePrefab);
            maze.transform.SetParent(root, true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(maze, Root + "/GIM005_Maze.prefab", InteractionMode.AutomatedAction);

            var room = BuildRoom(buttonPrefab);
            room.transform.SetParent(root, true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(room, Root + "/GIM005_GardenRoom.prefab", InteractionMode.AutomatedAction);

            var sync = (GameObject)PrefabUtility.InstantiatePrefab(syncPrefab, SceneManagementScene());
            sync.name = "Gim005Sync_KJH";
            RegisterNetworkPrefab(syncPrefab);

            EditorSceneManager.MarkSceneDirty(SceneManagementScene());
            Selection.activeObject = root.gameObject;
            Debug.Log("[GIM-005] Build All 완료. 씬을 저장(Ctrl+S)하세요.");
        }

        // =====================================================================
        // 데이터
        // =====================================================================

        static Gim005Config BuildData()
        {
            var symbols = LoadOrCreate<Gim005SymbolData>(DataDir + "/Gim005SymbolData.asset", out bool symCreated);
            if (symCreated || symbols.Count == 0) { symbols.FillDefaults(13); EditorUtility.SetDirty(symbols); }

            var cfg = LoadOrCreate<Gim005Config>(DataDir + "/Gim005Config.asset", out bool cfgCreated);
            if (cfgCreated || cfg.symbols == null) { cfg.symbols = symbols; EditorUtility.SetDirty(cfg); }
            AssetDatabase.SaveAssets();
            return cfg;
        }

        // =====================================================================
        // 부품 프리팹
        // =====================================================================

        // 문양 덤불: 폭 = 칸 크기(양옆 덤불 벽과 살짝 겹침), 앞(-z)·뒤(+z) 명판
        static GameObject BuildGatePrefab()
        {
            var go = new GameObject("Gim005GateBush");
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);

            var bush = Box(body, "Bush", new Vector3(0f, HedgeH * 0.5f, 0f), new Vector3(Cell, HedgeH, GateT), Mat("GateHedge", new Color(0.13f, 0.27f, 0.2f)));
            // 꽃 장식 (덤불이 일반 벽과 다르다는 걸 멀리서도 알 수 있게)
            var flower = Mat("GateFlower", new Color(0.75f, 0.2f, 0.35f));
            for (int i = 0; i < 6; i++)
            {
                float x = (i - 2.5f) * Cell / 6.5f;
                float y = 0.5f + (i % 3) * 0.9f + 0.3f * (i % 2);
                if (Mathf.Abs(x) < 0.5f && y > 1.0f && y < 2.4f) y = 2.65f;   // 명판을 가리지 않게
                Prim(PrimitiveType.Sphere, body, "Flower", new Vector3(x, y, -GateT * 0.5f), Vector3.one * 0.16f, flower);
                Prim(PrimitiveType.Sphere, body, "Flower", new Vector3(-x, y + 0.2f, GateT * 0.5f), Vector3.one * 0.16f, flower);
            }

            var faces = new List<Gim005SymbolDisplay>();
            faces.Add(Plaque(body, "Plaque_Front", new Vector3(0f, 1.65f, -GateT * 0.5f - 0.02f), Quaternion.identity, 0.62f));
            faces.Add(Plaque(body, "Plaque_Back", new Vector3(0f, 1.65f, GateT * 0.5f + 0.02f), Quaternion.Euler(0f, 180f, 0f), 0.62f));
            StripColliders(body, bush.GetComponent<Collider>());

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false; src.spatialBlend = 1f; src.minDistance = 2f; src.maxDistance = 16f; src.rolloffMode = AudioRolloffMode.Linear;

            var gate = go.AddComponent<Gim005Gate>();
            SetRef(gate, "body", body);
            SetRefArray(gate, "faces", faces.Cast<Object>().ToArray());
            SetRefArray(gate, "blockers", new Object[] { bush.GetComponent<Collider>() });
            SetFloat(gate, "sinkDepth", HedgeH + 0.2f);

            return SavePrefab(go, Root + "/Gim005GateBush.prefab");
        }

        // 명판: 나무판 + 그림 Quad + 숫자. 로컬 -z 쪽(앞)에서 읽힌다.
        static Gim005SymbolDisplay Plaque(Transform parent, string name, Vector3 pos, Quaternion rot, float size)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = pos;
            p.localRotation = rot;
            Box(p, "Board", new Vector3(0f, 0f, 0.012f), new Vector3(size, size, 0.03f), Mat("Wood", new Color(0.36f, 0.24f, 0.14f)));
            var quad = Prim(PrimitiveType.Quad, p, "SymbolQuad", new Vector3(0f, 0f, -0.006f), new Vector3(size * 0.82f, size * 0.82f, 1f), Mat("SymbolPlate", new Color(0.92f, 0.88f, 0.76f)));
            var label = Label(p, "1", new Vector3(0f, 0f, -0.012f), Quaternion.identity, size * 0.55f, new Color(0.12f, 0.08f, 0.05f));
            var d = p.gameObject.AddComponent<Gim005SymbolDisplay>();
            SetRef(d, "quad", quad.GetComponent<Renderer>());
            SetRef(d, "text", label);
            return d;
        }

        // 문양 버튼: 로컬 +z가 벽 쪽(누르는 방향), -z 쪽에서 본다
        static GameObject BuildButtonPrefab()
        {
            var go = new GameObject("Gim005SymbolButton");
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0f, -0.06f);
            col.size = new Vector3(0.34f, 0.34f, 0.12f);

            Box(go.transform, "Socket", new Vector3(0f, 0f, -0.01f), new Vector3(0.38f, 0.38f, 0.02f), Mat("PanelMetal", new Color(0.18f, 0.17f, 0.15f)));
            var cap = new GameObject("Cap").transform;
            cap.SetParent(go.transform, false);
            cap.localPosition = new Vector3(0f, 0f, -0.05f);
            Box(cap, "CapMesh", Vector3.zero, new Vector3(0.32f, 0.32f, 0.05f), Mat("ButtonCap", new Color(0.55f, 0.5f, 0.42f)));
            var face = Plaque(cap, "Face", new Vector3(0f, 0f, -0.04f), Quaternion.identity, 0.26f);
            // 버튼 위 작은 불빛
            var lamp = Box(go.transform, "Lamp", new Vector3(0f, 0.205f, -0.03f), new Vector3(0.14f, 0.025f, 0.03f), Mat("Lamp", new Color(0.15f, 0.15f, 0.15f), true));
            StripColliders(go.transform, col);

            var b = go.AddComponent<Gim005SymbolButton>();
            SetRef(b, "display", face);
            SetRef(b, "lamp", lamp.GetComponent<Renderer>());
            SetRef(b, "cap", cap);
            return SavePrefab(go, Root + "/Gim005SymbolButton.prefab");
        }

        static GameObject BuildSyncPrefab()
        {
            string path = Root + "/Gim005Sync.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;   // 이미 있으면 그대로 (GlobalObjectIdHash가 바뀌지 않게)
            var go = new GameObject("Gim005Sync");
            go.AddComponent<NetworkObject>();
            go.AddComponent<Gim005NetSync>();
            return SavePrefab(go, path);
        }

        static void RegisterNetworkPrefab(GameObject prefab)
        {
            var nm = Object.FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (nm == null || nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Count == 0)
            {
                Debug.LogWarning("[GIM-005] NetworkManager의 네트워크 프리팹 목록을 찾지 못했습니다. Gim005Sync.prefab을 직접 등록해 주세요.");
                return;
            }
            var list = nm.NetworkConfig.Prefabs.NetworkPrefabsLists[0];
            if (list.PrefabList.Any(p => p.Prefab == prefab)) return;
            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log("[GIM-005] Gim005Sync를 " + AssetDatabase.GetAssetPath(list) + "에 등록했습니다.");
        }

        // =====================================================================
        // 1층 정원 미로
        // =====================================================================

        // 칸 (c, r)의 경계선 좌표 (미로 루트 기준 로컬). x: 서→동, z: 북(+)→남(-)
        static float LineX(int c) { return -Half + c * Cell; }
        static float LineZ(int r) { return Half - r * Cell; }
        static float CenterX(int c) { return -Half + (c + 0.5f) * Cell; }
        static float CenterZ(int r) { return Half - (r + 0.5f) * Cell; }

        static GameObject BuildMaze(Gim005Config config, GameObject gatePrefab)
        {
            var go = new GameObject("GIM005_Maze");
            go.transform.position = MazeOrigin;
            var t = go.transform;

            var ctrlGo = new GameObject("Gim005Controller");
            ctrlGo.transform.SetParent(t, false);
            var controller = ctrlGo.AddComponent<Gim005Controller>();
            SetRef(controller, "config", config);
            SetRef(controller, "textMaterial", TextMat());
            SetRef(controller, "textFont", font);

            // ---- 덤불 벽 ----
            var hedges = new GameObject("Hedges").transform;
            hedges.SetParent(t, false);
            var hedgeMat = Mat("Hedge", new Color(0.16f, 0.33f, 0.14f));
            var gatesRoot = new GameObject("Gates").transform;
            gatesRoot.SetParent(t, false);
            var gateObjs = new GameObject[Gim005Rules.MaxGates];

            int cols = Gim005MazeLayout.Cols, rows = Gim005MazeLayout.Rows;

            // 세로 경계선 (x = LineX(c)), 각 줄 r마다 벽/덤불/통로 판정
            for (int c = 0; c <= cols; c++)
            {
                var wall = new bool[rows];
                for (int r = 0; r < rows; r++)
                {
                    if (c == 0 || c == cols) { wall[r] = true; continue; }
                    var a = new Gim005MazeLayout.Cell(c - 1, r); var b = new Gim005MazeLayout.Cell(c, r);
                    if (!Gim005MazeLayout.TryGetEdge(a, b, out int gate)) { wall[r] = true; continue; }
                    if (gate >= 0) gateObjs[gate] = PlaceGate(gatesRoot, gatePrefab, gate, new Vector3(LineX(c), 0f, CenterZ(r)), 90f);
                }
                float x = c == 0 ? -Half + OuterInset : c == cols ? Half - OuterInset : LineX(c);
                foreach (var run in Runs(wall))
                {
                    float z0 = LineZ(run.x), z1 = LineZ(run.y + 1);
                    float zA = ClampInner(z0 + HedgeT * 0.5f), zB = ClampInner(z1 - HedgeT * 0.5f);
                    Box(hedges, "Hedge_V" + c, new Vector3(x, HedgeH * 0.5f, (zA + zB) * 0.5f), new Vector3(HedgeT, HedgeH, zA - zB), hedgeMat);
                }
            }

            // 가로 경계선 (z = LineZ(r)), 각 칸 c마다
            for (int r = 0; r <= rows; r++)
            {
                var wall = new bool[cols];
                for (int c = 0; c < cols; c++)
                {
                    if (r == 0) { wall[c] = c != Gim005MazeLayout.EntranceCol; continue; }   // 북쪽: 입구만 뚫림
                    if (r == rows) { wall[c] = true; continue; }
                    var a = new Gim005MazeLayout.Cell(c, r - 1); var b = new Gim005MazeLayout.Cell(c, r);
                    if (!Gim005MazeLayout.TryGetEdge(a, b, out int gate)) { wall[c] = true; continue; }
                    if (gate >= 0) gateObjs[gate] = PlaceGate(gatesRoot, gatePrefab, gate, new Vector3(CenterX(c), 0f, LineZ(r)), 0f);
                }
                float z = r == 0 ? Half - OuterInset : r == rows ? -Half + OuterInset : LineZ(r);
                foreach (var run in Runs(wall))
                {
                    float xA = ClampInner(LineX(run.x) - HedgeT * 0.5f), xB = ClampInner(LineX(run.y + 1) + HedgeT * 0.5f);
                    Box(hedges, "Hedge_H" + r, new Vector3((xA + xB) * 0.5f, HedgeH * 0.5f, z), new Vector3(xB - xA, HedgeH, HedgeT), hedgeMat);
                }
            }

            // 순서 정리 (G1 ~ G8)
            foreach (var g in gateObjs.Where(g => g != null).OrderBy(g => g.name)) g.transform.SetAsLastSibling();

            // ---- 시작점 (입구 밖 남쪽 복도) ----
            var start = new GameObject("OBJ_MAZE_START").transform;
            start.SetParent(t, false);
            start.localPosition = new Vector3(0f, 0.05f, Half + 1.6f);
            start.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // ---- 입구 표시 (빈 오브젝트, 범위는 Gim005MazeArea가 가진다) ----
            var entrance = new GameObject("OBJ_MAZE_ENTRANCE").transform;
            entrance.SetParent(t, false);
            entrance.localPosition = new Vector3(0f, 0f, Half - 0.8f);

            // ---- 중앙 버튼 (광장 남동쪽) ----
            BuildCenterButton(t, new Vector3(2.0f, 0f, -2.8f));

            // ---- 미로 영역 (입구 감지, 시간 초과 복귀, 종소리) ----
            var soundGo = new GameObject("Sound2D");
            soundGo.transform.SetParent(t, false);
            var src2d = soundGo.AddComponent<AudioSource>();
            src2d.playOnAwake = false; src2d.spatialBlend = 0f;

            var area = go.AddComponent<Gim005MazeArea>();
            SetRef(area, "startPoint", start);
            SetRef(area, "source2D", src2d);
            SetVector(area, "areaCenter", new Vector3(0f, 2f, 0f));
            SetVector(area, "areaSize", new Vector3(2f * Half, 5f, 2f * Half));
            SetVector(area, "entranceCenter", new Vector3(0f, 1.3f, Half - 0.8f));
            SetVector(area, "entranceSize", new Vector3(Cell - HedgeT, 2.8f, 1.2f));

            return go;
        }

        static GameObject PlaceGate(Transform parent, GameObject prefab, int gate, Vector3 localPos, float yaw)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            g.name = "OBJ_GATE_BUSH_G" + (gate + 1);
            g.transform.localPosition = localPos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            SetInt(g.GetComponent<Gim005Gate>(), "gateIndex", gate);
            return g;
        }

        static void BuildCenterButton(Transform parent, Vector3 localPos)
        {
            var root = new GameObject("OBJ_CENTER_BUTTON").transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            Transform cap;
            var pedestalModel = AssetDatabase.LoadAssetAtPath<GameObject>(CenterPedestalPath);
            var buttonModel = AssetDatabase.LoadAssetAtPath<GameObject>(CenterButtonPath);
            if (pedestalModel != null && buttonModel != null)
            {
                // VARCO 받침대(0.36×0.36×1.0m, 위 ButtonMount) + 그 위 버튼(지름 0.18m, 높이 0.1m, 바닥 피벗)
                var ped = (GameObject)PrefabUtility.InstantiatePrefab(pedestalModel, root);
                ped.name = "Pedestal";
                var mount = ped.transform.Find("ButtonMount");
                float topY = mount != null ? mount.localPosition.y : 1.0f;
                var btn = (GameObject)PrefabUtility.InstantiatePrefab(buttonModel, root);
                btn.name = "Cap";
                btn.transform.localPosition = new Vector3(0f, topY, 0f);
                cap = btn.transform;
                // 조준용 콜라이더 (루트에 둬서 눌려 내려가도 판정 범위는 그대로)
                var pc = root.gameObject.AddComponent<BoxCollider>();
                pc.center = new Vector3(0f, topY * 0.5f, 0f);
                pc.size = new Vector3(0.36f, topY, 0.36f);
                var bc = root.gameObject.AddComponent<BoxCollider>();
                bc.center = new Vector3(0f, topY + 0.05f, 0f);
                bc.size = new Vector3(0.18f, 0.1f, 0.18f);
            }
            else
            {
                Debug.LogWarning("[GIM-005] 중앙 버튼 모델이 없어 임시 도형으로 만듭니다.");
                var stone = Mat("Stone", new Color(0.62f, 0.6f, 0.56f));
                Prim(PrimitiveType.Cylinder, root, "Pedestal", new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0.5f), stone);
                Prim(PrimitiveType.Cylinder, root, "Top", new Vector3(0f, 1.02f, 0f), new Vector3(0.62f, 0.03f, 0.62f), stone);
                var capGo = Prim(PrimitiveType.Cylinder, root, "Cap", new Vector3(0f, 1.08f, 0f), new Vector3(0.26f, 0.04f, 0.26f), Mat("CenterCap", new Color(0.7f, 0.15f, 0.12f)));
                Object.DestroyImmediate(capGo.GetComponent<Collider>());
                cap = capGo.transform;
            }
            var src = root.gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false; src.spatialBlend = 1f; src.maxDistance = 14f; src.rolloffMode = AudioRolloffMode.Linear;
            var b = root.gameObject.AddComponent<Gim005CenterButton>();
            SetRef(b, "cap", cap);
            SetRef(b, "source", src);
        }

        // 연속된 true 구간 (시작, 끝) 목록
        static IEnumerable<Vector2Int> Runs(bool[] wall)
        {
            int i = 0;
            while (i < wall.Length)
            {
                if (!wall[i]) { i++; continue; }
                int j = i;
                while (j + 1 < wall.Length && wall[j + 1]) j++;
                yield return new Vector2Int(i, j);
                i = j + 1;
            }
        }

        static float ClampInner(float v)
        {
            float lim = Half - OuterInset + HedgeT * 0.5f;
            return Mathf.Clamp(v, -lim, lim);
        }

        // =====================================================================
        // 2층 정원 관리실 패널
        // =====================================================================

        static GameObject BuildRoom(GameObject buttonPrefab)
        {
            var go = new GameObject("GIM005_GardenRoom");
            go.transform.position = RoomOrigin;
            var t = go.transform;

            var panelGo = new GameObject("OBJ_SYMBOL_PANEL");
            panelGo.transform.SetParent(t, false);
            var p = panelGo.transform;

            // 패널 겉모습: VARCO Prop_ButtonPanel (2.5 × 1.84 × 0.5m, 뒷면이 벽 z=0, 앞면이 -z 쪽).
            // 모델 원본은 앞면이 +z라 180° 돌려 붙인다. 앞면(버튼 붙는 면) 깊이 ≈ 0.415~0.427m, 테두리는 0.46m까지 튀어나옴.
            // 쓸 수 있는 앞면 범위(패널 로컬): x -1.0~1.0, y 1.03~2.47. 오른쪽 위(플레이어 기준 왼쪽 위)에 타이머 화면 홈.
            var panelModel = AssetDatabase.LoadAssetAtPath<GameObject>(PanelModelPath);
            if (panelModel != null)
            {
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(panelModel, p);
                vis.name = "PanelVisual";
                vis.transform.localPosition = new Vector3(0f, PanelCenterY, 0f);
                vis.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                Debug.LogWarning("[GIM-005] " + PanelModelPath + " 이 없어 임시 판으로 만듭니다.");
                Box(p, "Board", new Vector3(0f, PanelCenterY, -PanelFaceZ * 0.5f), new Vector3(2.5f, 1.84f, PanelFaceZ), Mat("PanelBoard", new Color(0.22f, 0.25f, 0.2f)));
            }
            // 몸통 콜라이더는 앞면까지만 (버튼 콜라이더를 가리지 않게)
            var bodyCol = new GameObject("PanelCollider");
            bodyCol.transform.SetParent(p, false);
            var bc = bodyCol.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, PanelCenterY, -PanelFaceZ * 0.5f);
            bc.size = new Vector3(2.5f, 1.84f, PanelFaceZ);

            float frontZ = -(PanelFaceMaxZ + 0.008f);   // 글자: 앞면보다 살짝 앞
            Label(p, "정원 관리", new Vector3(0.45f, 2.41f, frontZ), Quaternion.identity, 0.09f, new Color(0.9f, 0.85f, 0.7f));

            // 남은 시간 (OBJ_TIMER_DISPLAY) — 모델의 타이머 화면 홈(가운데 x -0.57, y 2.38, 화면 깊이 0.435m) 안에
            var timerRoot = new GameObject("OBJ_TIMER_DISPLAY").transform;
            timerRoot.SetParent(p, false);
            timerRoot.localPosition = new Vector3(-0.57f, 2.38f, -0.44f);
            if (panelModel == null)
                Box(timerRoot, "Back", new Vector3(0f, 0f, 0.003f), new Vector3(0.66f, 0.12f, 0.006f), Mat("TimerBack", new Color(0.05f, 0.05f, 0.05f)));
            var timerText = Label(timerRoot, "3:00", Vector3.zero, Quaternion.identity, 0.1f, Color.white);

            // 상태 표시등 + 글자 (타이머 반대쪽 위)
            var lamp = Prim(PrimitiveType.Sphere, p, "StatusLamp", new Vector3(0.15f, 2.30f, -(PanelFaceMaxZ + 0.03f)), Vector3.one * 0.11f, Mat("Lamp", new Color(0.15f, 0.15f, 0.15f), true));
            var statusText = Label(p, "대기 중", new Vector3(0.5f, 2.30f, frontZ), Quaternion.identity, 0.07f, new Color(0.9f, 0.85f, 0.7f));

            // 버튼 13개 (5-4-4 배열). 앞면 폭(2.0m)에 5개가 들어가도록 0.85배, 소켓 뒷면이 앞면에 닿게.
            var buttons = new GameObject("Buttons").transform;
            buttons.SetParent(p, false);
            int[] perRow = { 5, 4, 4 };
            float[] rowY = { 1.99f, 1.61f, 1.23f };
            float[] rowFaceZ = { 0.424f, 0.418f, 0.416f };   // 줄 높이에서의 앞면 깊이 (위로 갈수록 살짝 튀어나옴)
            const float dx = 0.40f;
            const float buttonScale = 0.85f;
            int symbol = 0;
            for (int row = 0; row < perRow.Length; row++)
            {
                for (int i = 0; i < perRow[row]; i++)
                {
                    var b = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, buttons);
                    b.name = "OBJ_SYMBOL_BUTTON_" + (symbol + 1).ToString("00");
                    b.transform.localPosition = new Vector3((i - (perRow[row] - 1) * 0.5f) * dx, rowY[row], -((panelModel != null ? rowFaceZ[row] : PanelFaceZ) + 0.002f));
                    b.transform.localScale = Vector3.one * buttonScale;
                    SetInt(b.GetComponent<Gim005SymbolButton>(), "symbol", symbol);
                    symbol++;
                }
            }
            StripColliders(p, bc, buttons);

            var src = panelGo.AddComponent<AudioSource>();
            src.playOnAwake = false; src.spatialBlend = 1f; src.minDistance = 2f; src.maxDistance = 12f; src.rolloffMode = AudioRolloffMode.Linear;
            var panel = panelGo.AddComponent<Gim005Panel>();
            SetRef(panel, "timerText", timerText);
            SetRef(panel, "statusText", statusText);
            SetRef(panel, "statusLamp", lamp.GetComponent<Renderer>());
            SetRef(panel, "source", src);
            return go;
        }

        // =====================================================================
        // 도우미
        // =====================================================================

        static GameObject SavePrefab(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material m)
        {
            return Prim(PrimitiveType.Cube, parent, name, localPos, size, m);
        }

        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        // 3D 글자 (임시 라벨). size = 글자 높이(m)
        static TextMesh Label(Transform parent, string text, Vector3 localPos, Quaternion localRot, float size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            Vector3 ls = parent.lossyScale;
            go.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, ls.x), 1f / Mathf.Max(0.0001f, ls.y), 1f / Mathf.Max(0.0001f, ls.z));
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = size / (tm.fontSize * 0.1f) * 1.4f;   // TextMesh 줄 높이 ≈ fontSize × characterSize × 0.1, 글자 몸통은 그보다 작아 보정
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = TextMat();
            return tm;
        }

        // 깊이 검사를 하는 글자 머티리얼 (기본 글꼴 머티리얼은 URP에서 벽 너머로 비쳐 보임)
        static Material TextMat()
        {
            if (mats.TryGetValue("__text", out var cached)) return cached;
            string path = MatDir + "/Gim005_WorldText.mat";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(MatDir + "/Gim005_WorldText.shader");
            if (shader == null) return font.material;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.mainTexture = null;   // 글꼴 텍스처는 실행 중에 만들어진다 → Gim005Controller가 연결
            EditorUtility.SetDirty(m);
            mats["__text"] = m;
            return m;
        }

        // keep 이외의 콜라이더 제거 (skipUnder 아래는 건드리지 않음)
        static void StripColliders(Transform t, Collider keep, Transform skipUnder = null)
        {
            foreach (var c in t.GetComponentsInChildren<Collider>(true))
            {
                if (c == keep) continue;
                if (skipUnder != null && c.transform.IsChildOf(skipUnder)) continue;
                Object.DestroyImmediate(c);
            }
        }

        static Material Mat(string name, Color color, bool emissive = false)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            string path = MatDir + "/Gim005_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            created = a == null;
            if (created)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            return a;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static UnityEngine.SceneManagement.Scene SceneManagementScene()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        }

        static void SceneManagerMove(GameObject go)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, SceneManagementScene());
        }

        static IEnumerable<GameObject> SceneRoots() { return SceneManagementScene().GetRootGameObjects(); }

        static SerializedObject So(Object o) { return new SerializedObject(o); }
        static void SetRef(Object o, string field, Object value) { var s = So(o); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetInt(Object o, string field, int value) { var s = So(o); s.FindProperty(field).intValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetFloat(Object o, string field, float value) { var s = So(o); s.FindProperty(field).floatValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetVector(Object o, string field, Vector3 value) { var s = So(o); s.FindProperty(field).vector3Value = value; s.ApplyModifiedPropertiesWithoutUndo(); }

        static void SetRefArray(Object o, string field, Object[] values)
        {
            var s = So(o);
            var p = s.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            s.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
