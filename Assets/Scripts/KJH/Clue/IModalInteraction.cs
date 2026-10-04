// "한동안 화면을 독차지하는" 상호작용(예: 단서를 눈앞에 들고 살펴보기)이 구현하는 인터페이스.
// 이 상호작용이 진행 중인 동안 InteractionController는
//   - 바라보기 감지와 안내 문구 갱신을 멈추고,
//   - E(Interact)와 Esc(취소) 입력을 다른 대상이 아니라 이 상호작용에게 그대로 전달한다.
// 이렇게 해두면 InteractionController가 "살펴보기"라는 기능을 구체적으로 몰라도 되고,
// 나중에 서랍 열어보기 같은 비슷한 기능이 생겨도 이 인터페이스만 구현하면 같은 방식으로 붙는다.
// 시작/종료 알림은 InteractionController.BeginModal / EndModal로 한다.
public interface IModalInteraction
{
    // 진행 중에 E(Player/Interact)가 눌렸을 때 호출된다.
    void OnInteractPressed();

    // 진행 중에 Esc(Photo/ClosePhoto)가 눌렸을 때 호출된다.
    void OnCancelPressed();
}
