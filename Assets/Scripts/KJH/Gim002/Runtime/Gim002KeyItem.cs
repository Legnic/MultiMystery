using UnityEngine;

namespace Gim002
{
    // GIM-001 보상 열쇠 (1층 액자 뒤 음악실 열쇠 / 2층 장난감 상자 속 태엽 열쇠).
    // LGH 공용 소지품 인벤토리(WorldItem + PlayerInventory)로 줍는다 — 보기+E로 인벤토리에 들어가고, 손에 들고 다니지 않는다.
    // 같은 오브젝트의 WorldItem이 "보기+E로 줍기·인벤토리 반영"을 전담하고, 여기서는 퍼즐 판정에 필요한 정보(keyData)만 들고
    // CanPickUp(아직 GIM-001 전이라 못 줍는 상태)을 콜라이더로 표현한다.
    //  - GIM-001이 풀리기 전(LOCKED)에는 집을 수 없다 (액자가 아직 안 떨어졌고 상자도 닫혀 있으므로) → 콜라이더를 꺼서 조준 자체가 안 되게 한다.
    //  - 사용(Gim002KeyTarget.Interact)은 인벤토리에서 이 열쇠를 1개 소비한다. 이미 쓴 열쇠는 그 전에 인벤토리/월드에서 이미 사라져 있다.
    [RequireComponent(typeof(LGH.WorldItem))]
    public class Gim002KeyItem : MonoBehaviour
    {
        [Tooltip("이 열쇠의 정보(이름·문양·인벤토리 아이템)")]
        [SerializeField] Gim002KeyData keyData;
        [Tooltip("어디에 쓰는 열쇠인지 (참고용 — 실제 사용 가능 여부는 Gim002KeyTarget이 판단)")]
        [SerializeField] Gim002KeyTarget.TargetKind usedFor;

        public Gim002KeyData KeyData { get { return keyData; } }
        public Gim002KeyTarget.TargetKind UsedFor { get { return usedFor; } }

        static Gim002Controller C { get { return Gim002Controller.Instance; } }

        LGH.WorldItem worldItem;
        Collider[] colliders;

        void Awake()
        {
            worldItem = GetComponent<LGH.WorldItem>();
            colliders = GetComponentsInChildren<Collider>(true);
            if (keyData != null && keyData.inventoryItem != null) worldItem.item = keyData.inventoryItem;
        }

        void Update()
        {
            // 아직 GIM-001이 안 풀렸으면 조준 자체가 안 되게 콜라이더를 꺼 둔다 (WorldItem이 이미 숨긴 상태면 그대로 둔다).
            bool canPick = worldItem.IsPresent && C != null && C.State.state != Gim002State.Locked;
            foreach (var c in colliders) if (c != null) c.enabled = canPick;
        }
    }
}
