// "이 상호작용은 특정 능력을 가진 플레이어만 할 수 있다"를 밝히는 인터페이스.
// IInteractable과 함께 붙인다. InteractionController가 이걸 보고, 능력이 없는 플레이어에게는
// 안내 문구를 띄우지 않고 E 입력에도 반응하지 않는다 (= 그 플레이어에게는 없는 대상처럼 보인다).
//
// IInteractable에 바로 넣지 않고 따로 둔 이유: 단서처럼 누구나 쓰는 대상은 이걸 구현하지 않으면 되므로,
// 기존 상호작용 코드를 하나도 고치지 않고 "능력이 필요한 대상"에만 골라서 붙일 수 있다.
// 예: ObjectEchoTarget → SoundEcho, PhotoSpot → PastPhoto
public interface IRequiresAbility
{
    // 이 대상과 상호작용하는 데 필요한 능력.
    PlayerAbility RequiredAbility { get; }
}
