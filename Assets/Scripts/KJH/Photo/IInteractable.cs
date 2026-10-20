using UnityEngine;

// 플레이어가 "E"키로 상호작용할 수 있는 모든 것(사진 촬영 지점, 나중의 서랍/상자 등)이
// 공통으로 구현하는 인터페이스. InteractionController는 이 인터페이스만 알면 되고,
// 실제로 무엇을 하는 오브젝트인지는 몰라도 된다 (다형성).
public interface IInteractable
{
    // 지금 상호작용 가능한 상태인지. false면 범위 안에 들어와도 안내 문구가 뜨지 않고,
    // E를 눌러도 아무 일도 일어나지 않는다 (예: 이미 촬영을 끝낸 PhotoSpot).
    bool CanInteract { get; }

    // 화면에 띄울 안내 문구 (예: "[E] 사진 찍기"). CanInteract가 false일 땐 이 값이 안 쓰인다.
    string InteractPrompt { get; }

    // 실제 상호작용 실행. interactor는 상호작용을 시도한 주체(보통 Player GameObject)로,
    // 필요하면 그 오브젝트에 붙은 다른 컴포넌트(PhotoCaptureSystem 등)를 찾아 쓸 수 있다.
    void Interact(GameObject interactor);
}
