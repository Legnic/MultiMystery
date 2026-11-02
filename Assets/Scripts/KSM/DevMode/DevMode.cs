#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoopDemo
{
    // 개발자 모드 (2026-10-31). 키보드로 D-E-V-E-L-O-P 를 순서대로 치면 켜진다 (다시 치면 꺼진다).
    //
    //  · 방 만들기 화면(방 목록)에서 치면  → 서비스 접속 없이 이 컴퓨터를 호스트로 시작해 혼자 게임을 시작한다 (로비·도입부 건너뜀, 바로 플레이).
    //  · 대기방에 혼자 있을 때 치면        → 바로 플레이로 넘어간다.
    //  · 게임 중(두 명이 있어도) 치면      → 친 사람의 컴퓨터에서만 켜진다. 상대에게는 아무 영향이 없다 (충돌 무시는 각자 자기 몸만 바꾼다).
    //
    // 켜진 동안 문 · 난간/창살(정문 철창, 2층 난간) · 창(유리·창턱·창틀)을 통과해서 다닐 수 있다. 벽과 바닥은 그대로 막는다.
    // 구현: 내 CharacterController와 그 콜라이더 사이의 충돌만 Physics.IgnoreCollision으로 끈다 (다른 플레이어·물건은 그대로).
    //
    // KJH님의 DevGimmickPanel(F9, 기믹 완료 버튼)과는 별개다. 둘 다 `UNITY_EDITOR || DEVELOPMENT_BUILD`에서만 컴파일된다.
    // 막는 방법: Development Build를 끄고 빌드하면 코드 자체가 빠진다. 수동으로는 Enabled = false.
    // 씬에 둘 필요 없음: 게임이 시작될 때 스스로 숨은 오브젝트를 하나 만든다.
    public class DevMode : MonoBehaviour
    {
        public static bool Enabled = true;
        // 이 컴퓨터(로컬 플레이어)만의 상태. 네트워크로 보내지 않는다.
        public static bool Active { get; private set; }

        const string CommandText = "develop";
        static readonly Key[] Command = { Key.D, Key.E, Key.V, Key.E, Key.L, Key.O, Key.P };
        const float ResetAfterSeconds = 2f;   // 마지막 키 이후 이만큼 지나면 입력 진행을 처음으로
        const float RescanInterval = 3f;      // 새로 생기거나 다시 켜진 콜라이더도 따라가도록 주기적으로 다시 건다
        const float SoloTimeout = 20f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (!Enabled || FindAnyObjectByType<DevMode>() != null) return;
            var go = new GameObject("DevMode (개발자 모드)") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<DevMode>();
        }

        int progress;
        float lastKeyTime = -999f;
        bool soloPending, hadLocal;
        float soloDeadline;

        readonly HashSet<Collider> ignored = new HashSet<Collider>();
        CharacterController ignoredFor;
        float nextScan;
        Transform buildingRoot, exteriorRoot, shellRoot;

        string toast = "";
        float toastUntil;
        GUIStyle toastStyle, hudStyle;

        void OnDestroy() { Release(); Active = false; }

        void Update()
        {
            if (!Enabled) { if (Active) SetActive(false, false); return; }
            // 방을 나가거나 세션이 끝나면 꺼진다 (다음 방에서는 다시 커맨드를 쳐야 한다)
            bool hasLocal = LGH.NetworkPlayer.Local != null;
            if (hadLocal && !hasLocal) { SetActive(false, false); soloPending = false; }
            hadLocal = hasLocal;

            PollCommand();
            if (soloPending) TickSolo();
            TickPassThrough();
        }

        // ---------------- 커맨드 입력 ----------------

        // 물리 키 기준이라 한글 IME 상태여도 된다 (DebugEscapeTrigger와 같은 방식).
        void PollCommand()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (progress > 0 && Time.unscaledTime - lastKeyTime > ResetAfterSeconds) progress = 0;

            if (kb[Command[progress]].wasPressedThisFrame)
            {
                progress++;
                lastKeyTime = Time.unscaledTime;
                if (progress >= Command.Length) { progress = 0; OnCommand(); }
                return;
            }
            // 시퀀스와 다른 키면 처음부터 (그 키가 첫 글자 D면 거기서부터 다시)
            foreach (var k in Command)
            {
                if (!kb[k].wasPressedThisFrame) continue;
                progress = (k == Command[0]) ? 1 : 0;
                lastKeyTime = Time.unscaledTime;
                break;
            }
        }

        void OnCommand()
        {
            var menu = FindFirstObjectByType<LGH.MultiplayerMenu>();
            if (menu != null) menu.ClearTypedCommand(CommandText);   // 입력칸에 쳤다면 그 글자를 지운다

            var nm = NetworkManager.Singleton;
            bool inGame = LGH.NetworkPlayer.Local != null;

            // 1) 방 만들기(방 목록) 화면: 혼자 시작
            if (!inGame)
            {
                bool menuScreen = menu != null && menu.enabled && nm != null && !nm.IsListening;
                if (menuScreen) StartSolo(nm);
                return;
            }

            // 2) 대기방에 혼자 있으면 바로 플레이로. 둘이 있으면 로비를 건드리지 않고 내 쪽만 켠다.
            var flow = GameFlowSync.Instance;
            if (flow != null && flow.IsSpawned && flow.IsServer && LGH.NetworkPlayer.All.Count <= 1
                && (flow.Stage == GameStage.Lobby || flow.Stage == GameStage.Intro))
            {
                if (flow.ServerDevSkipToPlaying()) { SetActive(true, true, "혼자 시작 — 개발자 모드 켜짐"); return; }
            }

            // 3) 게임 중: 나만 켜고 끈다
            SetActive(!Active, true);
        }

        void StartSolo(NetworkManager nm)
        {
            LGH.RoomResetter.ResetAll();   // 방을 새로 만들 때와 같이 문·퍼즐을 처음 상태로
            if (!nm.StartHost()) { Toast("혼자 시작 실패: 호스트를 시작하지 못했습니다"); return; }
            SetActive(true, false);
            soloPending = true;
            soloDeadline = Time.unscaledTime + SoloTimeout;
            Toast("개발자 모드: 혼자 시작하는 중...");
        }

        void TickSolo()
        {
            var flow = GameFlowSync.Instance;
            if (flow != null && flow.IsSpawned && flow.IsServer && LGH.NetworkPlayer.Local != null)
            {
                soloPending = false;
                flow.ServerDevSkipToPlaying();
                Toast("개발자 모드 켜짐 — 혼자 시작");
            }
            else if (Time.unscaledTime > soloDeadline)
            {
                soloPending = false;
                Toast("혼자 시작 시간 초과: 플레이어가 생성되지 않았습니다");
            }
        }

        void SetActive(bool on, bool announce, string message = null)
        {
            if (Active == on) return;
            Active = on;
            if (!on) Release();
            else nextScan = 0f;
            if (announce) Toast(message ?? (on ? "개발자 모드 켜짐 — 문·난간·창 통과" : "개발자 모드 꺼짐"));
        }

        // ---------------- 통과 ----------------

        void TickPassThrough()
        {
            var local = LGH.NetworkPlayer.Local;
            CharacterController cc = (Active && local != null) ? local.GetComponent<CharacterController>() : null;
            if (cc == null || (ignoredFor != null && ignoredFor != cc)) Release();
            if (cc == null || !cc.enabled || !cc.gameObject.activeInHierarchy) return;
            if (ignoredFor == cc && Time.unscaledTime < nextScan) return;

            nextScan = Time.unscaledTime + RescanInterval;
            ignoredFor = cc;
            // 꺼졌다 켜진 콜라이더는 무시 설정이 풀릴 수 있어서, 이미 건 것도 다시 건다 (몇백 개라 가볍다)
            foreach (var col in CollectPassable())
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                Physics.IgnoreCollision(cc, col, true);
                ignored.Add(col);
            }
        }

        void Release()
        {
            if (ignoredFor != null && ignoredFor.enabled && ignoredFor.gameObject.activeInHierarchy)
                foreach (var c in ignored)
                    if (c != null && c.enabled && c.gameObject.activeInHierarchy) Physics.IgnoreCollision(ignoredFor, c, false);
            ignored.Clear();
            ignoredFor = null;
        }

        List<Collider> buffer = new List<Collider>(1024);
        IEnumerable<Collider> CollectPassable()
        {
            buffer.Clear();
            // 문
            foreach (var d in FindObjectsByType<LGH.InteractableDoor>(FindObjectsInactive.Exclude))
                foreach (var c in d.GetComponentsInChildren<Collider>(false)) if (!c.isTrigger) buffer.Add(c);
            // 난간/창살/창: 건물과 바깥 담장 아래에서만 찾는다
            if (buildingRoot == null) { var g = GameObject.Find("Ravenwood_LGH"); if (g != null) buildingRoot = g.transform; }
            if (exteriorRoot == null) { var g = GameObject.Find("Exterior_LGH"); if (g != null) exteriorRoot = g.transform; }
            Scan(buildingRoot);
            Scan(exteriorRoot != null ? exteriorRoot.Find("PerimeterWall") : null);
            // 저택 외관 쉘: 창 아래 외벽(0~0.9m)과 창턱 돌출부가 이쪽 메시 콜라이더라서, 이걸 무시해야 창을 지날 수 있다.
            // 안쪽 구조 벽(Ravenwood_LGH/…/Seg·Lintel)은 그대로 막으므로 벽을 통과하지는 못한다.
            // 현관 데크(Vic_Deck)와 지붕(Vic_Slate)은 제외 — 바닥이 꺼지면 안 된다.
            if (shellRoot == null) { var g = GameObject.Find("VictorianExterior_LGH"); if (g != null) shellRoot = g.transform; }
            if (shellRoot != null)
                foreach (var name in ShellNames)
                {
                    var t = shellRoot.Find(name);
                    if (t == null) continue;
                    foreach (var c in t.GetComponentsInChildren<Collider>(false)) if (!c.isTrigger) buffer.Add(c);
                }
            return buffer;
        }

        static readonly string[] ShellNames = { "Vic_Brick", "Vic_Stone", "Vic_Trim", "Vic_Iron", "Vic_Glass" };

        void Scan(Transform root)
        {
            if (root == null) return;
            foreach (var c in root.GetComponentsInChildren<Collider>(false))
                if (!c.isTrigger && IsBarOrWindow(c)) buffer.Add(c);
        }

        // 이름 규칙: 창 = 벽 조각 이름(F1_Ext_Kitchen_E_41_Glass0 / _Sill0 / _FrameSill0 / _FrameHead0), 창살 = GateBar·Baluster·CageExtension 또는 Rail_*·Balustrade 아래
        static bool IsBarOrWindow(Collider c)
        {
            string n = c.name;
            if ((n.Contains("_Ext_") || n.Contains("_Int_")) && (n.Contains("_Glass") || n.Contains("_Sill") || n.Contains("_Frame"))) return true;
            if (n.StartsWith("GateBar") || n.StartsWith("Baluster") || n.StartsWith("CageExtension") || n.StartsWith("Rail_")) return true;
            for (var t = c.transform.parent; t != null; t = t.parent)
            {
                string pn = t.name;
                if (pn.StartsWith("Rail_") || pn == "Balustrade") return true;
                if (pn == "Ravenwood_LGH" || pn == "Exterior_LGH") break;
            }
            return false;
        }

        // ---------------- 화면 표시 ----------------

        void Toast(string msg) { toast = msg; toastUntil = Time.unscaledTime + 3f; }

        void OnGUI()
        {
            if (!Enabled) return;
            bool showHud = Active;
            bool showToast = Time.unscaledTime < toastUntil;
            if (!showHud && !showToast) return;
            if (toastStyle == null)
            {
                toastStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                toastStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
                hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                hudStyle.normal.textColor = new Color(1f, 0.85f, 0.4f, 0.9f);
            }
            float s = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            if (showHud) GUI.Label(new Rect(w - 360f, 8f, 350f, 24f), "DEV  문·난간·창 통과  (develop로 끄기)", hudStyle);
            if (showToast)
            {
                var r = new Rect(w * 0.5f - 300f, 70f, 600f, 36f);
                var shadow = new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height);
                var prev = GUI.color; GUI.color = new Color(0, 0, 0, 0.8f); GUI.Label(shadow, toast, toastStyle); GUI.color = prev;
                GUI.Label(r, toast, toastStyle);
            }
            GUI.matrix = old;
        }
    }
}
#endif
