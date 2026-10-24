using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LGH
{
    // 2-player menu: one player creates a room, it shows up in everyone's room list, the other clicks it to join.
    // Joining by code still works as a fallback. Connection goes through Unity's online services,
    // so it works across different home networks.
    public class MultiplayerMenu : MonoBehaviour
    {
        public Camera menuCamera;
        public int maxPlayers = 2;
        [Tooltip("Seconds between automatic room list refreshes while the menu is open (keep >= 2 to avoid rate limits)")]
        public float refreshInterval = 4f;

        ISession session;
        string joinCode = "";
        string roomName = "";
        string nickname = "";
        // the name this player uses in the room (saved on this PC, shown above the head to the other player)
        public static string Nickname { get; private set; } = "";
        // IMGUI's TextField can't compose Korean, so the name boxes read typed text from the Input System instead.
        // 0 = no box focused, 1 = nickname, 2 = room name
        int imeFocus;
        string imeComposing = "";
        int imeCompFrame = -10;
        bool imeOn;
        // true while the player is typing in one of these boxes (game keys must ignore the keyboard then)
        public static bool Typing { get; private set; }
        string status = "온라인 서비스에 연결 중...";
        bool ready, busy, paused, querying;
        float nextRefresh;
        Vector2 listScroll;
        readonly List<ISessionInfo> rooms = new List<ISessionInfo>();
        // IMGUI needs the same controls in Layout and Repaint, so OnGUI draws from a snapshot taken at Layout
        readonly List<ISessionInfo> shown = new List<ISessionInfo>();
        bool gInGame, gPaused, gHasSession, gReady;
        // Layout과 Repaint 사이에 값이 바뀌면 컨트롤 수가 어긋나 그 프레임에 일부 요소가 안 그려진다 — 구조에 영향을 주는 값도 전부 Layout 시점에 고정한다.
        bool gBusy, gSessionNull, gTwoPlayers, gHasFlow, gLocalReady, gOtherReady;
        CoopDemo.GameStage gStage;
        double gCountdown = -1;
        string gStatus = "", gSessionName = "", gSessionCode = "", gPlayerNames = "";
        int gPlayerCount, gMaxPlayers;
        GUIStyle title, label, small, box, button, field, rowName, rowInfo, countdown;

        async void Start()
        {
            ShowCursor(true);
            var kbd = Keyboard.current;
            if (kbd != null) { kbd.onTextInput += OnTextInput; kbd.onIMECompositionChange += OnImeComposition; }
            // keep the game (and the host's network loop) running when its window is not focused,
            // otherwise a second window on the same PC freezes and the join times out
            Application.runInBackground = true;
            roomName = "저택 " + UnityEngine.Random.Range(100, 1000);
            nickname = PlayerPrefs.GetString("LGH_Nickname", "");
            if (string.IsNullOrWhiteSpace(nickname)) nickname = "탐정 " + UnityEngine.Random.Range(100, 1000);
            Nickname = nickname.Trim();
            try
            {
                var options = new InitializationOptions();
                // a fresh profile per run so two game windows on the same PC count as two different players
                options.SetProfile("p" + Guid.NewGuid().ToString("N").Substring(0, 12));
                await UnityServices.InitializeAsync(options);
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                ready = true;
                status = "방을 만들거나 목록에서 방을 눌러 참가하세요";
                RefreshRooms();
                // test helper: start the game with -lghAutoHost to open a room straight away
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lghAutoHost") >= 0) CreateRoom();
            }
            catch (Exception e)
            {
                status = "온라인 서비스 연결 실패: " + e.Message;
                Debug.LogException(e);
            }
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        }

        void OnDestroy()
        {
            var kbd = Keyboard.current;
            if (kbd != null) { kbd.onTextInput -= OnTextInput; kbd.onIMECompositionChange -= OnImeComposition; }
            Typing = false;
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }

        public string Status { get { return status; } }
        public string SessionCode { get { return session != null ? session.Code : null; } }
        public bool Ready { get { return ready; } }
        public int RoomCount { get { return rooms.Count; } }

        bool InGame { get { return NetworkPlayer.Local != null; } }
        bool InLobbyMenu { get { return ready && !InGame && session == null; } }

        void Update()
        {
            if (menuCamera != null && menuCamera.gameObject.activeSelf == InGame)
                menuCamera.gameObject.SetActive(!InGame);
            // FirstPersonController unlocks the cursor on Esc; treat that as the pause menu
            paused = InGame && Cursor.lockState != CursorLockMode.Locked && !PlayerInventory.UiOpen;
            UpdateIme();

            if (InLobbyMenu && !busy && Time.unscaledTime >= nextRefresh) RefreshRooms();
        }

        // ---------- room list ----------

        public async void RefreshRooms()
        {
            if (!ready || querying) return;
            querying = true;
            nextRefresh = Time.unscaledTime + Mathf.Max(2f, refreshInterval);
            try
            {
                var q = new QuerySessionsOptions
                {
                    Count = 30,
                    FilterOptions = new List<FilterOption>
                    {
                        new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater),
                    },
                    SortOptions = new List<SortOption>
                    {
                        new SortOption(SortOrder.Descending, SortField.CreationTime),
                    },
                };
                var res = await MultiplayerService.Instance.QuerySessionsAsync(q);
                rooms.Clear();
                if (res != null && res.Sessions != null)
                    foreach (var s in res.Sessions)
                        if (!s.IsLocked && s.AvailableSlots > 0) rooms.Add(s);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LGH] 방 목록 갱신 실패: " + e.Message);
            }
            querying = false;
        }

        // ---------- create / join ----------

        public async void CreateRoom()
        {
            busy = true;
            status = "방을 만드는 중...";
            try
            {
                string n = string.IsNullOrWhiteSpace(roomName) ? "저택" : roomName.Trim();
                var options = new SessionOptions { Name = n, MaxPlayers = maxPlayers, IsPrivate = false }.WithRelayNetwork();
                session = await MultiplayerService.Instance.CreateSessionAsync(options);
                RoomResetter.ResetAll(); // fresh room: put every door/puzzle/clue back to its initial state
                status = "방을 만들었어요. 참가 코드: " + session.Code;
            }
            catch (Exception e)
            {
                status = "방 만들기 실패: " + e.Message;
                Debug.LogException(e);
            }
            busy = false;
        }

        public async void JoinRoomById(string sessionId, string name)
        {
            busy = true;
            status = "'" + name + "' 방에 참가하는 중...";
            try
            {
                session = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
                status = "참가했어요";
            }
            catch (Exception e)
            {
                status = "참가 실패: 방장과 연결하지 못했어요. 방장 게임이 켜져 있는지, 양쪽이 같은 버전인지 확인하세요 (" + e.Message + ")";
                Debug.LogException(e);
                nextRefresh = 0f; // refresh the list right away
            }
            busy = false;
        }

        public void JoinRoom(string code) { joinCode = code; JoinRoom(); }

        async void JoinRoom()
        {
            string code = joinCode.Trim().ToUpperInvariant();
            if (code.Length == 0) { status = "참가 코드를 입력하세요"; return; }
            busy = true;
            status = "참가하는 중...";
            try
            {
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                status = "참가했어요";
            }
            catch (Exception e)
            {
                status = "참가 실패: 코드가 맞는지, 방이 가득 찼는지 확인하세요 (" + e.Message + ")";
                Debug.LogException(e);
            }
            busy = false;
        }

        void LeaveRoom() { _ = LeaveRoomAsync(); }

        // 결과창("타이틀로 돌아가기") 등 바깥에서도 세션 정리가 끝난 뒤를 기다렸다가 다음 동작을 이어갈 수 있도록 Task로 노출한다.
        public async System.Threading.Tasks.Task LeaveRoomAsync()
        {
            busy = true;
            try { if (session != null) await session.LeaveAsync(); }
            catch (Exception e) { Debug.LogException(e); }
            session = null;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            status = "방에서 나왔어요";
            busy = false;
            nextRefresh = 0f;
            ShowCursor(true);
        }

        void OnClientDisconnect(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (!nm.IsServer && clientId == nm.LocalClientId)
            {
                status = "호스트와 연결이 끊어졌어요" + (string.IsNullOrEmpty(nm.DisconnectReason) ? "" : " (" + nm.DisconnectReason + ")");
                session = null;
                nextRefresh = 0f;
                ShowCursor(true);
            }
        }

        void SetNickname(string value)
        {
            nickname = value;
            Nickname = string.IsNullOrWhiteSpace(value) ? "플레이어" : value.Trim();
            PlayerPrefs.SetString("LGH_Nickname", nickname);
            if (NetworkPlayer.Local != null) NetworkPlayer.Local.SetNickname(Nickname);
        }

        // e.g. "나: 홍길동   ·   상대: 김철수"
        static string PlayerNames()
        {
            string me = null, other = null;
            foreach (var p in NetworkPlayer.All)
            {
                if (p == null || !p.IsSpawned) continue;
                if (p.IsOwner) me = p.Nickname; else other = other == null ? p.Nickname : other + ", " + p.Nickname;
            }
            string s = me != null ? "나: " + me : "";
            if (other != null) s += (s.Length > 0 ? "   ·   " : "") + "상대: " + other;
            return s;
        }

        void UpdateIme()
        {
            if (imeFocus != 0 && InGame && !paused) imeFocus = 0;   // back in the game: stop typing
            Typing = imeFocus != 0;
            var kbd = Keyboard.current;
            if (kbd != null)
            {
                if (imeOn != Typing) { kbd.SetIMEEnabled(Typing); imeOn = Typing; }
                if (Typing && imeComposing.Length == 0 && Time.frameCount - imeCompFrame > 1)
                {
                    if (kbd.backspaceKey.wasPressedThisFrame) EditIme(-1, ' ');
                    if (kbd.enterKey.wasPressedThisFrame || kbd.numpadEnterKey.wasPressedThisFrame || kbd.escapeKey.wasPressedThisFrame) imeFocus = 0;
                }
            }
            Input.imeCompositionMode = Typing ? IMECompositionMode.On : IMECompositionMode.Auto;
        }

        void OnTextInput(char c)
        {
            if (imeFocus == 0 || char.IsControl(c)) return;
            imeComposing = "";
            EditIme(1, c);
        }

        void OnImeComposition(IMECompositionString s)
        {
            imeComposing = s.ToString();
            imeCompFrame = Time.frameCount;
        }

        // dir 1: add c at the end, -1: delete the last letter
        void EditIme(int dir, char c)
        {
            string cur = imeFocus == 1 ? nickname : roomName;
            int max = imeFocus == 1 ? 12 : 20;
            if (dir < 0) { if (cur.Length > 0) cur = cur.Substring(0, cur.Length - 1); }
            else if (cur.Length < max) cur += c;
            if (imeFocus == 1) SetNickname(cur); else if (imeFocus == 2) roomName = cur;
        }

        // text box that works with the Korean IME: click it to type, Enter (or a click elsewhere) to finish
        void ImeField(int id, string value, params GUILayoutOption[] opts)
        {
            Rect r = GUILayoutUtility.GetRect(new GUIContent(value), field, opts);
            var e = Event.current;
            if (e.type == EventType.MouseDown && GUI.enabled)
            {
                if (r.Contains(e.mousePosition)) { imeFocus = id; imeComposing = ""; GUIUtility.keyboardControl = 0; e.Use(); }
                else if (imeFocus == id) imeFocus = 0;
            }
            if (e.type != EventType.Repaint) return;
            bool focused = imeFocus == id;
            string shown = value + (focused ? imeComposing : "");
            if (focused && ((int)(Time.unscaledTime * 2f)) % 2 == 0) shown += "|";
            field.Draw(r, new GUIContent(shown), r.Contains(e.mousePosition), false, false, focused);
            if (focused)
            {
                // put the IME candidate window under the box
                Vector2 p = GUIUtility.GUIToScreenPoint(new Vector2(r.center.x, r.yMax));
                Input.compositionCursorPos = p;
                var kbd = Keyboard.current;
                if (kbd != null) kbd.SetIMECursorPosition(new Vector2(p.x, Screen.height - p.y));
            }
        }

        void NicknameRow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("닉네임", small, GUILayout.Width(70), GUILayout.Height(38));
            ImeField(1, nickname, GUILayout.Height(38));
            GUILayout.EndHorizontal();
        }

        static void ShowCursor(bool show)
        {
            Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = show;
        }

        // ---------- GUI ----------

        int styleVersion = -1;

        void Styles()
        {
            // 스킨이 새로 만들어졌으면(예: 새 Play 실행) 이전 스타일은 파괴된 텍스처를 가리키므로 다시 만든다
            if (title != null && styleVersion == CoopDemo.MeetYouGuiSkin.Version) return;
            styleVersion = CoopDemo.MeetYouGuiSkin.Version;
            title = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            label = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleLeft };
            box = new GUIStyle(GUI.skin.box);
            button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            rowName = new GUIStyle(GUI.skin.button) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 4, 4) };
            rowInfo = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleRight };
            countdown = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        }

        void OnGUI()
        {
            // Meet You UI 스킨 (KSM/UI/MeetYouGuiSkin). 테마가 없으면 기본 IMGUI 모양 그대로.
            var prevSkin = CoopDemo.MeetYouGuiSkin.Begin();
            try { DrawGui(); }
            finally { CoopDemo.MeetYouGuiSkin.End(prevSkin); }
        }

        void DrawGui()
        {
            Styles();
            if (Event.current.type == EventType.Layout)
            {
                shown.Clear(); shown.AddRange(rooms);
                gInGame = InGame; gPaused = paused; gHasSession = session != null; gReady = ready;
                gBusy = busy; gSessionNull = session == null; gStatus = status;
                gSessionName = session != null ? session.Name : ""; gSessionCode = session != null ? session.Code : "";
                gPlayerCount = session != null ? session.PlayerCount : 0; gMaxPlayers = session != null ? session.MaxPlayers : 0;
                gTwoPlayers = gPlayerCount >= 2;
                gPlayerNames = PlayerNames();
                var f = CoopDemo.GameFlowSync.Instance;
                gHasFlow = f != null;
                if (f != null)
                {
                    gStage = f.Stage; gLocalReady = f.LocalLobbyReady; gOtherReady = f.OtherLobbyReady; gCountdown = f.LobbyCountdownRemaining;
                }
            }

            // 도입부/엔딩/결과 단계에서는 이 화면 대신 해당 Canvas가 화면을 차지한다.
            // 플레이어는 그 단계들 중에도 이미 스폰돼 있어서(InGame=true) 이 가드가 없으면 두 화면이 겹쳐 그려진다.
            // Lobby(대기방)와 Playing(일시정지/상단 정보)은 이 스크립트가 계속 그린다.
            var flow = CoopDemo.GameFlowSync.Instance;
            bool flowOwnsScreen = gInGame && gHasFlow &&
                (gStage == CoopDemo.GameStage.Intro || gStage == CoopDemo.GameStage.Ending || gStage == CoopDemo.GameStage.Result);
            if (flowOwnsScreen) return;

            bool lobbyStage = gInGame && gHasFlow && gStage == CoopDemo.GameStage.Lobby;

            var v = UiScale.Apply();
            float w = v.x, h = v.y;

            if (gInGame && !lobbyStage && !gPaused)
            {
                if (gHasSession)
                {
                    string info = gSessionName + "   참가 코드 " + gSessionCode + "   " + gPlayerCount + "/" + gMaxPlayers + "명";
                    string names = gPlayerNames;
                    if (names.Length > 0) info += "      " + names;
                    GUI.Label(new Rect(14, 8, w - 28, 28), info, small);
                }
                return;
            }

            // 옵션 창이 떠 있는 동안은 이 패널을 그리지 않는다 (OptionsMenuController가 같은 화면 영역을 쓴다).
            if (CoopDemo.OptionsMenuController.IsOpen) return;

            float pw = 520f, ph = !gInGame ? Mathf.Min(680f, h - 40f) : (lobbyStage ? 420f : 320f);
            var r = new Rect((w - pw) / 2f, (h - ph) / 2f, pw, ph);
            GUI.Box(r, GUIContent.none, CoopDemo.MeetYouGuiSkin.Window);
            GUILayout.BeginArea(new Rect(r.x + 24, r.y + 16, pw - 48, ph - 32));

            if (gInGame)
            {
                if (lobbyStage) DrawLobbyWaitingRoomBody(flow);
                else DrawPauseBody();
            }
            else
            {
                GUILayout.Label("저택", title, GUILayout.Height(50));
                GUILayout.Label("2인 협동 방탈출", label);
                GUILayout.Space(6);
                if (GUILayout.Button("옵션", button, GUILayout.Height(32), GUILayout.Width(100))) CoopDemo.OptionsMenuController.Open();
                GUILayout.Space(10);

                NicknameRow();
                GUILayout.Space(8);

                bool canAct = gReady && !gBusy && gSessionNull;
                GUI.enabled = canAct;

                // create
                GUILayout.BeginHorizontal();
                ImeField(2, roomName, GUILayout.Height(42), GUILayout.Width(250));
                if (GUILayout.Button("방 만들기", button, GUILayout.Height(42))) CreateRoom();
                GUILayout.EndHorizontal();

                GUILayout.Space(12);

                // room list
                GUILayout.BeginHorizontal();
                GUILayout.Label("열린 방 (" + shown.Count + ")", small, GUILayout.Height(26));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(querying ? "갱신 중..." : "새로고침", GUILayout.Height(26), GUILayout.Width(100))) RefreshRooms();
                GUILayout.EndHorizontal();

                listScroll = GUILayout.BeginScrollView(listScroll, box, GUILayout.ExpandHeight(true));
                if (shown.Count == 0)
                {
                    GUI.enabled = true;
                    GUILayout.Label(gReady ? "아직 열린 방이 없어요" : "연결 중...", label, GUILayout.Height(40));
                    GUI.enabled = canAct;
                }
                for (int i = 0; i < shown.Count; i++)
                {
                    var s = shown[i];
                    int players = s.MaxPlayers - s.AvailableSlots;
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(s.Name, rowName, GUILayout.Height(40))) JoinRoomById(s.Id, s.Name);
                    GUILayout.Label(players + "/" + s.MaxPlayers + "명", rowInfo, GUILayout.Width(70), GUILayout.Height(40));
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();

                GUILayout.Space(8);

                // code fallback
                GUILayout.BeginHorizontal();
                joinCode = GUILayout.TextField(joinCode, 12, field, GUILayout.Height(36), GUILayout.Width(250));
                if (GUILayout.Button("코드로 참가", button, GUILayout.Height(36))) JoinRoom();
                GUILayout.EndHorizontal();

                GUI.enabled = true;
                GUILayout.Space(8);
                GUILayout.Label(gStatus, label);
            }
            GUILayout.EndArea();
        }

        // 플레이 중(Playing) ESC로 연 일시정지 패널. 기존 동작 그대로.
        void DrawPauseBody()
        {
            GUILayout.Label("일시정지", title, GUILayout.Height(48));
            if (gHasSession) GUILayout.Label(gSessionName + "  (참가 코드: " + gSessionCode + ")", label);
            GUILayout.Space(8);
            NicknameRow();
            GUILayout.Space(8);
            if (GUILayout.Button("계속하기", button, GUILayout.Height(42))) { GUIUtility.keyboardControl = 0; imeFocus = 0; ShowCursor(false); }
            GUILayout.Space(6);
            if (GUILayout.Button("옵션", button, GUILayout.Height(42))) CoopDemo.OptionsMenuController.Open();
            GUILayout.Space(6);
            GUI.enabled = !gBusy;
            if (GUILayout.Button("방 나가기", button, GUILayout.Height(42))) LeaveRoom();
            GUI.enabled = true;
        }

        // 방 생성/참가 직후 ~ 둘 다 준비를 누르고 카운트다운이 끝나기 전까지(GameFlowSync.Stage == Lobby) 보여주는 대기방.
        void DrawLobbyWaitingRoomBody(CoopDemo.GameFlowSync flow)
        {
            GUILayout.Label("대기방", title, GUILayout.Height(48));
            if (!gSessionNull) GUILayout.Label(gSessionName + "   참가 코드 " + gSessionCode, label);
            GUILayout.Space(6);
            NicknameRow();
            GUILayout.Space(10);

            bool twoPlayers = gTwoPlayers;
            GUILayout.Label(twoPlayers ? gPlayerNames : "상대를 기다리는 중... 상대는 방 목록에서 이 방을 누르면 들어와요", label);
            GUILayout.Space(6);

            if (gHasFlow && flow != null)
            {
                bool localReady = gLocalReady;
                bool otherReady = gOtherReady;
                string readyLine = "나: " + (localReady ? "준비 완료" : "준비 안 됨");
                if (twoPlayers) readyLine += "   ·   상대: " + (otherReady ? "준비 완료" : "준비 안 됨");
                GUILayout.Label(readyLine, label);
                GUILayout.Space(8);

                double remain = gCountdown;
                if (remain >= 0)
                {
                    // 패널 폭(약 470)에 안 들어가면 잘리므로 줄바꿈 되는 작은 글씨체를 쓴다.
                    GUILayout.Label("모두 준비 완료!\n" + Mathf.CeilToInt((float)remain) + "초 후 시작합니다...", countdown, GUILayout.Height(64));
                }
                else
                {
                    GUI.enabled = twoPlayers;
                    if (GUILayout.Button(localReady ? "준비 취소" : "준비 완료", button, GUILayout.Height(42))) flow.RequestLobbyReady(!localReady);
                    GUI.enabled = true;
                }
            }

            GUILayout.Space(6);
            if (GUILayout.Button("옵션", button, GUILayout.Height(36))) CoopDemo.OptionsMenuController.Open();
            GUILayout.Space(6);
            GUI.enabled = !gBusy;
            if (GUILayout.Button("방 나가기", button, GUILayout.Height(36))) LeaveRoom();
            GUI.enabled = true;
        }
    }
}
