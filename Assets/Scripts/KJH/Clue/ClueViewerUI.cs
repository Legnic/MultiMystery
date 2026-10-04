using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// 일반 단서(ViewableClue)를 살펴볼 때 화면에 "가운데 이미지 + 오른쪽 설명"을 띄우는 UI.
// 배경은 어둡게 덮고(선택적으로 흐림 Volume도 함께), 글씨가 잘 보이게 한다.
// 이 UI는 씬에 하나만 두고 모든 일반 단서가 같이 쓴다 (단서마다 UI를 따로 만들 필요 없음).
// 사용법: Canvas 아래, CanvasGroup이 붙은 전체 화면 크기 오브젝트에 붙이고 아래 참조를 연결한다.
//        (Tools/KJH/Clue/Create Clue Viewer UI 메뉴로 자동 생성할 수 있다)
public class ClueViewerUI : MonoBehaviour
{
    [Header("UI 참조")]
    [Tooltip("패널 전체의 투명도를 조절하는 CanvasGroup. 평소엔 alpha 0.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("화면 가운데에 단서 이미지를 보여줄 Image.")]
    [SerializeField] private Image clueImage;

    [Tooltip("오른쪽 위에 표시할 단서 이름.")]
    [SerializeField] private Text titleText;

    [Tooltip("오른쪽에 표시할 단서 설명.")]
    [SerializeField] private Text descriptionText;

    [Header("연출")]
    [Tooltip("나타나고 사라지는 데 걸리는 시간 (초). 갑툭튀 느낌이 나지 않게 짧고 부드럽게.")]
    [SerializeField] private float fadeDuration = 0.25f;

    [Tooltip("배경을 흐리게 할 Volume (Inspect_Volume). 비워두면 어둡게 덮기만 한다.")]
    [SerializeField] private Volume backgroundVolume;

    // 지금 패널이 열려 있는지 (닫히는 중이면 false).
    public bool IsOpen { get; private set; }

    private Coroutine fadeRoutine;

    private void Awake()
    {
        // 처음에는 완전히 안 보이고, 화면 클릭도 막지 않게 한다.
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    // 단서 내용을 채우고 서서히 나타나게 한다.
    public void Show(ClueData clue)
    {
        if (clue == null) return;

        if (titleText != null) titleText.text = clue.displayName;
        if (descriptionText != null) descriptionText.text = clue.description;
        if (clueImage != null)
        {
            // 이미지가 없는 단서도 있으니, 없으면 Image 자체를 숨겨서 빈 하얀 사각형이 보이지 않게 한다.
            clueImage.sprite = clue.image;
            clueImage.enabled = clue.image != null;
            clueImage.preserveAspect = true; // 이미지 비율이 찌그러지지 않게
        }

        IsOpen = true;
        FadeTo(1f);
    }

    // 서서히 사라지게 한다.
    public void Hide()
    {
        IsOpen = false;
        FadeTo(0f);
    }

    private void FadeTo(float target)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    private IEnumerator FadeRoutine(float target)
    {
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 0f;
        float startWeight = backgroundVolume != null ? backgroundVolume.weight : 0f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            // 일시정지(timeScale = 0)와 무관하게 연출이 진행되도록 unscaledDeltaTime을 쓴다.
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));
            if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(startAlpha, target, t);
            if (backgroundVolume != null) backgroundVolume.weight = Mathf.Lerp(startWeight, target, t);
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = target;
        if (backgroundVolume != null) backgroundVolume.weight = target;
        fadeRoutine = null;
    }
}
