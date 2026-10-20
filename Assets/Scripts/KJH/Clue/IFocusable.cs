// 조준점(화면 가운데)이 자신을 가리키기 시작하거나 벗어났을 때 알림을 받고 싶은 상호작용 대상이 구현하는 인터페이스.
// 예: InspectableClue는 이 알림을 받아 은은하게 밝아지는 강조 표시를 켜고 끈다.
// IInteractable과 따로 둔 이유: PhotoSpot처럼 트리거 방식으로만 쓰는 대상은 이 알림이 필요 없어서,
// 기존 코드를 고치지 않고도 "필요한 대상만" 골라서 구현할 수 있게 하기 위함이다.
public interface IFocusable
{
    // InteractionController의 레이캐스트가 이 대상을 새로 가리키기 시작했을 때 한 번 호출된다.
    void OnFocusEnter();

    // 조준이 다른 곳으로 벗어났거나, 거리가 멀어졌거나, 다른 상호작용이 시작되어 감지가 멈췄을 때 한 번 호출된다.
    void OnFocusExit();
}
