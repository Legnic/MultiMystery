// IInteractable 중에서 "LGH 플레이어(LGH.PlayerInteractor)도 상호작용할 수 있는 것"을 표시하는 표식 인터페이스.
// KJH의 InteractionController는 IInteractable 전부를, LGH PlayerInteractor는 이 인터페이스를 구현한 것만 처리한다.
// (KJH 쪽 단서·열쇠·금고 등은 KJH 전용 플레이어/인벤토리를 전제로 하므로 LGH 플레이어가 건드리지 않게 구분한다.)
public interface IWorldInteractable : IInteractable
{
}
