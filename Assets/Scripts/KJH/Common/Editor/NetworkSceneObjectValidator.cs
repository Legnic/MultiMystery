using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 씬에 직접 둔 NetworkObject가 "게스트에서 지워지는" 구성인지 미리 알려주는 검사 도구.
//
// 왜 필요한가: NetworkManager의 EnableSceneManagement가 꺼져 있으면, 게스트는 접속하는 순간 자기 씬의 NetworkObject를 전부 삭제하고
// 호스트가 보낸 정보로 "등록된 네트워크 프리팹"에서 다시 만든다. 그래서 씬에 맨몸으로 둔 NetworkObject, 또는 프리팹이어도
// DefaultNetworkPrefabs(NetworkPrefabsList)에 등록되지 않은 것은 게스트에서 조용히 사라지고, 그 위의 동기화 스크립트가 전부 멈춘다.
// (호스트에서는 멀쩡해서 놓치기 쉽다.)
//
// 언제 돈다: ① Play 모드 진입 직전 ② 빌드 직전 ③ 메뉴 Tools/Network/Validate Scene NetworkObjects (열려 있는 씬 전체).
// 문제가 있으면 콘솔에 Error로 목록을 남긴다 (Play/빌드를 막지는 않는다).
[InitializeOnLoad]
public static class NetworkSceneObjectValidator
{
    private const string MenuPath = "Tools/Network/Validate Scene NetworkObjects";

    static NetworkSceneObjectValidator()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Validate();
        };
    }

    [MenuItem(MenuPath)]
    private static void ValidateMenu()
    {
        int problems = Validate();
        if (problems == 0) Debug.Log("[NetworkSceneValidator] 문제 없음 — 씬의 NetworkObject가 모두 등록된 프리팹입니다.");
    }

    // 열려 있는 씬들을 검사하고 문제 개수를 돌려준다.
    public static int Validate()
    {
        HashSet<GameObject> registered = LoadRegisteredPrefabs();
        int problems = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            problems += ValidateScene(scene, registered);
        }
        return problems;
    }

    private static int ValidateScene(Scene scene, HashSet<GameObject> registered)
    {
        // 이 씬의 NetworkManager가 씬 관리를 켜 두었다면 씬 오브젝트가 지워지지 않으므로 검사할 필요가 없다.
        NetworkManager manager = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            manager = root.GetComponentInChildren<NetworkManager>(true);
            if (manager != null) break;
        }
        if (manager == null || manager.NetworkConfig == null || manager.NetworkConfig.EnableSceneManagement) return 0;

        int problems = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (NetworkObject networkObject in root.GetComponentsInChildren<NetworkObject>(true))
            {
                string reason = Diagnose(networkObject.gameObject, registered);
                if (reason == null) continue;

                problems++;
                Debug.LogError($"[NetworkSceneValidator] '{HierarchyPath(networkObject.transform)}' ({scene.name}) — {reason}\n" +
                               "게스트가 접속하면 이 오브젝트가 삭제되어, 이것에 붙은 동기화 스크립트가 게스트에서 동작하지 않습니다. " +
                               "프리팹으로 만들고 DefaultNetworkPrefabs에 등록한 뒤 씬에는 그 프리팹 인스턴스를 두세요.", networkObject);
            }
        }
        return problems;
    }

    // 문제가 없으면 null, 있으면 사유를 돌려준다.
    private static string Diagnose(GameObject go, HashSet<GameObject> registered)
    {
        // 등록된 프리팹 안에 들어 있는 NetworkObject(예: 플레이어 프리팹의 자식)는 프리팹째로 만들어지므로 괜찮다.
        // 그래서 "가장 가까운 프리팹 인스턴스 루트"의 원본이 등록돼 있는지만 본다.
        if (!PrefabUtility.IsPartOfPrefabInstance(go)) return "프리팹이 아닌 씬 오브젝트입니다.";

        GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
        GameObject source = instanceRoot != null ? PrefabUtility.GetCorrespondingObjectFromOriginalSource(instanceRoot) : null;
        if (source == null) return "프리팹 원본을 찾을 수 없습니다.";

        // 바리안트/중첩이면 원본 체인의 어느 한 단계라도 등록돼 있으면 NGO가 해시로 찾을 수 있다.
        for (GameObject cursor = source; cursor != null; cursor = PrefabUtility.GetCorrespondingObjectFromSource(cursor))
        {
            if (registered.Contains(cursor)) return null;
        }
        return $"프리팹 '{source.name}'이(가) 네트워크 프리팹 목록(NetworkPrefabsList)에 등록되어 있지 않습니다.";
    }

    private static HashSet<GameObject> LoadRegisteredPrefabs()
    {
        var set = new HashSet<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:NetworkPrefabsList"))
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(AssetDatabase.GUIDToAssetPath(guid));
            if (list == null) continue;
            foreach (NetworkPrefab entry in list.PrefabList)
            {
                if (entry != null && entry.Prefab != null) set.Add(entry.Prefab);
            }
        }
        return set;
    }

    private static string HierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }

    // 빌드 직전에도 같은 검사를 돌린다 (빌드에 들어갈 씬이 열려 있는 씬과 같다는 전제의 간단한 검사).
    private sealed class BuildHook : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            int problems = Validate();
            if (problems > 0) Debug.LogError($"[NetworkSceneValidator] 빌드 전 검사에서 {problems}개의 문제를 찾았습니다. 위 에러 목록을 확인하세요.");
        }
    }
}
