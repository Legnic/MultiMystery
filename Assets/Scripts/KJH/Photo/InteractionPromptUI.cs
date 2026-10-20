using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면 아래쪽에 "[E] 사진 찍기" 같은 짧은 상호작용 안내 문구를 페이드로 보여주는 UI.
// 사용법: Canvas 아래, CanvasGroup이 붙은 오브젝트에 붙인다 (그 안에 Text 자식 하나 필요).
public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text promptText;
    [Tooltip("문구가 나타나고 사라질 때 걸리는 시간 (초). 너무 빠르면 갑툭튀 느낌이 나서 짧게 유지한다.")]
    [SerializeField] private float fadeDuration = 0.15f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void Show(string text)
    {
        if (promptText != null) promptText.text = text;
        FadeTo(1f);
    }

    public void Hide()
    {
        FadeTo(0f);
    }

    private void FadeTo(float target)
    {
        if (canvasGroup == null) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    private IEnumerator FadeRoutine(float target)
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = target;
        fadeRoutine = null;
    }
}
