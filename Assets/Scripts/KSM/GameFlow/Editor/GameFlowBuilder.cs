using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CoopDemo.EditorTools
{
    // 로비->도입부->플레이->엔딩->결과 흐름의 Canvas/네트워크 오브젝트를 자동 배치하는 도구. Unity 메뉴 Tools/KSM/GameFlow/Build All.
    // 몇 번을 다시 실행해도 된다 — 이전에 만든 GameFlow 관련 오브젝트를 지우고 새로 만든다(프로젝트의 다른 Build All 도구들과 같은 방식).
    // 아직 실제 일러스트가 없어서 전부 유니티 기본 UI(단색 Image + Text)로 임시 제작한다 — 나중에 이미지만 갈아끼우면 된다.
    public static class GameFlowBuilder
    {
        const string PrefabRoot = "Assets/Prefabs/KSM/GameFlow";
        static Font font;

        [MenuItem("Tools/KSM/GameFlow/Build All")]
        public static void BuildAll()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/KSM");
            EnsureFolder(PrefabRoot);

            // 이전 결과 지우기 (씬 루트 기준)
            foreach (var go in SceneRoots().Where(g => g.name == "LobbyCanvas" || g.name == "IntroCanvas" || g.name == "EndingCanvas"
                        || g.name == "ResultCanvas" || g.name == "GameFlowUI" || g.name == "GameFlowSync").ToList())
                Object.DestroyImmediate(go);

            var syncPrefab = BuildSyncPrefab();
            var syncGo = (GameObject)PrefabUtility.InstantiatePrefab(syncPrefab, ActiveScene());
            syncGo.name = "GameFlowSync";
            RegisterNetworkPrefab(syncPrefab);

            var lobbyGo = BuildLobbyCanvas();
            var introGo = BuildCutsceneCanvas("IntroCanvas", IllustratedCutsceneController.Kind.Intro, DefaultIntroCuts());
            var endingGo = BuildCutsceneCanvas("EndingCanvas", IllustratedCutsceneController.Kind.Ending, DefaultEndingCuts());
            var resultGo = BuildResultCanvas();

            // 전부 처음엔 꺼져 있다 — GameFlowUI가 단계에 맞춰 하나만 켠다.
            lobbyGo.SetActive(false);
            introGo.SetActive(false);
            endingGo.SetActive(false);
            resultGo.SetActive(false);

            var uiGo = new GameObject("GameFlowUI");
            var ui = uiGo.AddComponent<GameFlowUI>();
            SetRef(ui, "lobbyCanvas", lobbyGo);
            SetRef(ui, "introCanvas", introGo);
            SetRef(ui, "endingCanvas", endingGo);
            SetRef(ui, "resultCanvas", resultGo);

            EditorSceneManager.MarkSceneDirty(ActiveScene());
            Selection.activeObject = uiGo;
            Debug.Log("[GameFlow] Build All 완료. 씬을 저장(Ctrl+S)하세요. 테스트는 Playing 상태에서 키보드로 'ESCAPE'를 순서대로 입력하면 엔딩으로 넘어갑니다(임시).");
        }

        static IllustratedCutsceneController.Cut[] DefaultIntroCuts()
        {
            return new[]
            {
                NewCut("오랜 친구에게서 초대장이 도착했다.\n낡은 저택, 레이븐우드.", 2f),
                NewCut("\"하룻밤만 묵어가도 괜찮다면,\n저택을 구경시켜드리죠.\"", 2f),
                NewCut("문을 들어서자마자 등 뒤에서\n묵직한 소리와 함께 문이 잠겼다.", 2f),
                NewCut("저택 안의 모든 것이 심상치 않다.\n두 사람은 함께 출구를 찾기로 했다.", 2f),
            };
        }

        static IllustratedCutsceneController.Cut[] DefaultEndingCuts()
        {
            return new[]
            {
                NewCut("마지막 문이 열리고, 차가운 밤공기가 밀려들어왔다.", 2f),
                NewCut("두 사람은 뒤를 돌아보지 않고\n저택을 빠져나왔다.", 2f),
            };
        }

        static IllustratedCutsceneController.Cut NewCut(string caption, float minSeconds)
        {
            return new IllustratedCutsceneController.Cut { image = null, caption = caption, minSeconds = minSeconds };
        }

        // =====================================================================
        // GameFlowSync 네트워크 프리팹
        // =====================================================================

        static GameObject BuildSyncPrefab()
        {
            string path = PrefabRoot + "/GameFlowSync.prefab";
            var temp = new GameObject("GameFlowSync");
            temp.AddComponent<NetworkObject>();
            temp.AddComponent<GameFlowSync>();
            temp.AddComponent<DebugEscapeTrigger>();   // 임시: 진짜 탈출 기믹이 생기면 이 줄만 지우면 된다.
            bool ok;
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path, out ok);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        static void RegisterNetworkPrefab(GameObject prefab)
        {
            var nm = Object.FindAnyObjectByType<NetworkManager>(FindObjectsInactive.Include);
            if (nm == null || nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Count == 0)
            {
                Debug.LogWarning("[GameFlow] NetworkManager의 네트워크 프리팹 목록을 찾지 못했습니다. GameFlowSync.prefab을 직접 등록해 주세요.");
                return;
            }
            var list = nm.NetworkConfig.Prefabs.NetworkPrefabsLists[0];
            if (list.PrefabList.Any(p => p.Prefab == prefab)) return;
            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
        }

        // =====================================================================
        // 대기방 배경 Canvas — 저택(플레이 화면)이 뒤로 비치지 않게 가리는 불투명 배경만 둔다.
        // 실제 대기방 내용(인원/준비 버튼 등)은 MultiplayerMenu의 기존 OnGUI 패널이 이 위에 그대로 그린다.
        // =====================================================================

        static GameObject BuildLobbyCanvas()
        {
            var canvasGo = CreateOverlayCanvas("LobbyCanvas", sortOrder: 10);
            // 다른 GameFlow Canvas(0.97)와 달리 완전 불투명(1.0) — 요구사항이 "연출용으로 살짝 어둡게"가 아니라
            // "저택(플레이 화면)이 전혀 안 보이게" 가리는 것이므로, 밝은 광원이 살짝 비쳐 보이는 것도 허용하지 않는다.
            CreateFullStretchImage((RectTransform)canvasGo.transform, "Background", new Color(0.04f, 0.04f, 0.06f, 1f));
            return canvasGo;
        }

        // =====================================================================
        // 도입부/엔딩 공용 Canvas
        // =====================================================================

        static GameObject BuildCutsceneCanvas(string name, IllustratedCutsceneController.Kind kind, IllustratedCutsceneController.Cut[] cuts)
        {
            var canvasGo = CreateOverlayCanvas(name, sortOrder: 10);
            var canvasRect = (RectTransform)canvasGo.transform;

            CreateFullStretchImage(canvasRect, "Background", new Color(0.04f, 0.04f, 0.06f, 0.97f));

            var playback = new GameObject("PlaybackRoot", typeof(RectTransform));
            SetupStretch((RectTransform)playback.transform, canvasRect);

            var illustration = CreateImage(playback.transform, "Illustration", new Vector2(0, 120), new Vector2(900, 460), new Color(0.22f, 0.22f, 0.28f, 1f));
            var caption = CreateText(playback.transform, "Caption", "", 30, TextAnchor.MiddleCenter,
                new Color(0.95f, 0.95f, 0.9f), new Vector2(0, -220), new Vector2(1400, 160));
            var nextBtn = CreateButton(playback.transform, "NextButton", "다음", new Vector2(240, -380), new Vector2(220, 64));
            var skipBtn = CreateButton(playback.transform, "SkipButton", "스킵", new Vector2(500, -380), new Vector2(220, 64));

            var waiting = new GameObject("WaitingRoot", typeof(RectTransform));
            SetupStretch((RectTransform)waiting.transform, canvasRect);
            var waitingText = CreateText(waiting.transform, "WaitingText", "상대 플레이어를 기다리는 중...", 34, TextAnchor.MiddleCenter,
                Color.white, Vector2.zero, new Vector2(1200, 100));
            waiting.SetActive(false);

            var controller = canvasGo.AddComponent<IllustratedCutsceneController>();
            SetEnum(controller, "kind", (int)kind);
            SetRef(controller, "illustration", illustration.GetComponent<Image>());
            SetRef(controller, "caption", caption.GetComponent<Text>());
            SetRef(controller, "nextButton", nextBtn.GetComponent<Button>());
            SetRef(controller, "skipButton", skipBtn.GetComponent<Button>());
            SetRef(controller, "playbackRoot", playback);
            SetRef(controller, "waitingRoot", waiting);
            SetRef(controller, "waitingText", waitingText.GetComponent<Text>());
            controller.cuts = cuts;   // public 배열 필드 — SerializedObject 없이 바로 대입 가능

            return canvasGo;
        }

        // =====================================================================
        // 결과 Canvas
        // =====================================================================

        static GameObject BuildResultCanvas()
        {
            var canvasGo = CreateOverlayCanvas("ResultCanvas", sortOrder: 10);
            var canvasRect = (RectTransform)canvasGo.transform;

            CreateFullStretchImage(canvasRect, "Background", new Color(0.04f, 0.04f, 0.06f, 0.97f));

            CreateText(canvasRect, "TitleText", "MEET YOU", 64, TextAnchor.MiddleCenter, Color.white, new Vector2(0, 300), new Vector2(1000, 90));
            CreateText(canvasRect, "SubtitleText", "[탈출 성공]", 36, TextAnchor.MiddleCenter, new Color(0.9f, 0.85f, 0.6f), new Vector2(0, 220), new Vector2(1000, 60));
            CreateText(canvasRect, "DescriptionText", "마침내 두 사람은 저택을 빠져나왔다.", 24, TextAnchor.MiddleCenter, Color.white, new Vector2(0, 120), new Vector2(1200, 60));
            CreateText(canvasRect, "PlayerCountText", "탈출 인원 2명", 24, TextAnchor.MiddleCenter, Color.white, new Vector2(0, 40), new Vector2(800, 50));
            var clearTime = CreateText(canvasRect, "ClearTimeText", "클리어 시간 --:--:--", 24, TextAnchor.MiddleCenter, Color.white, new Vector2(0, -20), new Vector2(800, 50));
            var titleBtn = CreateButton(canvasRect, "TitleButton", "타이틀로 돌아가기", new Vector2(0, -160), new Vector2(360, 70));

            var controller = canvasGo.AddComponent<ResultCanvasController>();
            SetRef(controller, "clearTimeText", clearTime.GetComponent<Text>());
            SetRef(controller, "titleButton", titleBtn.GetComponent<Button>());

            return canvasGo;
        }

        // =====================================================================
        // uGUI 생성 헬퍼 (TitleCanvas와 같은 CanvasScaler 설정을 맞춘다)
        // =====================================================================

        static GameObject CreateOverlayCanvas(string name, int sortOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return go;
        }

        static void SetupStretch(RectTransform rt, RectTransform parent)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static GameObject CreateFullStretchImage(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            SetupStretch((RectTransform)go.transform, parent);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static GameObject CreateImage(Transform parent, string name, Vector2 anchoredPos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        static GameObject CreateText(Transform parent, string name, string text, int fontSize, TextAnchor anchor,
            Color color, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.alignment = anchor; t.color = color;
            t.text = text; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return go;
        }

        static GameObject CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.24f, 1f);

            var labelGo = CreateText(go.transform, "Label", label, 24, TextAnchor.MiddleCenter, Color.white, Vector2.zero, size);
            ((RectTransform)labelGo.transform).anchorMin = Vector2.zero;
            ((RectTransform)labelGo.transform).anchorMax = Vector2.one;
            ((RectTransform)labelGo.transform).offsetMin = Vector2.zero;
            ((RectTransform)labelGo.transform).offsetMax = Vector2.zero;
            return go;
        }

        // =====================================================================
        // 공용 유틸 (프로젝트의 다른 Build All 도구들과 동일한 패턴)
        // =====================================================================

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static UnityEngine.SceneManagement.Scene ActiveScene() { return UnityEngine.SceneManagement.SceneManager.GetActiveScene(); }
        static System.Collections.Generic.IEnumerable<GameObject> SceneRoots() { return ActiveScene().GetRootGameObjects(); }

        static SerializedObject So(Object o) { return new SerializedObject(o); }
        static void SetRef(Object o, string field, Object value) { var s = So(o); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetEnum(Object o, string field, int value) { var s = So(o); s.FindProperty(field).enumValueIndex = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
