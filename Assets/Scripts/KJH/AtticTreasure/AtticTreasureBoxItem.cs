using LGH;
using UnityEngine;

namespace AtticTreasure
{
    // 다락 별 상자 속 물건(황금상, 현관 열쇠) 공통 부분. 공용 들기 시스템(HoldableItem)으로 왼손에 든다.
    // 2026-10-28: 인벤토리에 넣던 방식 → 왼손에 드는 방식으로 변경 (인벤토리 시스템 제거 예정).
    //  - 상자(StarLockBox)가 열려 있고 아직 상자에 있을 때만 E로 집을 수 있다. 손은 하나라서 이미 무언가를 들고 있으면 못 집는다
    //    → 두 사람이 하나씩(한 명은 황금상, 한 명은 현관 열쇠) 드는 게 정상 흐름.
    //  - 누가 들고 있는지는 판정 담당(AtticTreasureSync)이 정하고, 허락이 오면 그 사람 손에 쥐어진다(GiveToLocalHand).
    //    상대 화면에서는 HoldableNetworkSync가 상대 캐릭터 왼손에 보여 준다.
    //  - 들고 있다가 상자 자리를 보고 E("제자리에 두기")를 누르면 상자로 돌아가고, 다른 사람이 다시 집을 수 있다.
    //  - 상자는 LGH 프리팹을 수정하지 않도록 이름으로 찾는다.
    // 모델 교체: 자식 "Visual"(겉모습 프리팹)만 바꾸면 된다. 콜라이더는 이 오브젝트에 있다.
    public abstract class AtticTreasureBoxItem : HoldableItem
    {
        [Tooltip("물건이 들어 있는 상자. 비우면 boxName 이름으로 찾는다")]
        [SerializeField] StarLockBox box;
        [SerializeField] string boxName = "StarLockBox_LGH";
        [SerializeField] string prompt = "[E] 줍기";

        Renderer[] renderers;
        Collider[] ownColliders;
        bool? shownState, colliderState;
        bool suppressReturn;

        protected static AtticTreasureSync Sync { get { return AtticTreasureSync.Instance; } }

        // 판정 담당 기준: 지금 상자 안에 있는가(아무도 안 들고 있음)
        protected abstract bool InBox { get; }
        // 지금 숨겨야 하는가 (예: 황금상 획득 연출 중)
        protected virtual bool Hidden { get { return false; } }
        protected abstract void RequestTake();
        protected abstract void RequestReturn();

        protected override void Awake()
        {
            base.Awake();
            renderers = GetComponentsInChildren<Renderer>(true);
            ownColliders = GetComponentsInChildren<Collider>(true);
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

        // 상대 캐릭터 손에 붙어 있는가 (HoldableNetworkSync가 상대 아바타 왼손 소켓 밑으로 옮겨 둔 상태)
        bool AttachedToOtherHand { get { return transform.parent != null && transform.GetComponentInParent<CoopDemo.CharacterAvatarModel>() != null; } }

        void Update()
        {
            // 보이는 경우: 상자 안에 있음 / 내 손에 있음 / 상대 손에 붙어 있음.
            // 그 밖(상대가 들었는데 상대 손에 보여 줄 수단이 없는 경우 등)은 상자 자리에 남아 보이지 않게 숨긴다 (2026-10-28 수정)
            bool show = !Hidden && (InBox || Location == Where.Held || AttachedToOtherHand);
            if (shownState != show)
            {
                shownState = show;
                foreach (var r in renderers) if (r != null) r.enabled = show;
            }
            // 손에 든 동안에는 HoldableItem이 콜라이더를 관리한다. 그 밖에는 상자 안에 있을 때만 켠다(상대 손에 있을 땐 끔).
            if (Location == Where.Held) { colliderState = null; return; }
            bool col = show && InBox && Location == Where.Home;
            if (colliderState != col)
            {
                colliderState = col;
                foreach (var c in ownColliders) if (c != null) c.enabled = col;
            }
        }

        bool BoxOpen { get { return box == null || box.IsOpen; } }

        protected override bool CanPickUp { get { return Sync != null && InBox && BoxOpen && !Hidden; } }

        public override bool CanInteract
        {
            get { return isActiveAndEnabled && Location == Where.Home && CanPickUp; }
        }

        public override string InteractPrompt
        {
            get
            {
                if (PlayerHand.LocalHeld != null) return DisplayNameForPrompt + "  (손에 다른 물건을 들고 있다)";
                return prompt;
            }
        }

        // 안내 문구용 이름: "[E] 황금상 집기" → "황금상"
        string DisplayNameForPrompt
        {
            get
            {
                string p = prompt ?? "";
                if (p.StartsWith("[E] ")) p = p.Substring(4);
                if (p.EndsWith(" 집기")) p = p.Substring(0, p.Length - 3);
                return string.IsNullOrEmpty(p) ? displayName : p;
            }
        }

        public override string DisplayName { get { return string.IsNullOrEmpty(displayName) || displayName == "물건" ? DisplayNameForPrompt : displayName; } }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract) return;
            if (PlayerHand.LocalHeld != null) { PlayerHand.Say(PlayerHand.Local.handFullMessage); return; }
            RequestTake();
        }

        // 판정 담당이 "이 컴퓨터 플레이어가 가져간다"를 확정했을 때 호출 → 왼손에 쥔다.
        // 그 사이 손이 차 있으면(다른 물건을 집음) 상자로 되돌린다.
        public void GiveToLocalHand()
        {
            if (Location == Where.Held) return;
            if (!PlayerHand.Local.TryTake(this)) RequestReturn();
        }

        // 판정 담당이 "상자로 돌아감"을 확정했는데 내 손에 들려 있으면(예: 연결 문제) 조용히 내려놓는다
        public void TakeBackFromLocalHand()
        {
            if (!IsHeldLocally) return;
            suppressReturn = true;
            PlayerHand.Local.ReturnHeldHome();
            suppressReturn = false;
        }

        // 손에서 놓였다 = "제자리에 두기"로 상자에 돌려놨다 → 판정 담당에게 알린다
        protected override void OnReleased()
        {
            if (!suppressReturn) RequestReturn();
        }
    }
}
