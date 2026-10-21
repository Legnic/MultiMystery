using UnityEngine;

// 물건의 "원래 자리" 표시. HoldableItem이 Awake에서 자동으로 하나 만든다(씬에 직접 배치할 필요 없음).
// 물건을 손에 든 동안에만 콜라이더가 켜져서, 그 자리를 바라보고 E를 누르면 물건을 원래 자리에 내려놓는다.
// 왜 따로 콜라이더가 필요한가: 물건을 들고 나면 원래 자리에는 아무것도 없어서 플레이어의 시선(레이캐스트)이 맞을 대상이 없기 때문.
public class HoldableHomeSpot : MonoBehaviour, IWorldInteractable
{
    HoldableItem owner;
    BoxCollider box;

    public static HoldableHomeSpot Create(HoldableItem owner, Transform parent, Vector3 localPos, Quaternion localRot, Bounds itemLocalBounds)
    {
        var go = new GameObject(owner.name + "_HomeSpot");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = owner.transform.localScale;   // 물건과 같은 좌표계라 크기를 그대로 쓸 수 있다

        var spot = go.AddComponent<HoldableHomeSpot>();
        spot.owner = owner;
        spot.box = go.AddComponent<BoxCollider>();
        spot.box.center = itemLocalBounds.center;
        // 너무 작으면 조준이 어려우니 조금 키운다
        spot.box.size = Vector3.Max(itemLocalBounds.size * 1.15f, Vector3.one * 0.08f);
        spot.box.enabled = false;
        return spot;
    }

    public void SetActive(bool on) { if (box != null) box.enabled = on; }

    public bool CanInteract { get { return owner != null && owner.IsHeldLocally; } }

    public string InteractPrompt { get { return "[E] " + owner.DisplayName + " 제자리에 두기"; } }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract) return;
        PlayerHand.Local.ReturnHeldHome();
    }
}
