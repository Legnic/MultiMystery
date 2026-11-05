using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GimmickDoors.EditorTools
{
    // 기믹 완료 → 문 열림 연결 자동 제작. Unity 메뉴 Tools/KJH/GimmickDoors/Build.
    // 다시 실행해도 된다: Assets/Prefabs/KJH/GimmickDoors/GimmickDoors.prefab을 다시 만들고(같은 경로·GUID) 씬의 GimmickDoors_KJH를 새로 배치한다.
    // 네트워크 오브젝트가 아니라 DefaultNetworkPrefabs 등록은 필요 없다 (문 동기화는 LGH DoorNetworkSync가 한다).
    // 문 목록을 바꾸려면 프리팹의 GimmickDoorLink를 Inspector에서 고치거나 아래 표를 고치고 다시 실행.
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
            // 2026-11-05 새 규칙: 처음에는 1층 초상화 갤러리와 2층 아이방만 들어갈 수 있고 나머지 문은 전부 잠겨 있다.
            // 문은 기믹이 끝나면 "잠금만" 풀린다(저절로 열리지 않는다). 그래서 문을 열 방향(openFrom)은 더 이상 쓰지 않는다.
            // 방 이름은 게임 속 층별 지도 기준(코드 이름과 다름): Kitchen=약품 재료실, Guest2=약품 조제실, Guest1=손님방, Pantry=저장고.
            // 안내 문구가 있는 기믹(GIM-002/003/004/007)은 풀리는 방 이름이 그 기믹의 완료 문구에 들어 있다 → 문 목록을 바꾸면 문구도 같이 고칠 것.
            //
            // 첫 번째 기믹(초상화 액자, GIM-001)이 끝나면 안내 없이 풀리는 문:
            //   1층 응접실(갤러리↔응접실)·서재·당구실·화장실, 2층 욕실·손님방(욕실↔손님방)·린넨실, 그리고 흡연실·안방
            Add(go, DoorGimmick.Gim001_Portrait,
                Door("Door_Gallery_Parlor"), Door("Door_Study_Parlor"), Door("Door_Billiard_Study"), Door("Door_Billiard"), Door("Door_WC"),
                Door("Door_Bath"), Door("Door_Bath_Guest1"), Door("Door_Linen"),
                Door("Door_CorridorE_Smoking"), Door("Door_Master"));
            // 인형극(GIM-002): 약품 재료실(Kitchen) + 식당 쪽 우회문, 식당, 저장고, 세탁실, 약품 조제실(Guest2)
            Add(go, DoorGimmick.Gim002_PuppetShow,
                Door("Door_Kitchen"), Door("Door_Kitchen_Dining"), Door("Door_Dining"), Door("Door_Pantry"), Door("Door_Laundry"),
                Door("Door_Guest2"));
            // 약품 제조(GIM-003): 서고, 손님방에서 서고로 이어지는 문(서고 문과 같이), 온실
            Add(go, DoorGimmick.Gim003_PotionCraft,
                Door("Door_Library"), Door("Door_Library_Guest1"), Door("Door_Conservatory"));

            PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = go;
            Debug.Log("[GimmickDoors] Build 완료. 씬을 저장(Ctrl+S)하세요.");
        }

        // 양문은 한쪽 이름만 넣는다 (Door_Kitchen → Door_Kitchen_B도 짝으로 같이 처리)
        static LinkedDoor Door(string name) { return new LinkedDoor { doorName = name, openFrom = Vector3.zero }; }

        static void Add(GameObject go, DoorGimmick gimmick, params LinkedDoor[] doors)
        {
            var link = go.AddComponent<GimmickDoorLink>();
            var so = new SerializedObject(link);
            so.FindProperty("gimmick").enumValueIndex = (int)gimmick;
            so.FindProperty("openOnComplete").boolValue = false;   // 잠금만 풀린다 (저절로 열리지 않음)
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
