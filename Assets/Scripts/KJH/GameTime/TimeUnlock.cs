using UnityEngine;

// "이 대상은 정해진 시간대부터 상호작용할 수 있다"를 정하는 조건 컴포넌트.
// 그 시간대가 되기 전에는 이 대상이 플레이어에게 "없는 것"처럼 보인다 (안내 문구·조준 강조·E 반응 모두 없음).
// 한 번 열린 뒤에는 시간이 더 흘러도 다시 잠기지 않는다 → 이미 얻은 단서도 계속 다시 볼 수 있다.
// (소리 듣기처럼 1회용 대상이 다시 안 되는 것은 그 대상 자체의 규칙이고, 이 컴포넌트와는 상관없다)
//
// 단서(ViewableClue/InspectableClue)·사진 촬영 지점·소리 듣기 사물 어디에든 붙일 수 있다. 그 스크립트들은 고칠 필요 없다.
// 사용법: 상호작용 대상 스크립트가 붙은 오브젝트에 Add Component → TimeUnlock, Available From을 고른다.
public class TimeUnlock : MonoBehaviour, IInteractionCondition
{
    [Tooltip("이 시간대부터 상호작용할 수 있다 (그 뒤 시간대에도 계속 가능).")]
    [SerializeField] private TimePhase availableFrom = TimePhase.Noon;

    public TimePhase AvailableFrom => availableFrom;

    public bool IsMet(GameObject interactor) => GameClock.IsAtOrAfter(availableFrom);
}
