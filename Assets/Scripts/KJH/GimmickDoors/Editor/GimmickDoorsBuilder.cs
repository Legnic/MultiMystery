using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GimmickDoors.EditorTools
{
    // 기믹 완료 → 문 열림 연결 자동 제작. Unity 메뉴 Tools/KJH/GimmickDoors/Build.
    // 다시 실행해도 된다: Assets/Prefabs/KJH/GimmickDoors/GimmickDoors.prefab을 다시 만들고(같은 경로·GUID) 씬의 GimmickDoors_KJH를 새로 배치한다.
    // 네트워크 오브젝트가 아니라 DefaultNetworkPrefabs 등록은 필요 없다 (문 동기화는 LGH DoorNetworkSync가 한다).
    // 문 목록·여는 방향을 바꾸려면 프리팹의 GimmickDoorLink를 Inspector에서 고치거나 아래 값을 고치고 다시 실행.
    public static class GimmickDoorsBuilder
    {
        const string Dir = "Assets/Prefabs/KJH/GimmickDoors";
        const string PrefabPath = Dir + "/GimmickDoors.prefab";

        [MenuItem("Tools/KJH/GimmickDoors/Build")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Prefabs/KJH", "GimmickDoors");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var g in scene.GetRootGameObjects().Where(g => g.name == "GimmickDoors_KJH").ToList()) Object.DestroyImmediate(g);

            var go = new GameObject("GimmickDoors_KJH");
            // 문은 openFrom 쪽에서 연 것처럼 잠긴 방 안으로 열린다 (2026-10-23 맵 확인: 주방·객실2는 복도가 동쪽, 서재·온실은 서쪽).
            // 우회로(2026-10-23 추가): 주방은 식당 쪽 문(Door_Kitchen_Dining, 식당이 남쪽), 서재는 손님방1 쪽 문(Door_Library_Guest1, 손님방1이 남쪽)
            Add(go, DoorGimmick.Gim002_PuppetShow,
                Door("Door_Kitchen", new Vector3(34.5f, 0f, -4.25f)),
                Door("Door_Kitchen_Dining", new Vector3(28f, 0f, 2.5f)),
                Door("Door_Guest2", new Vector3(34.5f, 3.9f, -3.25f)));
            Add(go, DoorGimmick.Gim003_PotionCraft,
                Door("Door_Library", new Vector3(53.5f, 3.9f, 1.5f)),
                Door("Door_Library_Guest1", new Vector3(63f, 3.9f, 9.5f)),
                Door("Door_Conservatory", new Vector3(53.5f, 0f, -12.85f)));

            PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = go;
            Debug.Log("[GimmickDoors] Build 완료. 씬을 저장(Ctrl+S)하세요.");
        }

        // 양문은 한쪽 이름만 넣는다 (Door_Kitchen → Door_Kitchen_B도 짝으로 같이 처리)
        static LinkedDoor Door(string name, Vector3 from) { return new LinkedDoor { doorName = name, openFrom = from }; }

        static void Add(GameObject go, DoorGimmick gimmick, params LinkedDoor[] doors)
        {
            var link = go.AddComponent<GimmickDoorLink>();
            var so = new SerializedObject(link);
            so.FindProperty("gimmick").enumValueIndex = (int)gimmick;
            var arr = so.FindProperty("doors");
            arr.arraySize = doors.Length;
            for (int i = 0; i < doors.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("doorName").stringValue = doors[i].doorName;
                e.FindPropertyRelative("openFrom").vector3Value = doors[i].openFrom;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
