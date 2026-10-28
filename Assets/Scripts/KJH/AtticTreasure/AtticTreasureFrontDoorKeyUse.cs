using LGH;
using UnityEngine;

namespace AtticTreasure
{
    // 현관문 한 짝에 런타임으로 붙는다 (AtticTreasureFrontDoor.Start가 붙임 — 씬·LGH 프리팹에 직접 두지 않는다).
    // 현관 열쇠를 왼손에 든 사람이 문을 보면 "[E] 현관 열쇠로 열기" → AtticTreasureSync가 확인 후 연다.
    // 열쇠가 없으면 CanInteract가 false라서 LGH 문 처리(잠김 문구)가 그대로 동작한다. (Gim002KeyTarget과 같은 방식)
    public class AtticTreasureFrontDoorKeyUse : MonoBehaviour, IWorldInteractable
    {
        InteractableDoor door;

        public void Init(InteractableDoor d) { door = d; }

        static bool HoldingKey { get { return PlayerHand.LocalHeld is AtticTreasureKey; } }

        public bool CanInteract { get { return door != null && door.locked && HoldingKey && AtticTreasureSync.Instance != null; } }

        public string InteractPrompt { get { return "[E] 현관 열쇠로 열기"; } }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract) return;
            Transform player = PlayerInteractor.Active != null ? PlayerInteractor.Active.transform : interactor.transform;
            AtticTreasureSync.Instance.RequestOpenFrontDoor(player.position);
        }
    }
}
