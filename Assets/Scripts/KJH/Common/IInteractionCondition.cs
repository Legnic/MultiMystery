using UnityEngine;

// "이 대상은 지금 상호작용할 수 있는 조건이 되었나?"를 밝히는 인터페이스.
// 상호작용 대상(IInteractable)과 같은 오브젝트에 이걸 구현한 컴포넌트를 붙이면,
// InteractionController가 조건을 하나라도 만족하지 못한 대상을 "없는 것"으로 취급한다
// (안내 문구·조준 강조·E 반응 모두 없음 — 능력이 없는 플레이어에게 보이는 방식과 같다).
//
// IInteractable의 CanInteract에 넣지 않고 따로 둔 이유: 단서·사진·소리 듣기 스크립트를 하나도 고치지 않고,
// 조건이 필요한 오브젝트에만 컴포넌트를 "붙이는 것"으로 끝내기 위함이다. 여러 개를 함께 붙이면 모두 만족해야 열린다.
// 예: TimeUnlock(정해진 시간대부터 가능). 나중에 "특정 단서를 얻어야 가능" 같은 조건도 같은 방식으로 추가하면 된다.
public interface IInteractionCondition
{
    // 이 플레이어(interactor)가 지금 이 대상과 상호작용할 수 있으면 true.
    bool IsMet(GameObject interactor);
}
