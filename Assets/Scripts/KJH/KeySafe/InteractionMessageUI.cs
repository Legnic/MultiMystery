using UnityEngine;
using UnityEngine.UI;

// "알맞은 열쇠가 없는 것 같다", "서재 열쇠를 얻었다" 같은 짧은 혼잣말 문구를 화면 하단에 잠깐 띄웠다가
// 천천히 사라지게 하는 UI. 상호작용한 플레이어 본인 화면에만 뜨는 로컬 전용 UI라 동기화하지 않는다.
// 톤: 경고창처럼 튀지 않게 연한 회백색·작은 글씨, 0.3초 페이드인 / 유지 / 0.6초 페이드아웃.
// 같은 문구가 떠 있는 동안 다시 Show가 오면 새로 깜빡이지 않고 "유지 시간"만 처음부터 다시 센다.
// 사용법: Canvas 아래, CanvasGroup이 붙은 오브젝트에 붙이고 자식 Text를 연결한다.
public class InteractionMessageUI : MonoBehaviour
{
    // 지금 문구가 어느 단계에 있는지. 코루틴 대신 Update에서 단계를 진행시켜 "타이머만 리셋"을 간단하게 처리한다.
    private enum Phase { Hidden, FadingIn, Holding, FadingOut }

    [Header("UI 참조")]
    [Tooltip("문구 전체의 투명도를 조절하는 CanvasGroup. 비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("문구를 표시할 Text.")]
    [SerializeField] private Text messageText;

    [Header("타이밍 (초)")]
    [Tooltip("나타나는 시간. 갑툭튀 느낌이 나지 않게 짧은 페이드를 준다.")]
    [SerializeField] private float fadeInDuration = 0.3f;

    [Tooltip("Show에 시간을 따로 주지 않았을 때 완전히 보인 채 유지되는 시간.")]
    [SerializeField] private float defaultHoldDuration = 2f;

    [Tooltip("사라지는 시간. 나타날 때보다 길게 해서 여운이 남게 한다.")]
    [SerializeField] private float fadeOutDuration = 0.6f;

    private Phase phase = Phase.Hidden;
    // Holding 단계에서 남은 유지 시간 (초).
    private float holdRemaining;
    // 지금 떠 있는 문구. 같은 문구가 다시 오면 중복 표시하지 않고 타이머만 리셋하기 위해 기억한다.
    private string currentMessage;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        // 처음엔 완전히 숨기고, 화면 클릭도 막지 않게 한다.
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    // 기본 유지 시간(약 2초)으로 문구를 띄운다.
    public void Show(string message)
    {
        Show(message, defaultHoldDuration);
    }

    // 문구를 seconds초 동안 유지했다가 페이드아웃한다.
    public void Show(string message, float seconds)
    {
        if (canvasGroup == null || string.IsNullOrEmpty(message)) return;

        bool sameMessageVisible = message == currentMessage && (phase == Phase.FadingIn || phase == Phase.Holding);

        currentMessage = message;
        if (messageText != null) messageText.text = message;
        holdRemaining = Mathf.Max(0f, seconds);

        // 같은 문구가 이미 나타나는 중/떠 있는 중이면 단계는 그대로 두고 유지 시간만 다시 센다 (연타해도 깜빡이지 않음).
        if (sameMessageVisible) return;

        // 숨겨져 있거나 사라지는 중이었다면 지금 투명도에서부터 다시 나타나게 한다 (0으로 뚝 떨어뜨리지 않음).
        // 다른 문구가 떠 있던 중이라면 글자만 바뀌고 계속 보인다.
        if (phase == Phase.Hidden || phase == Phase.FadingOut) phase = Phase.FadingIn;
    }

    // 즉시가 아니라 페이드아웃으로 숨긴다 (예: 다른 UI가 열릴 때).
    public void Hide()
    {
        if (phase != Phase.Hidden) phase = Phase.FadingOut;
    }

    private void Update()
    {
        if (canvasGroup == null || phase == Phase.Hidden) return;

        // 일시정지(timeScale = 0) 중에도 문구가 멈추지 않도록 unscaledDeltaTime을 쓴다.
        float dt = Time.unscaledDeltaTime;

        switch (phase)
        {
            case Phase.FadingIn:
                // 지속 시간이 0이면 나눗셈 대신 바로 1로 (0으로 나누기 방지).
                canvasGroup.alpha = fadeInDuration > 0f ? Mathf.MoveTowards(canvasGroup.alpha, 1f, dt / fadeInDuration) : 1f;
                if (canvasGroup.alpha >= 1f) phase = Phase.Holding;
                break;

            case Phase.Holding:
                holdRemaining -= dt;
                if (holdRemaining <= 0f) phase = Phase.FadingOut;
                break;

            case Phase.FadingOut:
                canvasGroup.alpha = fadeOutDuration > 0f ? Mathf.MoveTowards(canvasGroup.alpha, 0f, dt / fadeOutDuration) : 0f;
                if (canvasGroup.alpha <= 0f)
                {
                    phase = Phase.Hidden;
                    currentMessage = null; // 완전히 사라졌으니 다음 같은 문구는 새로 나타나게 한다
                }
                break;
        }
    }
}
