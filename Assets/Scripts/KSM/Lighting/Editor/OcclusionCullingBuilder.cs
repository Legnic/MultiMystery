using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoopDemo
{
    // 오클루전 컬링 설정 + 베이크 (2026-11-05 최적화).
    // 벽·바닥·천장·지붕을 "점유물(Occluder Static)"로, 베이크된 실내 물건을 "피점유물(Occludee Static)"로 지정하고 베이크한다.
    // 이렇게 하면 벽 뒤에 가려진 방의 가구·소품은 그리지 않는다(드로우콜·삼각형 감소).
    //
    // 점유물로 쓰지 않는 것(가려야 할 것이 사라지는 사고 방지): 유리·창틀·창턱·문·비밀 패널·반투명·얇은 장식 조각.
    // 움직이거나 켜졌다 꺼지는 벽이 생기면(비밀문 등) 이 목록에서 빼고 다시 베이크할 것 — 점유물로 지정된 채 움직이면 그 뒤가 안 보이는 버그가 난다.
    public static class OcclusionCullingBuilder
    {
        // 점유물 후보를 찾는 루트 (Ravenwood_LGH 아래 경로)
        static readonly string[] OccluderRoots = { "Floor1/Walls", "Floor2/Walls", "Attic/Walls", "Floor1/Floors", "Floor2/Floors", "Floor2/Roof" };
        static readonly string[] OccluderAttics = { "AtticFloor_", "AtticRoof_" };
        // 이름에 이게 들어 있으면 점유물에서 뺀다
        static readonly string[] Excluded = { "Glass", "Frame", "Sill", "Fill", "Mullion", "Transom", "Secret", "Door", "Peeling", "Walnut", "Patch", "Rail", "Window" };

        // 베이크 값: 벽 두께 0.2m·문 폭 약 1m인 건물이라 작은 값을 쓴다
        const float SmallestOccluder = 1f;
        const float SmallestHole = 0.25f;
        const float BackfaceThreshold = 100f;

        [MenuItem("Tools/KSM/Optimization/Occlusion - Set Flags And Bake")]
        public static void SetFlagsAndBake()
        {
            int occluders, occludees;
            SetFlags(out occluders, out occludees);
            StaticOcclusionCulling.smallestOccluder = SmallestOccluder;
            StaticOcclusionCulling.smallestHole = SmallestHole;
            StaticOcclusionCulling.backfaceThreshold = BackfaceThreshold;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Occlusion] 점유물 " + occluders + "개, 피점유물 " + occludees + "개 지정. 백그라운드 베이크를 시작합니다.");
            StaticOcclusionCulling.GenerateInBackground();
        }

        [MenuItem("Tools/KSM/Optimization/Occlusion - Clear Data")]
        public static void ClearData()
        {
            StaticOcclusionCulling.Clear();
            Debug.Log("[Occlusion] 오클루전 데이터를 지웠습니다.");
        }

        // 플래그만 지정 (베이크는 하지 않음). 몇 번을 다시 실행해도 같은 결과다.
        public static void SetFlags(out int occluders, out int occludees)
        {
            occluders = 0; occludees = 0;
            var rw = GameObject.Find("Ravenwood_LGH");
            var occluderSet = new HashSet<GameObject>();
            if (rw != null)
            {
                foreach (var rootPath in OccluderRoots)
                {
                    var root = rw.transform.Find(rootPath);
                    if (root != null) CollectOccluders(root, occluderSet);
                }
                var attic = rw.transform.Find("Attic");
                if (attic != null)
                    foreach (Transform c in attic)
                        if (OccluderAttics.Any(p => c.name.StartsWith(p))) CollectOccluders(c, occluderSet);
            }

            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                var go = r.gameObject;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                var next = flags;
                if (occluderSet.Contains(go)) { next |= StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic; occluders++; }
                // 베이크된(정적) 물건은 피점유물로 — 가려지면 그리지 않는다. 정적이 아닌 물건은 원래 실행 중에 가림 여부를 검사한다.
                else if ((flags & (StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic)) != 0) { next |= StaticEditorFlags.OccludeeStatic; occludees++; }
                if (next == flags) continue;
                GameObjectUtility.SetStaticEditorFlags(go, next);
                if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                EditorUtility.SetDirty(go);
            }
        }

        static void CollectOccluders(Transform root, HashSet<GameObject> set)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled) continue;
                string n = r.name;
                if (Excluded.Any(x => n.Contains(x))) continue;
                // 반투명 재질이 하나라도 있으면 점유물이 아니다
                bool transparent = false;
                foreach (var m in r.sharedMaterials) if (m != null && m.renderQueue >= 3000) { transparent = true; break; }
                if (transparent) continue;
                set.Add(r.gameObject);
            }
        }
    }
}
