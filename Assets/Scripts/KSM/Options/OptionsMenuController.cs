using System.Collections.Generic;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CoopDemo
{
    // 옵션 화면. 타이틀/대기실/인게임 어디서든 OptionsMenuController.Open()으로 연다.
    // - 네트워크 세션, 음성 채널, 상대 플레이어는 절대 건드리지 않는다 (로컬 전용).
    // - Time.timeScale은 쓰지 않는다. 인게임에서는 커서만 풀고 LGH.FirstPersonController를 IPlayerLock으로 잠깐 잠근다.
    // - 슬라이더는 움직이는 즉시 미리보기가 적용된다(PlayerOptions.Preview). [적용 및 저장]을 눌러야 재실행 후에도 유지된다.
    // 디자이너 이미지 버전 화면은 OptionsMenuController.Designed.cs (Resources/OptionsDesignTheme이 있으면 그쪽을 그린다)
    public partial class OptionsMenuController : MonoBehaviour
    {
        public static OptionsMenuController Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        PlayerOptionsData staged;
        int tab;
        Vector2 tabScroll;
        float dragStartY;
        bool dragTracking, dragActive;
        LGH.FirstPersonController lockedController;

        // 해상도 변경 "되돌리기 타이머": 새 해상도를 고르면 바로 적용해보고, 확인을 안 누르면 자동으로 되돌린다.
        const float ResolutionConfirmSeconds = 15f;
        bool resolutionPending;
        float resolutionRevertAt;
        int resolutionPrevWidth, resolutionPrevHeight;
        List<(int w, int h)> resolutionOptions;

        static readonly Color[] CrosshairPresets =
        {
            Color.white, new Color(1f, 0.25f, 0.2f), new Color(0.3f, 1f, 0.3f), new Color(1f, 0.9f, 0.2f), new Color(0.3f, 0.85f, 1f)
        };
        static readonly string[] CrosshairPresetNames = { "흰색", "빨강", "초록", "노랑", "하늘" };

        [Tooltip("조준점 모양 후보들. LGH.PlayerInteractor.crosshairOptions와 반드시 같은 순서로 넣을 것 — " +
                 "여기 인덱스를 그대로 PlayerOptionsData.crosshairIndex로 저장해서 플레이어 쪽에 적용한다.")]
        [SerializeField] Texture2D[] crosshairTextures;
        const float CrosshairBaseSize = 32f;

        GUIStyle title, tabBtn, tabBtnActive, label, small, box, button, sliderLabel, toggleStyle, warnStyle;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            IsOpen = false;
            DontDestroyOnLoad(gameObject);
        }

        // 도메인 리로드를 끄고 Play하면 정적 값이 이전 실행 것 그대로 남는다.
        // 옵션을 연 채로 Play를 끝내면 다음 실행이 옵션 창이 열린 채로 시작하므로 매번 초기화한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsOpen = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Open()
        {
            if (Instance == null) return;
            PlayerOptions.EnsureLoaded();
            Instance.staged = PlayerOptions.Current;
            // [M]키로 바꾼 음소거 상태는 옵션 값에 반영되지 않으므로, 열 때 실제 마이크 상태로 맞춘다
            if (VoiceChatManager.Instance != null && VoiceChatManager.Instance.IsReady)
                Instance.staged.micMuted = VoiceChatManager.Instance.IsMicMuted;
            Instance.resolutionPending = false;
            Instance.openDropdown = -1;
            IsOpen = true;
            Instance.LockLocalPlayer(true);
            Instance.SetBackgroundUiBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // data: true면 저장된 값으로 되돌리면서 닫기(취소), false면 지금 미리보기 값 그대로 두고 닫기(닫기 버튼)
        public static void Close(bool revert)
        {
            if (Instance == null) return;
            Instance.resolutionPending = false;
            if (revert) PlayerOptions.RevertToSaved();   // OptionsManager.Apply()가 해상도까지 포함해서 되돌려준다.
            IsOpen = false;
            Instance.LockLocalPlayer(false);
            Instance.SetBackgroundUiBlocked(false);
            // 인게임이면 다시 잠그지 않는다 — 일시정지 패널로 돌아가서 "계속하기"를 누를 때 잠그는 게 자연스럽다.
        }

        // 옵션 창은 OnGUI(IMGUI)로 그려지는데, 타이틀 화면은 uGUI(Canvas+EventSystem)라서 서로 입력 체계가 다르다.
        // 그냥 두면 옵션 창 뒤에 깔린 "시작/옵션/나가기" 같은 uGUI 버튼이 옵션 창 너머로 그대로 클릭된다.
        // EventSystem을 꺼서 uGUI 쪽 클릭 처리를 통째로 멈춘다 — OnGUI는 EventSystem과 무관하게 동작하므로 옵션 자체는 영향 없다.
        EventSystem blockedEventSystem;

        void SetBackgroundUiBlocked(bool blocked)
        {
            if (blocked)
            {
                blockedEventSystem = EventSystem.current != null ? EventSystem.current : FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
                if (blockedEventSystem != null) blockedEventSystem.enabled = false;
            }
            else if (blockedEventSystem != null)
            {
                blockedEventSystem.enabled = true;
                blockedEventSystem = null;
            }
        }

        void LockLocalPlayer(bool locked)
        {
            var local = LGH.NetworkPlayer.Local;
            var fpc = local != null ? local.GetComponent<LGH.FirstPersonController>() : null;
            if (locked)
            {
                lockedController = fpc;
                if (fpc != null) { fpc.SpeedMultiplier = 0f; fpc.LookEnabled = false; }
            }
            else if (lockedController != null)
            {
                lockedController.SpeedMultiplier = 1f;
                lockedController.LookEnabled = true;
                lockedController = null;
            }
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            // 인벤토리 등 상위 모달이 이번 프레임에 Esc를 이미 썼으면 옵션은 반응하지 않는다 (우선순위: 상위 모달 > 옵션 > 일반 인게임).
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !LGH.PlayerInventory.EscapeConsumed)
            {
                if (openDropdown >= 0) openDropdown = -1;   // 펼친 장치 목록이 있으면 그것만 접는다
                else Close(false);
            }

            if (resolutionPending && Time.unscaledTime >= resolutionRevertAt) RevertResolution();
        }

        // ---------------- GUI ----------------

        int styleVersion = -1;

        void Styles()
        {
            // 스킨이 새로 만들어졌으면(예: 새 Play 실행) 이전 스타일은 파괴된 텍스처를 가리키므로 다시 만든다
            if (title != null && styleVersion == MeetYouGuiSkin.Version) return;
            styleVersion = MeetYouGuiSkin.Version;
            title = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            tabBtn = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            tabBtnActive = new GUIStyle(tabBtn);
            tabBtnActive.normal.textColor = Color.white;
            tabBtnActive.fontStyle = FontStyle.Bold;
            // 선택된 탭/장치는 "눌린" 금색 모양으로 보여준다 (스킨이 없으면 기본 모양 그대로)
            if (tabBtnActive.onNormal.background != null)
            {
                tabBtnActive.normal.background = tabBtnActive.onNormal.background;
                tabBtnActive.hover.background = tabBtnActive.onNormal.background;
            }
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            box = new GUIStyle(GUI.skin.box);
            button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            sliderLabel = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleRight };
            toggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = 16 };
            warnStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            warnStyle.normal.textColor = new Color(1f, 0.75f, 0.4f);
        }

        void OnGUI()
        {
            if (!IsOpen) return;
            if (DesignTheme != null)
            {
                var m = GUI.matrix;
                try { DrawDesigned(); }
                finally { GUI.matrix = m; }
                return;
            }
            var prevSkin = MeetYouGuiSkin.Begin();
            try { DrawGui(); }
            finally { MeetYouGuiSkin.End(prevSkin); }
        }

        void DrawGui()
        {
            Styles();
            var v = LGH.UiScale.Apply();
            float w = v.x, h = v.y;

            float pw = Mathf.Min(620f, w - 60f), ph = Mathf.Min(560f, h - 60f);
            var r = new Rect((w - pw) / 2f, (h - ph) / 2f, pw, ph);
            GUI.Box(r, GUIContent.none, MeetYouGuiSkin.Window);
            GUILayout.BeginArea(new Rect(r.x + 20f, r.y + 14f, pw - 40f, ph - 28f));

            GUILayout.Label("옵션", title, GUILayout.Height(40));

            GUILayout.BeginHorizontal();
            DrawTab(0, "오디오");
            DrawTab(1, "그래픽");
            DrawTab(2, "조작");
            DrawTab(3, "게임");
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            GUILayout.BeginVertical(box, GUILayout.ExpandHeight(true));
            // 장치가 많아 내용이 길어질 수 있으므로 스크롤 영역으로 감싼다 (휠, 스크롤바, 빈 곳 드래그).
            tabScroll = GUILayout.BeginScrollView(tabScroll, false, false, GUILayout.ExpandHeight(true));
            switch (tab)
            {
                case 0: DrawAudioTab(); break;
                case 1: DrawGraphicsTab(); break;
                case 2: DrawControlsTab(); break;
                case 3: DrawGameTab(); break;
            }
            GUILayout.EndScrollView();
            HandleDragScroll(GUILayoutUtility.GetLastRect());
            GUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("기본값", button, GUILayout.Height(40))) ApplyPreview(PlayerOptionsData.Default());
            if (GUILayout.Button("취소", button, GUILayout.Height(40))) Close(true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("적용 및 저장", button, GUILayout.Height(40), GUILayout.Width(150))) PlayerOptions.Save(staged);
            if (GUILayout.Button("닫기", button, GUILayout.Height(40), GUILayout.Width(100))) Close(false);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        // 화면을 마우스로 끌어 내리는 스크롤. 버튼 위에서 시작한 드래그도 스크롤로 취급하되, 끌기 시작하면 그 버튼 클릭은 취소한다.
        void HandleDragScroll(Rect area)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && area.Contains(e.mousePosition))
            {
                dragStartY = e.mousePosition.y;
                dragActive = false;
                dragTracking = true;
            }
            else if (e.type == EventType.MouseDrag && dragTracking && e.button == 0)
            {
                if (!dragActive && Mathf.Abs(e.mousePosition.y - dragStartY) > 6f)
                {
                    dragActive = true;
                    GUIUtility.hotControl = 0; // 누르고 있던 버튼/슬라이더가 반응하지 않게 한다.
                }
                if (dragActive)
                {
                    tabScroll.y = Mathf.Max(0f, tabScroll.y - e.delta.y);
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp)
            {
                dragTracking = false;
                dragActive = false;
            }
        }

        void DrawTab(int index, string name)
        {
            if (GUILayout.Button(name, tab == index ? tabBtnActive : tabBtn, GUILayout.Height(34)))
            {
                if (tab != index) tabScroll = Vector2.zero;
                tab = index;
            }
        }

        void ApplyPreview(PlayerOptionsData data)
        {
            staged = data;
            PlayerOptions.Preview(staged);
        }

        float Slider(string labelText, float value01, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(labelText, label, GUILayout.Width(150));
            float next = GUILayout.HorizontalSlider(value01, min, max, GUILayout.Height(24));
            GUILayout.Label(Mathf.RoundToInt(Mathf.InverseLerp(min, max, next) * 100f) + "%", sliderLabel, GUILayout.Width(50));
            GUILayout.EndHorizontal();
            return next;
        }

        void DrawAudioTab()
        {
            // 배경음악/효과음 분리는 폴리싱 단계에서 다시 검토하기로 함 (2026-10-22) — 지금은 전체 음량만 노출.
            // PlayerOptionsData.bgmVolume/sfxVolume과 OptionsManager의 믹서 적용 로직은 그대로 남겨뒀으니,
            // 나중에 믹서에 BGM/SFX 그룹이 생기면 여기 슬라이더 두 줄만 다시 추가하면 된다.
            float v;
            v = Slider("전체 음량", staged.masterVolume, 0f, 1f);
            if (!Mathf.Approximately(v, staged.masterVolume)) { staged.masterVolume = v; PlayerOptions.Preview(staged); }

            v = Slider("상대방 음성", staged.voiceVolume, 0f, 1f);
            if (!Mathf.Approximately(v, staged.voiceVolume)) { staged.voiceVolume = v; PlayerOptions.Preview(staged); }

            GUILayout.Space(8);
            bool micOn = !staged.micMuted;
            bool nextMicOn = GUILayout.Toggle(micOn, " 내 마이크 켜기", toggleStyle, GUILayout.Height(28));
            if (nextMicOn != micOn) { staged.micMuted = !nextMicOn; PlayerOptions.Preview(staged); }

            GUILayout.Space(6);
            string micStatus = VoiceChatManager.Instance != null ? VoiceChatManager.Instance.Status : "음성: 아직 시작 안 됨";
            GUILayout.Label(micStatus, small);
            bool voiceReady = VoiceChatManager.Instance != null && VoiceChatManager.Instance.IsReady;
            if (voiceReady)
            {
                string speaking = VoiceChatManager.Instance.SelfSpeaking ? "말하는 중 (입력 감지됨)" : "조용함";
                GUILayout.Label("마이크 입력: " + speaking, small);
            }

            GUILayout.Space(10);
            if (!voiceReady)
            {
                GUILayout.Label("장치 목록은 방에 들어가 음성이 연결된 뒤에 고를 수 있습니다.", small);
            }
            else
            {
                GUILayout.Label("마이크 장치", label);
                DrawDeviceList(VivoxService.Instance.AvailableInputDevices, VivoxService.Instance.ActiveInputDevice, isInput: true);
                GUILayout.Space(8);
                GUILayout.Label("스피커 장치", label);
                DrawDeviceList(VivoxService.Instance.AvailableOutputDevices, VivoxService.Instance.ActiveOutputDevice, isInput: false);
            }
        }

        // devices: VivoxInputDevice 또는 VivoxOutputDevice의 읽기 전용 목록 (object로 받아서 둘 다 처리).
        void DrawDeviceList(System.Collections.Generic.IEnumerable<object> devices, object active, bool isInput)
        {
            foreach (var dev in devices)
            {
                string name = isInput ? ((VivoxInputDevice)dev).DeviceName : ((VivoxOutputDevice)dev).DeviceName;
                string id = isInput ? ((VivoxInputDevice)dev).DeviceID : ((VivoxOutputDevice)dev).DeviceID;
                bool isActive = active != null && (isInput ? ((VivoxInputDevice)active).DeviceID : ((VivoxOutputDevice)active).DeviceID) == id;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button((isActive ? "● " : "  ") + name, isActive ? tabBtnActive : tabBtn, GUILayout.Height(30)))
                {
                    if (isInput) { staged.micDeviceId = id; ((VivoxInputDevice)dev).SetActiveDeviceAsync(); }
                    else { staged.speakerDeviceId = id; ((VivoxOutputDevice)dev).SetActiveDeviceAsync(); }
                    PlayerOptions.Preview(staged);
                }
                GUILayout.EndHorizontal();
            }
        }

        void DrawGraphicsTab()
        {
            bool nextFullscreen = GUILayout.Toggle(staged.fullscreen, " 전체 화면 (테두리 없음)", toggleStyle, GUILayout.Height(28));
            if (nextFullscreen != staged.fullscreen) { staged.fullscreen = nextFullscreen; PlayerOptions.Preview(staged); }
            GUILayout.Label(staged.fullscreen ? "" : "꺼져 있으면 창 모드로 실행됩니다.", small);

            GUILayout.Space(10);
            float v = Slider("화면 밝기", staged.brightness, -1f, 1f);
            if (!Mathf.Approximately(v, staged.brightness)) { staged.brightness = v; PlayerOptions.Preview(staged); }
            GUILayout.Label("어두운 곳에서도 단서는 원래 보이도록 만들어져 있습니다 — 이 슬라이더는 보기 편하게 밝기만 보정합니다.", small);

            GUILayout.Space(14);
            GUILayout.Label("해상도", label);
            EnsureResolutionOptions();
            int current = CurrentResolutionIndex();
            GUILayout.BeginHorizontal();
            GUI.enabled = !resolutionPending && current > 0;
            if (GUILayout.Button("◀", button, GUILayout.Width(40), GUILayout.Height(30))) TryChangeResolution(resolutionOptions[current - 1]);
            GUI.enabled = true;
            GUILayout.Label($"{resolutionOptions[current].w} x {resolutionOptions[current].h}", label, GUILayout.Width(150));
            GUI.enabled = !resolutionPending && current < resolutionOptions.Count - 1;
            if (GUILayout.Button("▶", button, GUILayout.Width(40), GUILayout.Height(30))) TryChangeResolution(resolutionOptions[current + 1]);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (resolutionPending)
            {
                int left = Mathf.CeilToInt(resolutionRevertAt - Time.unscaledTime);
                GUILayout.Space(6);
                GUILayout.Label($"이 해상도를 유지할까요? {left}초 후 자동으로 되돌아갑니다.", warnStyle);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("유지하기", button, GUILayout.Height(32))) ConfirmResolution();
                if (GUILayout.Button("지금 되돌리기", button, GUILayout.Height(32))) RevertResolution();
                GUILayout.EndHorizontal();
            }
        }

        void EnsureResolutionOptions()
        {
            if (resolutionOptions != null) return;
            // 고정 4종 (16:9). 모니터가 지원하는 목록 전체 대신 이것만 보여준다.
            resolutionOptions = new List<(int, int)> { (1280, 720), (1920, 1080), (2560, 1440), (3840, 2160) };
        }

        // 지금 해상도의 칸. 목록에 없는 해상도(예: 1366x768)면 넓이가 가장 가까운 칸을 보여준다.
        int CurrentResolutionIndex()
        {
            int exact = resolutionOptions.FindIndex(o => o.w == staged.resolutionWidth && o.h == staged.resolutionHeight);
            if (exact >= 0) return exact;
            long area = (long)staged.resolutionWidth * staged.resolutionHeight;
            int best = 0;
            long bestDiff = long.MaxValue;
            for (int i = 0; i < resolutionOptions.Count; i++)
            {
                long diff = System.Math.Abs((long)resolutionOptions[i].w * resolutionOptions[i].h - area);
                if (diff < bestDiff) { bestDiff = diff; best = i; }
            }
            return best;
        }

        void TryChangeResolution((int w, int h) target)
        {
            resolutionPrevWidth = staged.resolutionWidth;
            resolutionPrevHeight = staged.resolutionHeight;
            staged.resolutionWidth = target.w;
            staged.resolutionHeight = target.h;
            Screen.SetResolution(target.w, target.h, staged.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            resolutionPending = true;
            resolutionRevertAt = Time.unscaledTime + ResolutionConfirmSeconds;
        }

        void ConfirmResolution()
        {
            resolutionPending = false;
            PlayerOptions.Preview(staged);
        }

        void RevertResolution()
        {
            resolutionPending = false;
            staged.resolutionWidth = resolutionPrevWidth;
            staged.resolutionHeight = resolutionPrevHeight;
            Screen.SetResolution(resolutionPrevWidth, resolutionPrevHeight, staged.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            PlayerOptions.Preview(staged);
        }

        void DrawControlsTab()
        {
            float v = Slider("마우스 감도", staged.mouseSensitivity, 0.2f, 5f);
            if (!Mathf.Approximately(v, staged.mouseSensitivity)) { staged.mouseSensitivity = v; PlayerOptions.Preview(staged); }

            GUILayout.Space(10);
            GUILayout.Label("현재 조작 키", label);
            GUILayout.Label(
                "WASD 이동 · 마우스 시점 · 스페이스 점프\n" +
                "[E] 조사/상호작용 · [I] 소지품 · [F] 읽기 · [G] 내려놓기 · [R] 양초 교체\n" +
                "[M] 마이크 음소거 · [ESC] 일시정지/옵션",
                small);
        }

        void DrawGameTab()
        {
            GUILayout.Label("조준점", label);

            GUILayout.BeginHorizontal();
            GUILayout.Label("모양", label, GUILayout.Width(70));
            bool hasShapes = crosshairTextures != null && crosshairTextures.Length > 0;
            int shapeIndex = hasShapes ? Mathf.Clamp(staged.crosshairIndex, 0, crosshairTextures.Length - 1) : 0;
            GUI.enabled = hasShapes && shapeIndex > 0;
            if (GUILayout.Button("◀", button, GUILayout.Width(40), GUILayout.Height(60))) ChangeCrosshairShape(shapeIndex - 1);
            GUI.enabled = true;

            // 미리보기: 지금 고른 모양을 실제 크기/색상 그대로 어두운 바탕 위에 보여준다.
            Rect previewRect = GUILayoutUtility.GetRect(80f, 60f, GUILayout.Width(80f), GUILayout.Height(60f));
            if (Event.current.type == EventType.Repaint)
            {
                var old = GUI.color;
                GUI.color = new Color(0.08f, 0.08f, 0.08f, 1f);
                GUI.DrawTexture(previewRect, Texture2D.whiteTexture);
                if (hasShapes && crosshairTextures[shapeIndex] != null)
                {
                    float size = Mathf.Min(56f, CrosshairBaseSize * staged.crosshairSize);
                    var r = new Rect(previewRect.center.x - size / 2f, previewRect.center.y - size / 2f, size, size);
                    GUI.color = staged.crosshairColor;
                    GUI.DrawTexture(r, crosshairTextures[shapeIndex]);
                }
                GUI.color = old;
            }

            GUI.enabled = hasShapes && shapeIndex < crosshairTextures.Length - 1;
            if (GUILayout.Button("▶", button, GUILayout.Width(40), GUILayout.Height(60))) ChangeCrosshairShape(shapeIndex + 1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            float v = Slider("조준점 크기", staged.crosshairSize, 0.5f, 2.5f);
            if (!Mathf.Approximately(v, staged.crosshairSize)) { staged.crosshairSize = v; PlayerOptions.Preview(staged); }

            GUILayout.Space(8);
            GUILayout.Label("조준점 색상", label);
            GUILayout.BeginHorizontal();
            if (circleTex == null) circleTex = MakeCircle(64);
            for (int i = 0; i < CrosshairPresets.Length; i++)
            {
                var c = CrosshairPresets[i];
                bool isActive = staged.crosshairColor == c;
                Rect cr = GUILayoutUtility.GetRect(40f, 40f, GUILayout.Width(40f), GUILayout.Height(40f));
                // 단순한 원형 색 견본. 선택된 색은 바깥에 흰 테두리 원을 한 겹 더 그린다.
                if (Event.current.type == EventType.Repaint)
                {
                    var old = GUI.color;
                    if (isActive) { GUI.color = MeetYouGuiSkin.Gold; GUI.DrawTexture(cr, circleTex); }
                    else if (cr.Contains(Event.current.mousePosition)) { GUI.color = new Color(1f, 1f, 1f, 0.45f); GUI.DrawTexture(cr, circleTex); }
                    GUI.color = c;
                    float pad = isActive ? 5f : 4f;
                    GUI.DrawTexture(new Rect(cr.x + pad, cr.y + pad, cr.width - pad * 2f, cr.height - pad * 2f), circleTex);
                    GUI.color = old;
                }
                if (GUI.Button(cr, GUIContent.none, GUIStyle.none))
                {
                    staged.crosshairColor = c;
                    PlayerOptions.Preview(staged);
                }
                GUILayout.Space(6);
            }
            GUILayout.EndHorizontal();
        }

        Texture2D circleTex;

        // 가장자리가 부드러운 흰색 원 (GUI.color로 색을 입혀 쓴다)
        static Texture2D MakeCircle(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
                }
            t.SetPixels(px);
            t.Apply(false, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        void ChangeCrosshairShape(int index)
        {
            staged.crosshairIndex = index;
            PlayerOptions.Preview(staged);
        }
    }
}
