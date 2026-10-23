using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gim002.EditorTools
{
    // GIM-002 씬 자동 배치 도구. Unity 메뉴 Tools/KJH/GIM-002/Build All.
    // 몇 번을 다시 실행해도 된다: 이전에 만든 GIM002 오브젝트를 지우고 새로 만든다.
    // 단, 데이터 에셋(Config, 장면 데이터, 열쇠)은 "없을 때만" 만든다 → Inspector에서 바꾼 수치·문구가 재실행으로 지워지지 않는다.
    //
    // 만드는 것 (모두 임시 도형, 아트가 오면 교체):
    //  - Assets/Prefabs/KJH/Gim002/Data : Gim002Config, 장면 1~3, 열쇠 2종
    //  - Assets/Prefabs/KJH/Gim002 : 표시등 프리팹 2종(준비=커튼 모양, 진행=둥근 램프), Gim002Sync 네트워크 프리팹
    //  - 씬: GIM002 루트 아래 Controller / 2층 아이방 무대 일체 / 1층 음악실 건반·표시등·놀이책
    //  - 기존 오브젝트에 컴포넌트 추가: 음악실 문 2개(Gim002KeyTarget), 두 열쇠(Gim002KeyItem)
    public static class Gim002Builder
    {
        const string Root = "Assets/Prefabs/KJH/Gim002";
        const string DataDir = Root + "/Data";
        const string MatDir = Root + "/Materials";

        // 배치 위치 (KJH 씬 LGH 맵 기준으로 측정한 값. 🟡 정확한 위치는 Unity에서 확정 → 옮겨도 코드는 위치에 의존하지 않음)
        static readonly Vector3 StagePos = new Vector3(60.5f, 3.9f, -8.45f);   // 2층 아이방 남쪽 벽, 침대와 장난감 상자 사이 (아이 그림 맞은편)
        static readonly Vector3 DollRackPos = new Vector3(62.4f, 3.9f, -7.35f); // 장난감 상자 앞
        static readonly Vector3 CardRackPos = new Vector3(64.6f, 3.9f, -6.2f);  // 동쪽 벽
        const float PianoKeysX = 39.2f, PianoKeysTop = 0.80f, PianoKeysZMin = 3.6f;
        static readonly Vector3 BookPos = new Vector3(36.55f, 1.2f, 9.8f);     // 음악실 레코드장 위

        // 임시 라벨 전체 크기 배율 (1.5m 정도 떨어져서 읽을 수 있게)
        const float LabelScale = 2.5f;

        static Font font;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [MenuItem("Tools/KJH/GIM-002/Build All")]
        public static void BuildAll()
        {
            // 씬의 무대·표시등은 실제 모델(KWJ Puppet)로 교체되어 있다. 다시 만들면 임시 박스 모양으로 돌아간다.
            // 또 씬 루트 이름이 GIM002_KJH로 바뀌어 있어서, 다시 만들면 기존 것을 지우지 못하고 GIM002가 하나 더 생긴다.
            if (!EditorUtility.DisplayDialog("GIM-002 Build All",
                    "무대와 표시등 프리팹을 임시 박스 모양으로 다시 만듭니다.\n실제 모델 교체 내용이 사라지고, 씬 루트가 GIM002_KJH라면 GIM002가 중복으로 생깁니다. 계속할까요?",
                    "다시 만들기", "취소"))
                return;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mats.Clear();
            EnsureFolder(Root); EnsureFolder(DataDir); EnsureFolder(MatDir);

            var config = BuildData(out var musicKey, out var windKey);
            var readyLamp = BuildLampPrefab("Gim002_Lamp_Ready", true);
            var progressLamp = BuildLampPrefab("Gim002_Lamp_Progress", false);
            var syncPrefab = BuildSyncPrefab();

            // 이전 결과 지우기
            foreach (var go in SceneRoots().Where(g => g.name == "GIM002" || g.name == "Gim002Sync").ToList()) Object.DestroyImmediate(go);

            var root = new GameObject("GIM002").transform;
            var controllerGo = new GameObject("Gim002Controller");
            controllerGo.transform.SetParent(root, false);
            var controller = controllerGo.AddComponent<Gim002Controller>();
            SetRef(controller, "config", config);

            BuildStage(root, readyLamp, progressLamp, windKey);
            BuildMusicRoom(root, readyLamp, progressLamp);
            HookExisting(musicKey, windKey);

            // 네트워크 동기화 오브젝트 (다른 Sync 오브젝트들처럼 씬 최상위에 둔다)
            var sync = (GameObject)PrefabUtility.InstantiatePrefab(syncPrefab, SceneManagementScene());
            sync.name = "Gim002Sync";
            RegisterNetworkPrefab(syncPrefab);

            EditorSceneManager.MarkSceneDirty(SceneManagementScene());
            Selection.activeObject = root.gameObject;
            Debug.Log("[GIM-002] Build All 완료. 씬을 저장(Ctrl+S)하세요.");
        }

        // =====================================================================
        // 데이터
        // =====================================================================

        static Gim002Config BuildData(out Gim002KeyData musicKey, out Gim002KeyData windKey)
        {
            var defaults = Gim002SceneRule.Defaults();
            string[] sentences =
            {
                "먼 곳을 바라보는 친구가 별빛 아래 나무들 사이에서 길을 찾았다.",
                "목마른 잎을 돌보는 친구가 유리 지붕 아래 잠든 정원을 깨웠다.",
                "활로 노래하는 친구가 건반이 기다리는 방에서 축하곡을 연주했다.",
            };
            string[] triggers = { "ANIM_OWL_LOOK", "ANIM_FROG_WATER", "ANIM_CAT_VIOLIN" };

            var scenes = new List<Gim002SceneData>();
            for (int i = 0; i < 3; i++)
            {
                var s = LoadOrCreate<Gim002SceneData>(DataDir + "/Gim002_Scene" + (i + 1) + ".asset", out bool created);
                if (created)
                {
                    s.doll = defaults[i].Doll;
                    s.background = defaults[i].Background;
                    s.symbols = defaults[i].Symbols.ToArray();
                    s.performTrigger = triggers[i];
                    s.sentence = sentences[i];
                    EditorUtility.SetDirty(s);
                }
                scenes.Add(s);
            }

            var cfg = LoadOrCreate<Gim002Config>(DataDir + "/Gim002Config.asset", out bool cfgCreated);
            if (cfgCreated || cfg.scenes.Count == 0) { cfg.scenes = scenes; EditorUtility.SetDirty(cfg); }

            musicKey = LoadOrCreate<Gim002KeyData>(DataDir + "/Gim002Key_MusicRoom.asset", out bool k1);
            if (k1) { musicKey.displayName = "음악실 열쇠"; musicKey.emblem = "높은음자리표"; EditorUtility.SetDirty(musicKey); }
            if (musicKey.inventoryItem == null)
            {
                musicKey.inventoryItem = AssetDatabase.LoadAssetAtPath<LGH.ItemData>("Assets/Objects/LGH/Items/Item_Gim002MusicKey.asset");
                EditorUtility.SetDirty(musicKey);
            }
            windKey = LoadOrCreate<Gim002KeyData>(DataDir + "/Gim002Key_Windup.asset", out bool k2);
            if (k2) { windKey.displayName = "태엽 열쇠"; windKey.emblem = "나비"; EditorUtility.SetDirty(windKey); }
            if (windKey.inventoryItem == null)
            {
                windKey.inventoryItem = AssetDatabase.LoadAssetAtPath<LGH.ItemData>("Assets/Objects/LGH/Items/Item_Gim002StageKey.asset");
                EditorUtility.SetDirty(windKey);
            }

            AssetDatabase.SaveAssets();
            return cfg;
        }

        // =====================================================================
        // 프리팹
        // =====================================================================

        // 준비 표시등(커튼 모양) / 진행 표시등(둥근 램프). 모양은 다르지만 같은 Gim002IndicatorLamp 스크립트를 쓴다.
        static GameObject BuildLampPrefab(string name, bool curtainShape)
        {
            var go = new GameObject(name);
            Renderer bulb;
            if (curtainShape)
            {
                // 커튼 모양: 틀 + 가운데 두 장의 커튼 조각(빛나는 부분) + 위쪽 주름 장식
                Box(go.transform, "Frame", new Vector3(0f, 0.07f, -0.01f), new Vector3(0.16f, 0.14f, 0.02f), Mat("LampBase", new Color(0.2f, 0.15f, 0.1f)));
                var b = Box(go.transform, "Bulb", new Vector3(0f, 0.065f, 0.002f), new Vector3(0.13f, 0.11f, 0.01f), Mat("LampBulb", Color.gray, true));
                Box(go.transform, "Valance", new Vector3(0f, 0.13f, 0.004f), new Vector3(0.15f, 0.025f, 0.012f), Mat("Curtain", new Color(0.55f, 0.05f, 0.08f)));
                Box(go.transform, "Split", new Vector3(0f, 0.06f, 0.008f), new Vector3(0.006f, 0.1f, 0.004f), Mat("LampBase", Color.black));
                bulb = b.GetComponent<Renderer>();
            }
            else
            {
                var baseGo = Prim(PrimitiveType.Cylinder, go.transform, "Base", new Vector3(0f, 0.01f, 0f), new Vector3(0.07f, 0.01f, 0.07f), Mat("LampBase", new Color(0.2f, 0.15f, 0.1f)));
                var b = Prim(PrimitiveType.Sphere, go.transform, "Bulb", new Vector3(0f, 0.05f, 0f), Vector3.one * 0.06f, Mat("LampBulb", Color.gray, true));
                bulb = b.GetComponent<Renderer>();
            }
            // 표시등은 상호작용 대상이 아니라 시선을 막지 않도록 콜라이더를 없앤다
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var lamp = go.AddComponent<Gim002IndicatorLamp>();
            SetRef(lamp, "bulb", bulb);

            string path = Root + "/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject BuildSyncPrefab()
        {
            string path = Root + "/Gim002Sync.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;   // 이미 있으면 그대로 (GlobalObjectIdHash가 바뀌지 않게)
            var go = new GameObject("Gim002Sync");
            go.AddComponent<NetworkObject>();
            go.AddComponent<Gim002NetSync>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // NetworkManager가 쓰는 네트워크 프리팹 목록(DefaultNetworkPrefabs.asset)에 등록
        static void RegisterNetworkPrefab(GameObject prefab)
        {
            var nm = Object.FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (nm == null || nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Count == 0)
            {
                Debug.LogWarning("[GIM-002] NetworkManager의 네트워크 프리팹 목록을 찾지 못했습니다. Gim002Sync.prefab을 직접 등록해 주세요.");
                return;
            }
            var list = nm.NetworkConfig.Prefabs.NetworkPrefabsLists[0];
            if (list.PrefabList.Any(p => p.Prefab == prefab)) return;
            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log("[GIM-002] Gim002Sync를 " + AssetDatabase.GetAssetPath(list) + "에 등록했습니다.");
        }

        // =====================================================================
        // 2층 아이방 무대
        // =====================================================================

        static void BuildStage(Transform root, GameObject readyLamp, GameObject progressLamp, Gim002KeyData windKey)
        {
            var area = new GameObject("F2_Nursery").transform;
            area.SetParent(root, false);

            // 무대 본체 (앞면 = +Z = 아이 그림 쪽)
            var stage = new GameObject("OBJ_STAGE").transform;
            stage.SetParent(area, false);
            stage.position = StagePos;

            var wood = Mat("Wood", new Color(0.35f, 0.2f, 0.1f));
            var darkWood = Mat("DarkWood", new Color(0.18f, 0.1f, 0.05f));
            Box(stage, "Base", new Vector3(0f, 0.35f, 0f), new Vector3(1.4f, 0.7f, 0.7f), wood);
            Box(stage, "BackWall", new Vector3(0f, 1.15f, -0.32f), new Vector3(1.4f, 0.9f, 0.04f), darkWood);
            Box(stage, "Pillar_L", new Vector3(-0.66f, 1.17f, 0.3f), new Vector3(0.08f, 0.95f, 0.08f), wood);
            Box(stage, "Pillar_R", new Vector3(0.66f, 1.17f, 0.3f), new Vector3(0.08f, 0.95f, 0.08f), wood);
            Box(stage, "TopBeam", new Vector3(0f, 1.68f, 0.3f), new Vector3(1.42f, 0.12f, 0.1f), wood);

            // 커튼
            var curtainRoot = new GameObject("Curtain").transform;
            curtainRoot.SetParent(stage, false);
            var red = Mat("Curtain", new Color(0.55f, 0.05f, 0.08f));
            var cl = Box(curtainRoot, "Curtain_L", new Vector3(-0.31f, 1.16f, 0.27f), new Vector3(0.62f, 0.9f, 0.02f), red);
            var cr = Box(curtainRoot, "Curtain_R", new Vector3(0.31f, 1.16f, 0.27f), new Vector3(0.62f, 0.9f, 0.02f), red);
            var curtain = curtainRoot.gameObject.AddComponent<Gim002Curtain>();
            SetRef(curtain, "left", cl.transform);
            SetRef(curtain, "right", cr.transform);

            // 배경카드 슬롯 (무대 뒤쪽)
            var bgSlotGo = Box(stage, "OBJ_SLOT_BG", new Vector3(0f, 1.15f, -0.28f), new Vector3(0.62f, 0.46f, 0.03f), Mat("SlotFrame", new Color(0.1f, 0.08f, 0.06f)));
            var bgAnchor = new GameObject("Anchor").transform;
            bgAnchor.SetParent(bgSlotGo.transform, false);
            bgAnchor.localPosition = new Vector3(0f, 0f, 0.8f);   // 슬롯 앞면 바로 앞 (부모 스케일 기준)
            bgAnchor.localScale = new Vector3(1f / 0.62f, 1f / 0.46f, 1f / 0.03f);
            var bgSlot = bgSlotGo.AddComponent<Gim002StageSlot>();
            SetEnum(bgSlot, "kind", (int)StageSlotKind.Background);
            SetRef(bgSlot, "anchor", bgAnchor);

            // 인형 꽂이 (무대 바닥 가운데)
            var dollSlotGo = Box(stage, "OBJ_SLOT_DOLL", new Vector3(0f, 0.715f, 0.02f), new Vector3(0.24f, 0.03f, 0.24f), Mat("SlotFrame", new Color(0.1f, 0.08f, 0.06f)));
            var dollAnchor = new GameObject("Anchor").transform;
            dollAnchor.SetParent(dollSlotGo.transform, false);
            dollAnchor.localPosition = new Vector3(0f, 0.5f, 0f);
            dollAnchor.localScale = new Vector3(1f / 0.24f, 1f / 0.03f, 1f / 0.24f);
            var dollSlot = dollSlotGo.AddComponent<Gim002StageSlot>();
            SetEnum(dollSlot, "kind", (int)StageSlotKind.Doll);
            SetRef(dollSlot, "anchor", dollAnchor);

            // 무대 준비 손잡이 (오른쪽 측면)
            var crank = new GameObject("OBJ_CRANK").transform;
            crank.SetParent(stage, false);
            crank.localPosition = new Vector3(0.74f, 0.45f, 0.24f);   // 오른쪽은 장난감 상자와 가까워서 앞쪽 모서리에 둔다
            var crankCol = crank.gameObject.AddComponent<BoxCollider>();
            crankCol.size = new Vector3(0.12f, 0.3f, 0.3f);
            var handle = new GameObject("Handle").transform;
            handle.SetParent(crank, false);
            Prim(PrimitiveType.Cylinder, handle, "Axle", new Vector3(0.03f, 0f, 0f), new Vector3(0.05f, 0.04f, 0.05f), Mat("Brass", new Color(0.75f, 0.6f, 0.2f)), Quaternion.Euler(0f, 0f, 90f));
            Box(handle, "Arm", new Vector3(0.07f, 0.06f, 0f), new Vector3(0.02f, 0.14f, 0.025f), Mat("Brass", new Color(0.75f, 0.6f, 0.2f)));
            Prim(PrimitiveType.Cylinder, handle, "Grip", new Vector3(0.1f, 0.12f, 0f), new Vector3(0.025f, 0.03f, 0.025f), darkWood, Quaternion.Euler(0f, 0f, 90f));
            StripColliders(handle);
            var crankComp = crank.gameObject.AddComponent<Gim002StageCrank>();
            SetRef(crankComp, "handle", handle);

            // 태엽 구멍 (왼쪽 측면, 나비 문양)
            var wind = new GameObject("OBJ_WINDUP_HOLE").transform;
            wind.SetParent(stage, false);
            wind.localPosition = new Vector3(-0.71f, 0.45f, 0.24f);   // 왼쪽은 침대와 가까워서 앞쪽 모서리에 둔다
            Prim(PrimitiveType.Cylinder, wind, "Rim", Vector3.zero, new Vector3(0.12f, 0.01f, 0.12f), Mat("Brass", new Color(0.75f, 0.6f, 0.2f)), Quaternion.Euler(0f, 0f, 90f));
            Prim(PrimitiveType.Cylinder, wind, "Hole", new Vector3(-0.006f, 0f, 0f), new Vector3(0.03f, 0.01f, 0.03f), Mat("Hole", Color.black), Quaternion.Euler(0f, 0f, 90f));
            StripColliders(wind);
            var windCol = wind.gameObject.AddComponent<BoxCollider>();
            windCol.size = new Vector3(0.05f, 0.16f, 0.16f);
            Label(wind, "나비 문양", new Vector3(-0.02f, 0.11f, 0f), Quaternion.LookRotation(Vector3.right), 0.012f);
            var windTarget = wind.gameObject.AddComponent<Gim002KeyTarget>();
            SetEnum(windTarget, "kind", (int)Gim002KeyTarget.TargetKind.StageWindup);
            SetRef(windTarget, "requiredKey", windKey);

            // 반주판 (무대 아래 앞면) + 덮개
            var board = new GameObject("OBJ_SCOREBOARD").transform;
            board.SetParent(stage, false);
            board.localPosition = new Vector3(-0.08f, 0.33f, 0.355f);
            Box(board, "Board", Vector3.zero, new Vector3(0.8f, 0.34f, 0.01f), Mat("Board", new Color(0.92f, 0.88f, 0.75f)));
            var symText = Label(board, "", new Vector3(0f, 0f, 0.008f), Quaternion.LookRotation(Vector3.back), 0.018f, Color.black);
            var hinge = new GameObject("LidHinge").transform;
            hinge.SetParent(board, false);
            hinge.localPosition = new Vector3(0f, 0.18f, 0.02f);
            Box(hinge, "Lid", new Vector3(0f, -0.18f, 0f), new Vector3(0.82f, 0.36f, 0.015f), wood);
            Label(hinge, "반주판", new Vector3(0f, -0.18f, 0.009f), Quaternion.LookRotation(Vector3.back), 0.014f, new Color(0.9f, 0.8f, 0.5f));
            StripColliders(board);
            var sb = board.gameObject.AddComponent<Gim002Scoreboard>();
            SetRef(sb, "lid", hinge);
            SetRef(sb, "symbolText", symText);

            // 진행 표시등 3개 (반주판 옆)
            for (int i = 0; i < 3; i++)
            {
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(progressLamp, stage);
                lamp.name = "OBJ_LAMP_PROGRESS_2F_" + (i + 1);
                lamp.transform.localPosition = new Vector3(0.42f + i * 0.09f, 0.3f, 0.36f);
                lamp.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                SetEnum(lamp.GetComponent<Gim002IndicatorLamp>(), "kind", (int)Gim002IndicatorLamp.LampKind.Progress);
                SetInt(lamp.GetComponent<Gim002IndicatorLamp>(), "progressIndex", i + 1);
            }

            // "반주자" 준비 표시등 (무대 위)
            var playerLamp = (GameObject)PrefabUtility.InstantiatePrefab(readyLamp, stage);
            playerLamp.name = "OBJ_LAMP_PLAYER";
            playerLamp.transform.localPosition = new Vector3(0f, 1.75f, 0.33f);
            SetEnum(playerLamp.GetComponent<Gim002IndicatorLamp>(), "kind", (int)Gim002IndicatorLamp.LampKind.PlayerReady);
            Label(stage, "반주자", new Vector3(0.18f, 1.82f, 0.36f), Quaternion.LookRotation(Vector3.back), 0.014f, new Color(0.9f, 0.8f, 0.5f));

            // 안내 명판
            Label(stage, "이야기는 반주자가 들려줄 거야.", new Vector3(0f, 0.6f, 0.356f), Quaternion.LookRotation(Vector3.back), 0.012f, new Color(0.95f, 0.85f, 0.55f));

            // 무대 조명 (태엽을 감으면 켜짐)
            var lightGo = new GameObject("StageLight");
            lightGo.transform.SetParent(stage, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.3f, 0.9f);
            lightGo.transform.localRotation = Quaternion.Euler(55f, 180f, 0f);
            var lt = lightGo.AddComponent<Light>();
            lt.type = LightType.Spot;
            lt.range = 4f;
            lt.spotAngle = 50f;
            lt.color = new Color(1f, 0.85f, 0.6f);
            lightGo.AddComponent<Gim002StageLight>();

            // 오답 빨간 화면 + 2층 효과음 스피커
            stage.gameObject.AddComponent<Gim002ScreenFlash>();
            var spk = Speaker(stage, "Speaker_2F", new Vector3(0f, 1f, 0.4f), "2층");
            SetBool(spk, "playPianoWrong", false);   // 반주 오답 화음은 1층에서만 (2층은 인형이 고개를 저음)

            // 인형 받침대 + 인형 4개
            var dollRack = new GameObject("OBJ_DOLL_RACK").transform;
            dollRack.SetParent(area, false);
            dollRack.position = DollRackPos;
            Box(dollRack, "Shelf", new Vector3(0f, 0.3f, 0f), new Vector3(1.6f, 0.6f, 0.4f), wood);
            var dolls = new[] { DollId.OwlScope, DollId.OwlBook, DollId.FrogCan, DollId.CatViolin };
            // 함정 부엉이가 정답 부엉이 바로 옆에 있으면 너무 쉬워 보이지 않게, 받침대 순서는 섞어 둔다
            var order = new[] { DollId.FrogCan, DollId.OwlBook, DollId.CatViolin, DollId.OwlScope };
            for (int i = 0; i < order.Length; i++)
            {
                var slotPos = new GameObject("Spot_" + i).transform;
                slotPos.SetParent(dollRack, false);
                slotPos.localPosition = new Vector3(-0.57f + i * 0.38f, 0.6f, 0f);
                BuildDoll(slotPos, order[i]);
            }

            // 배경카드 꽂이 + 카드 5장 (동쪽 벽, 서쪽(-X)을 향함)
            var cardRack = new GameObject("OBJ_CARD_RACK").transform;
            cardRack.SetParent(area, false);
            cardRack.position = CardRackPos;
            cardRack.rotation = Quaternion.Euler(0f, -90f, 0f);    // 앞면(+Z)이 방 안쪽(-X)을 보게
            Box(cardRack, "Rack", new Vector3(0f, 0.45f, -0.08f), new Vector3(2.9f, 0.9f, 0.1f), wood);
            var cards = new[] { BackgroundId.Castle, BackgroundId.NightForest, BackgroundId.Sea, BackgroundId.MusicRoom, BackgroundId.Greenhouse };
            for (int i = 0; i < cards.Length; i++)
            {
                var slotPos = new GameObject("Spot_" + i).transform;
                slotPos.SetParent(cardRack, false);
                slotPos.localPosition = new Vector3(-1.16f + i * 0.58f, 1.1f, 0f);
                BuildCard(slotPos, cards[i]);
            }
        }

        // 인형 하나 (임시 도형). 부엉이 두 개는 얼굴(몸통·색)이 같고 소품만 다르다(망원경 vs 책) — 기획 의도된 함정.
        static void BuildDoll(Transform spot, DollId id)
        {
            var go = new GameObject("DOLL_" + id);
            go.transform.SetParent(spot, false);
            var body = new GameObject("Body").transform;    // Gim002Puppet이 흔드는 부분
            body.SetParent(go.transform, false);

            Color c = id == DollId.FrogCan ? new Color(0.3f, 0.65f, 0.25f) : id == DollId.CatViolin ? new Color(0.55f, 0.55f, 0.6f) : new Color(0.5f, 0.33f, 0.18f);
            var m = Mat("Doll_" + c.GetHashCode(), c);
            Prim(PrimitiveType.Capsule, body, "Torso", new Vector3(0f, 0.11f, 0f), new Vector3(0.14f, 0.1f, 0.12f), m);
            Prim(PrimitiveType.Sphere, body, "Head", new Vector3(0f, 0.26f, 0f), Vector3.one * 0.13f, m);
            var eye = Mat("Eye", Color.white);
            Prim(PrimitiveType.Sphere, body, "Eye_L", new Vector3(-0.03f, 0.28f, 0.055f), Vector3.one * 0.03f, eye);
            Prim(PrimitiveType.Sphere, body, "Eye_R", new Vector3(0.03f, 0.28f, 0.055f), Vector3.one * 0.03f, eye);

            var prop = Mat("Prop_" + id, id == DollId.OwlBook ? new Color(0.2f, 0.3f, 0.7f) : new Color(0.75f, 0.6f, 0.2f));
            switch (id)
            {
                case DollId.OwlScope:
                    Prim(PrimitiveType.Cylinder, body, "Telescope", new Vector3(0.07f, 0.28f, 0.08f), new Vector3(0.03f, 0.07f, 0.03f), prop, Quaternion.Euler(70f, 0f, 0f)); break;
                case DollId.OwlBook:
                    Box(body, "Book", new Vector3(0f, 0.13f, 0.08f), new Vector3(0.1f, 0.12f, 0.03f), prop); break;
                case DollId.FrogCan:
                    Prim(PrimitiveType.Cylinder, body, "WateringCan", new Vector3(0.09f, 0.12f, 0.04f), new Vector3(0.06f, 0.04f, 0.06f), prop);
                    Prim(PrimitiveType.Cylinder, body, "Spout", new Vector3(0.14f, 0.15f, 0.04f), new Vector3(0.012f, 0.04f, 0.012f), prop, Quaternion.Euler(0f, 0f, -50f)); break;
                case DollId.CatViolin:
                    Box(body, "Violin", new Vector3(0.07f, 0.17f, 0.06f), new Vector3(0.05f, 0.13f, 0.025f), Mat("Violin", new Color(0.55f, 0.25f, 0.08f)));
                    Box(body, "Bow", new Vector3(-0.06f, 0.17f, 0.07f), new Vector3(0.006f, 0.16f, 0.006f), prop); break;
            }
            StripColliders(go.transform);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.17f, 0f);
            col.size = new Vector3(0.22f, 0.36f, 0.2f);

            var item = go.AddComponent<Gim002StageItem>();
            SetEnum(item, "kind", (int)StageSlotKind.Doll);
            SetEnum(item, "doll", (int)id);
            SetVector(item, "holdOffset", new Vector3(-0.25f, -0.35f, 0.55f));
            go.AddComponent<Gim002Puppet>();
        }

        // 배경카드 한 장 (임시: 색 + 글자). 앞면 = +Z.
        static void BuildCard(Transform spot, BackgroundId id)
        {
            var go = new GameObject("BG_" + id);
            go.transform.SetParent(spot, false);
            Color c;
            string desc;
            switch (id)
            {
                case BackgroundId.NightForest: c = new Color(0.08f, 0.15f, 0.3f); desc = "별이 뜬\n나무숲"; break;
                case BackgroundId.Greenhouse: c = new Color(0.55f, 0.8f, 0.6f); desc = "유리 지붕 아래\n화분들"; break;
                case BackgroundId.MusicRoom: c = new Color(0.6f, 0.35f, 0.2f); desc = "피아노를\n크게 그린 방"; break;
                case BackgroundId.Sea: c = new Color(0.2f, 0.45f, 0.75f); desc = "파도와\n등대"; break;
                default: c = new Color(0.55f, 0.55f, 0.5f); desc = "탑이 있는\n성"; break;
            }
            Box(go.transform, "Card", Vector3.zero, new Vector3(0.52f, 0.38f, 0.015f), Mat("Card_" + id, c));
            Label(go.transform, desc, new Vector3(0f, 0f, 0.009f), Quaternion.LookRotation(Vector3.back), 0.014f, Color.white);
            StripColliders(go.transform);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.52f, 0.38f, 0.03f);

            var item = go.AddComponent<Gim002StageItem>();
            SetEnum(item, "kind", (int)StageSlotKind.Background);
            SetEnum(item, "background", (int)id);
            SetVector(item, "holdOffset", new Vector3(-0.3f, -0.25f, 0.6f));
        }

        // =====================================================================
        // 1층 음악실
        // =====================================================================

        static void BuildMusicRoom(Transform root, GameObject readyLamp, GameObject progressLamp)
        {
            var area = new GameObject("F1_MusicRoom").transform;
            area.SetParent(root, false);

            // 피아노 기호 건반: 기존 Piano_Keys 위에 흰 건반 7개. 플레이어는 +X쪽(의자)에서 -X를 보고 앉으므로 "왼쪽" = -Z쪽.
            var piano = new GameObject("OBJ_PIANO_KEYS").transform;
            piano.SetParent(area, false);
            float[] freqs = { 261.6f, 293.7f, 329.6f, 349.2f, 392f, 440f, 493.9f };   // 도 레 미 파 솔 라 시
            var symbols = new[] { SymbolId.Sun, SymbolId.Cloud, SymbolId.Moon, SymbolId.Flower, SymbolId.Star };
            string[] symbolNames = { "해", "구름", "달", "꽃", "별" };
            for (int i = 0; i < 7; i++)
            {
                var key = Box(piano, "Key_" + (i + 1), new Vector3(PianoKeysX, PianoKeysTop + 0.015f, PianoKeysZMin + 0.1f + i * 0.2f), new Vector3(0.22f, 0.03f, 0.185f), Mat("WhiteKey", new Color(0.95f, 0.95f, 0.92f)));
                var pk = key.AddComponent<Gim002PianoKey>();
                bool has = i < symbols.Length;
                SetBool(pk, "hasSymbol", has);
                if (has) SetEnum(pk, "symbol", (int)symbols[i]);
                SetFloat(pk, "toneFrequency", freqs[i]);
                if (has)
                {
                    // 건반 윗면에 누워 있는 기호 스티커 (플레이어 쪽에서 바로 읽히도록 회전)
                    var lbl = Label(piano, symbolNames[i], key.transform.position + new Vector3(0.03f, 0.017f, 0f), Quaternion.LookRotation(Vector3.down, Vector3.left), 0.016f, new Color(0.15f, 0.1f, 0.4f));
                    lbl.transform.SetParent(key.transform, true);
                }
            }

            // "무대" 준비 표시등 + 진행 표시등 3개 (피아노 위)
            var lamps = new GameObject("PianoLamps").transform;
            lamps.SetParent(area, false);
            var stageLamp = (GameObject)PrefabUtility.InstantiatePrefab(readyLamp, lamps);
            stageLamp.name = "OBJ_LAMP_STAGE";
            stageLamp.transform.position = new Vector3(38.7f, 1.0f, 3.85f);
            stageLamp.transform.rotation = Quaternion.LookRotation(Vector3.right);
            SetEnum(stageLamp.GetComponent<Gim002IndicatorLamp>(), "kind", (int)Gim002IndicatorLamp.LampKind.StageReady);
            SetBool(stageLamp.GetComponent<Gim002IndicatorLamp>(), "blinkOnStageWrong", true);
            Label(lamps, "무대", new Vector3(38.72f, 1.2f, 3.85f), Quaternion.LookRotation(Vector3.left), 0.014f, new Color(0.95f, 0.85f, 0.55f));
            for (int i = 0; i < 3; i++)
            {
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(progressLamp, lamps);
                lamp.name = "OBJ_LAMP_PROGRESS_1F_" + (i + 1);
                lamp.transform.position = new Vector3(38.7f, 1.0f, 4.25f + i * 0.14f);
                SetEnum(lamp.GetComponent<Gim002IndicatorLamp>(), "kind", (int)Gim002IndicatorLamp.LampKind.Progress);
                SetInt(lamp.GetComponent<Gim002IndicatorLamp>(), "progressIndex", i + 1);
            }

            // 1층 효과음 스피커 (피아노에서 재생)
            var spk = Speaker(area, "Speaker_1F", new Vector3(38.2f, 1.2f, 4.3f), "1층");
            SetBool(spk, "playStageRight", false);   // 무대 정답은 1층에 변화 없음 (2층이 말로 알려 줌)

            // 놀이책 (레코드장 위)
            // 놀이책: 빈 루트(들기 대상) + 표지 도형 + 표지 글자. 루트 스케일을 1로 두어야 글자 크기가 찌그러지지 않는다.
            var book = new GameObject("OBJ_PLAYBOOK");
            book.transform.SetParent(area, false);
            book.transform.position = BookPos;
            Box(book.transform, "Cover", new Vector3(0f, 0.025f, 0f), new Vector3(0.22f, 0.05f, 0.3f), Mat("Book", new Color(0.55f, 0.15f, 0.15f)));
            StripColliders(book.transform);
            var bookCol = book.AddComponent<BoxCollider>();
            bookCol.center = new Vector3(0f, 0.025f, 0f);
            bookCol.size = new Vector3(0.24f, 0.06f, 0.32f);
            Label(book.transform, "인형극\n놀이책", new Vector3(0f, 0.052f, 0f), Quaternion.LookRotation(Vector3.down, Vector3.left), 0.014f, new Color(1f, 0.9f, 0.6f));
            var pb = book.AddComponent<Gim002PlayBook>();
            SetString(pb, "displayName", "인형극 놀이책");
            SetVector(pb, "holdOffset", new Vector3(-0.25f, -0.3f, 0.55f));
            SetVector(pb, "holdEuler", new Vector3(-60f, 0f, 0f));
        }

        // =====================================================================
        // 기존 오브젝트 연결 (음악실 문, GIM-001 보상 열쇠)
        // =====================================================================

        static void HookExisting(Gim002KeyData musicKey, Gim002KeyData windKey)
        {
            foreach (var doorName in new[] { "Door_Hall_Music", "Door_Music_Smoking" })
            {
                var door = Object.FindObjectsByType<LGH.InteractableDoor>(FindObjectsInactive.Include).FirstOrDefault(d => d.name == doorName);
                if (door == null) { Debug.LogWarning("[GIM-002] 문을 찾지 못함: " + doorName); continue; }
                var t = door.GetComponent<Gim002KeyTarget>();
                if (t == null) t = Undo.AddComponent<Gim002KeyTarget>(door.gameObject);
                SetEnum(t, "kind", (int)Gim002KeyTarget.TargetKind.MusicDoor);
                SetRef(t, "requiredKey", musicKey);
                SetRef(t, "door", door);
            }

            HookKey("Key_Behind_E3", musicKey, Gim002KeyTarget.TargetKind.MusicDoor);
            HookKey("Key_Behind_2F", windKey, Gim002KeyTarget.TargetKind.StageWindup);
        }

        static void HookKey(string name, Gim002KeyData data, Gim002KeyTarget.TargetKind usedFor)
        {
            var t = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(x => x.name == name);
            if (t == null) { Debug.LogWarning("[GIM-002] 열쇠를 찾지 못함: " + name); return; }
            if (t.GetComponent<Collider>() == null)
            {
                // 열쇠 모형 전체를 덮는 콜라이더 (작아서 조준이 어려우니 최소 크기를 둔다)
                var rs = t.GetComponentsInChildren<Renderer>();
                var b = new Bounds(t.position, Vector3.one * 0.05f);
                foreach (var r in rs) b.Encapsulate(r.bounds);
                var col = Undo.AddComponent<BoxCollider>(t.gameObject);
                col.center = t.InverseTransformPoint(b.center);
                Vector3 size = t.InverseTransformVector(b.size);
                col.size = Vector3.Max(new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)), Vector3.one * 0.12f);
            }
            // LGH 공용 소지품 인벤토리로 줍는다 — WorldItem이 "보기+E로 줍기"를 전담한다 (RequireComponent로도 강제됨).
            var world = t.GetComponent<LGH.WorldItem>();
            if (world == null) world = Undo.AddComponent<LGH.WorldItem>(t.gameObject);
            if (data != null && data.inventoryItem != null) SetRef(world, "item", data.inventoryItem);

            var item = t.GetComponent<Gim002KeyItem>();
            if (item == null) item = Undo.AddComponent<Gim002KeyItem>(t.gameObject);
            SetRef(item, "keyData", data);
            SetEnum(item, "usedFor", (int)usedFor);
        }

        // =====================================================================
        // 도우미
        // =====================================================================

        static Gim002FeedbackSpeaker Speaker(Transform parent, string name, Vector3 pos, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (parent.name == "F1_MusicRoom") go.transform.position = pos; else go.transform.localPosition = pos;
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.minDistance = 2f;
            src.maxDistance = 14f;
            src.rolloffMode = AudioRolloffMode.Linear;
            var spk = go.AddComponent<Gim002FeedbackSpeaker>();
            SetString(spk, "label", label);
            return spk;
        }

        static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material m)
        {
            return Prim(PrimitiveType.Cube, parent, name, localPos, size, m);
        }

        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material m, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        // 3D 글자 (임시 라벨). size = 글자 한 칸의 대략적인 높이(m)
        static TextMesh Label(Transform parent, string text, Vector3 localPos, Quaternion localRot, float size, Color? color = null)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            // 부모 스케일 영향을 없애서 크기를 m 단위로 맞춘다
            Vector3 ls = parent.lossyScale;
            go.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, ls.x), 1f / Mathf.Max(0.0001f, ls.y), 1f / Mathf.Max(0.0001f, ls.z));
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = size * LabelScale / (tm.fontSize * 0.1f);   // TextMesh 글자 높이 ≈ fontSize × characterSize × 0.1
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color ?? Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }

        static void StripColliders(Transform t)
        {
            foreach (var c in t.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }

        // URP Lit 머티리얼 (없으면 만들고, 있으면 색만 맞춤)
        static Material Mat(string name, Color color, bool emissive = false)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            string path = MatDir + "/Gim002_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.color = color;
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

        static IEnumerable<GameObject> SceneRoots() { return SceneManagementScene().GetRootGameObjects(); }

        // SerializedObject로 private [SerializeField] 값 넣기 (Undo/저장이 정상 동작하도록)
        static SerializedObject So(Object o) { return new SerializedObject(o); }
        static void SetRef(Object o, string field, Object value) { var s = So(o); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetEnum(Object o, string field, int value) { var s = So(o); s.FindProperty(field).enumValueIndex = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetInt(Object o, string field, int value) { var s = So(o); s.FindProperty(field).intValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetBool(Object o, string field, bool value) { var s = So(o); s.FindProperty(field).boolValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetFloat(Object o, string field, float value) { var s = So(o); s.FindProperty(field).floatValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetString(Object o, string field, string value) { var s = So(o); s.FindProperty(field).stringValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetVector(Object o, string field, Vector3 value) { var s = So(o); s.FindProperty(field).vector3Value = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
