using UnityEngine;

// IWorldInteractable 중에서 E 말고 R로도 다른 동작을 하는 것 (예: 2층 혼합 통 = E 넣기 / R 비우기).
// LGH PlayerInteractor는 이것을 보고 있고 CanSecondaryInteract가 true일 때만 R을 여기로 보내고, 그 외에는 원래대로 양초 바꾸기에 쓴다.
public interface IWorldSecondaryInteractable : IWorldInteractable
{
    bool CanSecondaryInteract { get; }
    void SecondaryInteract(GameObject interactor);
}
