using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 촬영 직후 "과거 사진을 얻었다" 같은 짧은 안내를 화면에 띄웠다가 자동으로 사라지는 토스트 UI.
// 사용법: Canvas 아래, CanvasGroup이 붙은 오브젝트에 붙인다 (그 안에 Text 자식 하나 필요).
public class PhotoAcquiredToast : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text messageText;

    [Header("타이밍")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [Tooltip("완전히 보인 상태로 유지되는 시간 (초).")]
    [SerializeField] private float holdDuration = 1.2f;
    [SerializeField] private float fadeOutDuration = 0.4f;

    private Coroutine routine;

    private void Awake()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    // PhotoCaptureSystem이 촬영에 성공했을 때 호출한다.
    public void Show(PhotoItemData item)
    {
        if (messageText != null)
        {
            messageText.text = $"과거 사진을 얻었다: {item.title}";
        }

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        yield return Fade(0f, 1f, fadeInDuration);
        yield return new WaitForSeconds(holdDuration);
        yield return Fade(1f, 0f, fadeOutDuration);
        routine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
