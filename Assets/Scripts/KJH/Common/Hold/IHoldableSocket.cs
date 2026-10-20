// "물건을 꽂아 두는 자리"(예: GIM-002 인형 꽂이, 카드 슬롯)가 구현하는 인터페이스. 공용 들기 시스템의 일부.
// 물건이 꽂혀 있으면 플레이어의 시선(레이캐스트)은 자리 대신 물건에 맞는다.
// 그래서 꽂힌 물건(HoldableItem)은 E 입력을 자기가 처리하지 않고 이 인터페이스로 자리에게 넘긴다 → 빼기/바꾸기 규칙은 자리가 정한다.
public interface IHoldableSocket
{
    // 꽂힌 물건(placed)을 바라볼 때 안내 문구를 띄울지
    bool CanUsePlaced(HoldableItem placed);

    // 꽂힌 물건을 바라볼 때의 안내 문구 (예: "[E] 빼기", "단단히 고정되어 있다")
    string PromptForPlaced(HoldableItem placed);

    // 꽂힌 물건을 바라보고 E를 눌렀을 때 (빈손이면 빼기, 다른 걸 들고 있으면 바꾸기 등)
    void UsePlaced(HoldableItem placed);
}
