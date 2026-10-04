// 단서를 살펴볼 때 재생되는 "펼침 연출"의 공통 규격.
// 양피지 말기/펴기(ParchmentRoller), 나중에 만들 신문 접기/펴기 등 동작 종류가 달라도 이 인터페이스만 구현하면
// InspectableClue가 자동으로 찾아서 재생해 준다 (Inspector에서 이벤트를 손으로 연결할 필요가 없어짐).
// 규칙: "Reveal(드러내기) = 내용이 보이게 펼치기", "Hide(감추기) = 원래 상태(말림/접힘)로 되돌리기".
public interface IRevealAnimation
{
    // 내용이 보이도록 duration초 동안 펼친다. 카메라 앞에 도착해서 살펴보기가 시작될 때 호출된다.
    void PlayReveal(float duration);

    // 원래 상태(말림/접힘)로 duration초 동안 되돌린다. 내려놓기를 시작할 때 호출된다.
    void PlayHide(float duration);
}
