#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// 개발자 모드: 기믹을 직접 풀지 않고 "완료" 상태로 바로 넘기는 테스트용 패널 (2026-10-28).
//
// 사용법: 플레이 중 F9 → 왼쪽 위에 패널이 열린다 (커서가 풀림). 버튼을 누르면 그 기믹이 완료 처리된다. F9로 닫는다.
//  - 판정은 호스트가 하므로 버튼은 호스트(또는 혼자 Play)에서만 동작한다. 게스트 화면에는 결과만 동기화되어 보인다.
//  - 각 기믹의 "완료했을 때 일어나는 일"(문 열림, 철창 사라짐, 완료 문구, 다음 기믹 시작 등)은 실제로 풀었을 때와 같은 경로로 일어난다.
//
// 막는 방법 (개발이 끝나면):
//  1) 자동: 이 파일 전체가 `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 안에 있다.
//     Build Profiles(Build Settings)에서 "Development Build"를 끄고 빌드하면 패널 코드 자체가 빌드에서 빠진다.
//  2) 수동: 아래 Enabled를 false로 바꾸면 에디터·개발 빌드에서도 F9가 동작하지 않는다.
//
// 씬에 배치할 필요 없음: 게임이 시작될 때 스스로 숨은 오브젝트를 하나 만든다.
public class DevGimmickPanel : MonoBehaviour
{
    public static bool Enabled = true;

    const Key ToggleKey = Key.F9;

    bool open;
    CursorLockMode prevLock;
    bool prevVisible;
    string lastResult = "";
    [System.NonSerialized] GUIStyle titleStyle, labelStyle, buttonStyle;
    Texture2D bg;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (!Enabled || FindAnyObjectByType<DevGimmickPanel>() != null) return;
        var go = new GameObject("DevGimmickPanel (개발자 모드)") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        go.AddComponent<DevGimmickPanel>();
    }

    void OnDestroy() { if (open) LGH.MultiplayerMenu.OverlayOpen = false; }

    static bool IsHost
    {
        get
        {
            var nm = NetworkManager.Singleton;
            return nm == null || !nm.IsListening || nm.IsServer;
        }
    }

    void Update()
    {
        if (!Enabled) return;
        var kb = Keyboard.current;
        if (kb == null || !kb[ToggleKey].wasPressedThisFrame) return;
        open = !open;
        LGH.MultiplayerMenu.OverlayOpen = open;   // 커서가 풀려도 ESC 일시정지 메뉴가 뜨지 않게
        if (open) { prevLock = Cursor.lockState; prevVisible = Cursor.visible; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        else { Cursor.lockState = prevLock; Cursor.visible = prevVisible; }
    }

    void OnGUI()
    {
        if (!Enabled || !open) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            labelStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
        }

        const float w = 360f;
        var area = new Rect(16f, 16f, w, 520f);
        if (bg == null) { bg = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave }; bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.85f)); bg.Apply(); }
        GUI.DrawTexture(area, bg);

        GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, w - 24f, area.height - 20f));
        GUILayout.Label("개발자 모드 — 기믹 완료 (F9 닫기)", titleStyle);
        if (!IsHost) GUILayout.Label("호스트 화면에서만 조작할 수 있습니다.", labelStyle);
        GUILayout.Space(6);

        bool prevEnabled = GUI.enabled;
        GUI.enabled = IsHost;
        Row("GIM-001 초상화 퍼즐", Gim001Done, CompleteGim001);
        Row("GIM-002 인형극", Gim002Done, CompleteGim002);
        Row("GIM-003 약품 제조", Gim003Done, CompleteGim003);
        Row("GIM-004 온실 화분", Gim004Done, CompleteGim004);
        Row("GIM-005 덤불 미로", Gim005Done, CompleteGim005);
        Row("GIM-007 안방 가구 밀기", Gim007Done, CompleteGim007);
        Row("다락 별 상자 열기", StarBoxDone, OpenStarBox);
        GUILayout.Space(6);
        if (GUILayout.Button("탈출 성공 → 엔딩", buttonStyle, GUILayout.Height(28)))
        {
            var flow = CoopDemo.GameFlowSync.Instance;
            if (flow != null) { flow.RequestEscapeSuccess(); lastResult = "탈출 성공 요청 (플레이 중일 때만 받아들임)"; }
            else lastResult = "이 씬에 GameFlowSync가 없습니다";
        }
        GUI.enabled = prevEnabled;

        GUILayout.Space(8);
        if (!string.IsNullOrEmpty(lastResult)) GUILayout.Label(lastResult, labelStyle);
        GUILayout.EndArea();
    }

    void Row(string label, bool? done, System.Func<string> complete)
    {
        string state = done == null ? "  (씬에 없음)" : done.Value ? "  ✔ 완료" : "";
        bool prev = GUI.enabled;
        if (done == null || done.Value) GUI.enabled = false;
        if (GUILayout.Button(label + state, buttonStyle, GUILayout.Height(28)))
        {
            try { lastResult = label + ": " + complete(); }
            catch (System.Exception e) { lastResult = label + " 실패: " + e.Message; Debug.LogException(e); }
        }
        GUI.enabled = prev;
    }

    // ---------------- 각 기믹 ----------------
    // 온라인이면 각 NetSync의 호스트 판정 엔진(authority)을, 혼자면 Local 어댑터를 그대로 쓴다.
    static T Authority<T>(object net) where T : class
    {
        if (net == null) return null;
        if (net is T direct) return direct;
        var f = net.GetType().GetField("authority", BindingFlags.Instance | BindingFlags.NonPublic);
        return f != null ? f.GetValue(net) as T : null;
    }

    static bool? Gim001Done { get { return PortraitPuzzle.Instance != null ? PortraitPuzzle.Instance.Solved : (bool?)null; } }
    static string CompleteGim001()
    {
        var cfg = PortraitPuzzleScene.Instance;
        var pz = PortraitPuzzle.Instance;
        if (cfg == null || pz == null) return "씬에 없음";
        for (int i = 0; i < cfg.answer.Length && i < cfg.portraits.Length; i++)
        {
            var p = cfg.portraits[i];
            int need = cfg.answer[i] - p.Step;
            for (int k = 0; k < Mathf.Abs(need); k++) pz.RequestShift(p, need > 0 ? 1 : -1);
        }
        return "액자를 정답으로 맞춤";
    }

    static bool? Gim002Done { get { var c = Gim002.Gim002Controller.Instance; return c != null ? c.State.state == Gim002.Gim002State.Complete : (bool?)null; } }
    static string CompleteGim002()
    {
        var a = Authority<Gim002.LocalGim002Net>(Gim002.Gim002Controller.Instance?.Net);
        if (a == null) return "판정 엔진을 찾지 못함";
        a.Logic.DebugForceComplete();
        return "완료 처리";
    }

    static bool? Gim003Done { get { var s = PotionCraft.PotionCraftSync.Instance; return s != null ? s.Completed : (bool?)null; } }
    static string CompleteGim003()
    {
        var s = PotionCraft.PotionCraftSync.Instance;
        if (s == null) return "씬에 없음";
        s.ServerComplete();
        return "완료 처리";
    }

    static bool? Gim004Done { get { var c = Gim004.Gim004Controller.Instance; return c != null ? c.IsComplete : (bool?)null; } }
    static string CompleteGim004()
    {
        var a = Authority<Gim004.LocalGim004Net>(Gim004.Gim004Controller.Instance?.Net);
        if (a == null) return "판정 엔진을 찾지 못함";
        a.Logic.DebugForceComplete();
        return "완료 처리";
    }

    static bool? Gim005Done { get { var c = Gim005.Gim005Controller.Instance; return c != null ? c.IsComplete : (bool?)null; } }
    static string CompleteGim005()
    {
        var a = Authority<Gim005.LocalGim005Net>(Gim005.Gim005Controller.Instance?.Net);
        if (a == null) return "판정 엔진을 찾지 못함";
        a.Logic.DebugForceComplete();
        return "완료 처리";
    }

    static bool? Gim007Done { get { var c = Gim007.Gim007Controller.Instance; return c != null ? c.IsComplete : (bool?)null; } }
    static string CompleteGim007()
    {
        var a = Authority<Gim007.LocalGim007Net>(Gim007.Gim007Controller.Instance?.Net);
        if (a == null) return "판정 엔진을 찾지 못함";
        a.Logic.DebugForceComplete();
        return "완료 처리";
    }

    static LGH.StarLockBox StarBox { get { return FindAnyObjectByType<LGH.StarLockBox>(); } }
    static bool? StarBoxDone { get { var b = StarBox; return b != null ? b.IsOpen : (bool?)null; } }
    static string OpenStarBox()
    {
        var b = StarBox;
        if (b == null) return "씬에 없음";
        // 실제로 숫자를 돌리고 버튼을 누르는 것과 같은 경로 (네트워크 게임이면 호스트 판정 → 모두에게 동기화)
        var code = b.Code;
        for (int i = 0; i < 4; i++)
        {
            int presses = (code[i] - b.Digit(i) + 10) % 10;
            for (int k = 0; k < presses; k++) b.Press(i);
        }
        b.Press(4);
        return "망원경 코드로 열기 요청";
    }
}
#endif
