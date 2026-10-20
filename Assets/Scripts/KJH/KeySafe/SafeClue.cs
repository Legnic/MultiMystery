using System;
using UnityEngine;

// 금고 안에 들어 있는 단서(쪽지)를 "금고가 열리기 전에는 완전히 없는 것"으로 만드는 컴포넌트.
// 단서를 보여주는 기능 자체는 기존 ViewableClue + ClueViewerUI를 그대로 쓰고, 이 컴포넌트는 "언제 볼 수 있는가"만 담당한다.
// 이중 차단:
//   ① 콜라이더 비활성화 → 조준 레이캐스트에 아예 잡히지 않음
//   ② IInteractionCondition → 혹시 콜라이더가 켜져 있어도 InteractionController가 프롬프트·E 입력을 막음
// 금고가 열리면(LockedSafe.OnOpened) 콜라이더를 켜고, 이후에는 몇 번이든 다시 볼 수 있다(추리 게임 특성상 재확인 필요).
// 처음 읽었을 때 OnClueRead 이벤트를 한 번 발생시켜, 나중에 단서수첩 같은 기능이 구독할 수 있게 해 둔다.
// 사용법: 금고의 자식인 단서 오브젝트(ViewableClue가 붙은 곳)에 함께 붙인다. safe를 비워두면 부모에서 LockedSafe를 찾는다.
[RequireComponent(typeof(ViewableClue))]
public class SafeClue : MonoBehaviour, IInteractionCondition
{
    [Tooltip("이 단서를 담고 있는 금고. 비워두면 부모 오브젝트에서 자동으로 찾는다.")]
    [SerializeField] private LockedSafe safe;

    // 처음 읽었을 때 한 번만 발생하는 이벤트 (단서 데이터를 함께 넘긴다).
    public event Action<ClueData> OnClueRead;

    // 이미 한 번 읽었는지. 다시 열람은 가능하지만 OnClueRead는 한 번만 발생시키기 위해 기억한다.
    public bool HasBeenRead { get; private set; }

    private ViewableClue viewableClue;
    // 금고가 닫혀 있는 동안 꺼 둘 콜라이더들 (단서 본체나 자식 어디에 있어도 모두 잡는다).
    private Collider[] clueColliders;

    private void Awake()
    {
        // GetComponent 계열은 Awake에서 한 번만 해 둔다.
        viewableClue = GetComponent<ViewableClue>();
        if (safe == null) safe = GetComponentInParent<LockedSafe>();
        // 꺼져 있는 콜라이더도 포함해서 찾는다 (프리팹에서 처음부터 꺼 둔 상태일 수 있으므로).
        clueColliders = GetComponentsInChildren<Collider>(true);

        if (safe == null) Debug.LogWarning("[SafeClue] 연결된 LockedSafe가 없어 이 단서는 계속 잠긴 상태로 남습니다.", this);
    }

    private void OnEnable()
    {
        if (safe != null) safe.OnOpened += HandleSafeOpened;
        if (viewableClue != null) viewableClue.Opened += HandleClueOpened;

        // 시작 시점의 금고 상태에 맞춰 콜라이더를 켜고 끈다 (이미 열린 금고라면 바로 켜짐).
        SetCollidersEnabled(IsMet(null));
    }

    private void OnDisable()
    {
        if (safe != null) safe.OnOpened -= HandleSafeOpened;
        if (viewableClue != null) viewableClue.Opened -= HandleClueOpened;
    }

    // ── IInteractionCondition ─────────────────────────────
    // 금고가 완전히 열렸을 때만 상호작용 가능 (Opening 중에도 아직 불가).
    public bool IsMet(GameObject interactor)
    {
        return safe != null && safe.IsOpened;
    }

    private void HandleSafeOpened(LockedSafe openedSafe)
    {
        SetCollidersEnabled(true);
    }

    // ViewableClue가 열람 UI를 띄울 때마다 호출된다. 처음 한 번만 OnClueRead를 발생시킨다.
    private void HandleClueOpened(ViewableClue clue)
    {
        if (HasBeenRead) return;
        HasBeenRead = true;
        OnClueRead?.Invoke(clue.ClueData);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (clueColliders == null) return;
        foreach (Collider col in clueColliders)
        {
            if (col != null) col.enabled = enabled;
        }
    }
}
