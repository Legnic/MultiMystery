using System;
using UnityEngine;

// 지금 게임 안이 어느 시간대(정오/저녁/자정)인지 관리하는 컴포넌트 (씬에 하나만 둔다).
// "시간대 바꾸기"는 반드시 이 클래스의 SetPhase 한 곳에서만 일어나게 한다.
// 이유: ① 시간이 흐르는 방식(타이머·이야기 진행 등)은 다른 곳에서 만들 예정이라, 그쪽은 SetPhase만 부르면 되도록
//       ② 나중에 2인 협동 네트워크를 붙일 때 이 한 곳에서만 시간대를 상대와 맞추면 되도록 하기 위함이다.
// 지금은 테스트용으로 Inspector의 버튼(플레이 중에도 동작)으로 직접 넘긴다.
// 사용법: 빈 오브젝트(예: GameClock)에 붙이고 Start Phase를 고른다.
public class GameClock : MonoBehaviour
{
    [Tooltip("게임을 시작할 때의 시간대.")]
    [SerializeField] private TimePhase startPhase = TimePhase.Noon;

    // 시간대가 바뀌었을 때 알려주는 이벤트 (조명·소리 변화 등 나중에 만들 기능이 여기에 구독하면 된다).
    public event Action<TimePhase> OnPhaseChanged;

    public TimePhase CurrentPhase { get; private set; }

    // 씬의 GameClock. 단서마다 FindAnyObjectByType으로 찾지 않도록 한 곳에 기억해 둔다.
    public static GameClock Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameClock] 씬에 GameClock이 두 개 이상 있습니다. 하나만 남겨 주세요.", this);
            return;
        }
        Instance = this;
        CurrentPhase = startPhase;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 시간대를 바꾼다. 같은 시간대로 바꾸면 아무 일도 하지 않는다.
    public void SetPhase(TimePhase phase)
    {
        if (phase == CurrentPhase) return;
        CurrentPhase = phase;
        Debug.Log($"[GameClock] 시간대 변경: {phase.ToKorean()}", this);
        OnPhaseChanged?.Invoke(phase);
    }

    // 다음 시간대로 넘긴다. 마지막(자정)이면 그대로 둔다.
    public void NextPhase()
    {
        int next = (int)CurrentPhase + 1;
        if (Enum.IsDefined(typeof(TimePhase), next)) SetPhase((TimePhase)next);
    }

    // 지금이 phase 이후(같은 시간대 포함)인지. TimeUnlock이 "~부터 가능"을 판단할 때 쓴다.
    // GameClock이 없는 씬에서는 시간 제한을 걸지 않는다(true) — 시간 기능이 없는 테스트 씬에서도 단서가 막히지 않도록.
    public static bool IsAtOrAfter(TimePhase phase)
    {
        return Instance == null || Instance.CurrentPhase >= phase;
    }
}
