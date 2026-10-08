using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

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
        string status = "온라인 서비스에 연결 중...";
        bool ready, busy, paused, querying;
        float nextRefresh;
        Vector2 listScroll;
        readonly List<ISessionInfo> rooms = new List<ISessionInfo>();
        // IMGUI needs the same controls in Layout and Repaint, so OnGUI draws from a snapshot taken at Layout
        readonly List<ISessionInfo> shown = new List<ISessionInfo>();
        bool gInGame, gPaused, gHasSession, gReady;
        GUIStyle title, label, small, box, button, field, rowName, rowInfo;

        async void Start()
        {
            ShowCursor(true);
            roomName = "저택 " + UnityEngine.Random.Range(100, 1000);
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
            paused = InGame && Cursor.lockState != CursorLockMode.Locked;

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
                status = "참가 실패: 방이 가득 찼거나 사라졌어요 (" + e.Message + ")";
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

        async void LeaveRoom()
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
                status = "호스트와 연결이 끊어졌어요";
                session = null;
                nextRefresh = 0f;
                ShowCursor(true);
            }
        }

        static void ShowCursor(bool show)
        {
            Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = show;
        }

        // ---------- GUI ----------

        void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            label = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleLeft };
            box = new GUIStyle(GUI.skin.box);
            button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            rowName = new GUIStyle(GUI.skin.button) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 4, 4) };
            rowInfo = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleRight };
        }

        void OnGUI()
        {
            Styles();
            if (Event.current.type == EventType.Layout)
            {
                shown.Clear(); shown.AddRange(rooms);
                gInGame = InGame; gPaused = paused; gHasSession = session != null; gReady = ready;
            }
            var v = UiScale.Apply();
            float w = v.x, h = v.y;

            if (gInGame && !gPaused)
            {
                if (session != null)
                {
                    string info = session.Name + "   참가 코드 " + session.Code + "   " + session.PlayerCount + "/" + session.MaxPlayers + "명";
                    GUI.Label(new Rect(14, 8, w - 28, 28), info, small);
                    if (session.IsHost && session.PlayerCount < 2) GUI.Label(new Rect(14, 34, w - 28, 28), "상대를 기다리는 중... 상대는 방 목록에서 이 방을 누르면 들어와요", small);
                }
                return;
            }

            float pw = 520f, ph = gInGame ? 220f : Mathf.Min(620f, h - 40f);
            var r = new Rect((w - pw) / 2f, (h - ph) / 2f, pw, ph);
            GUI.Box(r, GUIContent.none, box);
            GUILayout.BeginArea(new Rect(r.x + 24, r.y + 16, pw - 48, ph - 32));

            if (gInGame)
            {
                GUILayout.Label("일시정지", title, GUILayout.Height(48));
                if (gHasSession) GUILayout.Label(session != null ? session.Name + "  (참가 코드: " + session.Code + ")" : "", label);
                GUILayout.Space(12);
                if (GUILayout.Button("계속하기", button, GUILayout.Height(42))) ShowCursor(false);
                GUILayout.Space(6);
                GUI.enabled = !busy;
                if (GUILayout.Button("방 나가기", button, GUILayout.Height(42))) LeaveRoom();
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label("저택", title, GUILayout.Height(50));
                GUILayout.Label("2인 협동 방탈출", label);
                GUILayout.Space(10);

                bool canAct = ready && !busy && session == null;
                GUI.enabled = canAct;

                // create
                GUILayout.BeginHorizontal();
                roomName = GUILayout.TextField(roomName, 20, field, GUILayout.Height(42), GUILayout.Width(250));
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
                GUILayout.Label(status, label);
            }
            GUILayout.EndArea();
        }
    }
}
