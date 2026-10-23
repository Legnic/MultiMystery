using LGH;
using UnityEngine;

namespace AtticTreasure
{
    // 다락 별 상자 속 물건(황금상, 현관 열쇠) 공통 부분.
    //  - 상자(StarLockBox)가 열려 있고 아직 상자에 있을 때만 E로 집을 수 있다.
    //  - 상자에 없으면(누가 가져감) 모든 컴퓨터에서 겉모습·콜라이더를 끈다.
    //  - 상자는 LGH 프리팹을 수정하지 않도록 이름으로 찾는다.
    // 모델 교체: 자식 "Visual"(겉모습 프리팹)만 바꾸면 된다. 콜라이더는 이 오브젝트에 있다.
    public abstract class AtticTreasureBoxItem : MonoBehaviour, IWorldInteractable
    {
        [Tooltip("물건이 들어 있는 상자. 비우면 boxName 이름으로 찾는다")]
        [SerializeField] StarLockBox box;
        [SerializeField] string boxName = "StarLockBox_LGH";
        [SerializeField] string prompt = "[E] 줍기";

        Renderer[] renderers;
        Collider[] colliders;
        bool shown = true;

        protected static AtticTreasureSync Sync { get { return AtticTreasureSync.Instance; } }

        // 지금 상자 안에 있는가 (Sync 상태 기준)
        protected abstract bool InBox { get; }
        protected abstract void RequestTake();

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            if (box == null) box = FindBox(boxName, transform.position);
        }

        // 이름이 같은 상자가 여러 개면 가장 가까운 것
        static StarLockBox FindBox(string name, Vector3 near)
        {
            StarLockBox best = null;
            float bestD = float.MaxValue;
            foreach (var b in FindObjectsByType<StarLockBox>(FindObjectsInactive.Include))
            {
                if (!string.IsNullOrEmpty(name) && b.name != name) continue;
                float d = (b.transform.position - near).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        void Update()
        {
            bool show = Sync == null || InBox;
            if (show == shown) return;
            shown = show;
            foreach (var r in renderers) if (r != null) r.enabled = show;
            foreach (var c in colliders) if (c != null) c.enabled = show;
        }

        bool BoxOpen { get { return box == null || box.IsOpen; } }

        public bool CanInteract { get { return Sync != null && InBox && BoxOpen; } }
        public string InteractPrompt { get { return prompt; } }

        public void Interact(GameObject interactor)
        {
            if (CanInteract) RequestTake();
        }
    }
}
