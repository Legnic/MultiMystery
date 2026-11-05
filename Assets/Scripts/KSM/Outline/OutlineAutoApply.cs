using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoopDemo
{
    // 씬이 불러와질 때마다 "눈에 띄어야 하는 물건"을 종류별로 찾아 OutlineTarget을 붙인다.
    // 다른 팀원 폴더의 프리팹·씬·스크립트를 고치지 않고도(머지로 값이 덮여도) 항상 적용되게 하려고 런타임에 붙인다.
    //  - 열쇠들           : 집을 수 있게 되었을 때만 켠다 (숨겨져 있을 땐 꺼 둔다)
    //  - 놀이책·제조법 책·쪽지·온실 기록일지 : 항상 켠다
    // 새 종류를 추가하려면 Apply()에 한 줄만 넣으면 된다.
    public static class OutlineAutoApply
    {
        const float KeyDistance = 25f;
        const float ReadableDistance = 18f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Apply(); }

        public static void Apply()
        {
            // 열쇠: 들기 시스템 열쇠(GIM-002), 다락 별 상자의 현관 열쇠
            AddAll<Gim002.Gim002KeyItem>(OutlineTarget.Mode.WhenInteractable, KeyDistance);
            AddAll<AtticTreasure.AtticTreasureKey>(OutlineTarget.Mode.WhenInteractable, KeyDistance);
            // 열쇠 이름의 LGH 바닥 아이템 (놋쇠 열쇠 등)
            foreach (var w in Object.FindObjectsByType<LGH.WorldItem>(FindObjectsInactive.Include))
            {
                if (w.item == null || !IsKey(w.item)) continue;
                Add(w.gameObject, OutlineTarget.Mode.WhenInteractable, KeyDistance);
            }

            // 읽을거리: 놀이책, 제조법 책·쪽지, 온실 기록일지, LGH 쪽지
            AddAll<Gim002.Gim002PlayBook>(OutlineTarget.Mode.Always, ReadableDistance);
            // 제조법 책·쪽지는 받침대("Stand" 밑)를 빼고 책과 쪽지 종이에만 두른다
            foreach (var s in Object.FindObjectsByType<PotionCraft.ReadableStation>(FindObjectsInactive.Include))
            {
                Add(s.gameObject, OutlineTarget.Mode.Always, ReadableDistance);
                s.GetComponent<OutlineTarget>().SetRenderers(RenderersWithout(s.transform, "Stand"));
            }
            AddAll<Gim004.Gim004Journal>(OutlineTarget.Mode.Always, ReadableDistance);
            AddAll<LGH.PaperNote>(OutlineTarget.Mode.Always, ReadableDistance);
        }

        // root 아래 렌더러 중 이름이 excludedName인 오브젝트(와 그 자식)에 속한 것을 뺀다
        static Renderer[] RenderersWithout(Transform root, string excludedName)
        {
            var list = new System.Collections.Generic.List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                bool excluded = false;
                for (var t = r.transform; t != null && t != root.parent; t = t.parent)
                    if (t.name == excludedName) { excluded = true; break; }
                if (!excluded) list.Add(r);
            }
            return list.ToArray();
        }

        static bool IsKey(LGH.ItemData item)
        {
            string id = ((item.itemId ?? "") + " " + (item.displayName ?? "")).ToLowerInvariant();
            return id.Contains("key") || id.Contains("열쇠");
        }

        static void AddAll<T>(OutlineTarget.Mode mode, float maxDistance) where T : Component
        {
            foreach (var c in Object.FindObjectsByType<T>(FindObjectsInactive.Include)) Add(c.gameObject, mode, maxDistance);
        }

        static void Add(GameObject go, OutlineTarget.Mode mode, float maxDistance)
        {
            var t = go.GetComponent<OutlineTarget>();
            if (t == null) t = go.AddComponent<OutlineTarget>();
            t.Configure(mode, maxDistance);
        }
    }
}
