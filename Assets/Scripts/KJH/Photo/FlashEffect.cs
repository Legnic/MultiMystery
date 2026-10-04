using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 사진 촬영 순간 "흰 화면이 번쩍였다가 서서히 사라지는" 플래시 연출을 담당한다.
// 사용법: Canvas 아래, 화면 전체를 덮는 흰색 Image 오브젝트에 이 스크립트를 붙인다.
public class FlashEffect : MonoBehaviour
{
    // ── Inspector에서 설정하는 값들 ─────────────────────────────
    [Tooltip("화면 전체를 덮는 흰색 Image. 평소에는 알파값 0으로 숨겨져 있다가 촬영 순간만 보인다.")]
    [SerializeField] private Image flashImage;

    [Tooltip("플래시가 최고 밝기에서 완전히 투명해질 때까지 걸리는 시간 (초). 기획서 기준 0.8초.")]
    [SerializeField] private float fadeOutDuration = 0.8f;

    // 셔터음은 PhotoCaptureSystem이 AudioSource로 재생하므로 여기서는 다루지 않는다.
    // (연출(시각)과 사운드 재생 책임을 스크립트별로 나눠서, 한쪽만 고쳐도 다른 쪽이 안 깨지게 한다)

    private Coroutine flashRoutine; // 중복 촬영 시 이전 플래시 코루틴을 멈추기 위해 참조를 들고 있는다.

    private void Awake()
    {
        if (flashImage == null)
        {
            // Inspector에 연결을 깜빡했을 때 바로 알아챌 수 있도록 경고를 남긴다.
            Debug.LogWarning("[FlashEffect] flashImage가 연결되지 않았습니다. Inspector에서 흰색 Image를 연결하세요.");
            return;
        }

        // 시작할 때는 완전히 투명해야 한다 (평소엔 안 보이는 상태).
        SetAlpha(0f);
    }

    // PhotoCaptureSystem이 촬영 순간 이 메서드를 호출한다.
    public void Play()
    {
        if (flashImage == null) return;

        // 연속 촬영 등으로 이전 플래시가 아직 사라지는 중이면 멈추고 새로 시작한다.
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // 1단계: 순간적으로 완전히 흰 화면(알파 1)으로 번쩍인다.
        SetAlpha(1f);

        // 2단계: fadeOutDuration 동안 알파를 1 -> 0으로 서서히 줄인다.
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            // Time.deltaTime을 더해가며 진행도를 계산해야 프레임 속도와 무관하게 같은 시간이 걸린다.
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);

            // 1에서 0으로 선형 보간 (처음엔 빠르게, 끝에는 서서히 느껴지도록 Lerp 자체가 매끄럽게 처리해준다).
            SetAlpha(Mathf.Lerp(1f, 0f, t));

            yield return null; // 다음 프레임까지 대기
        }

        SetAlpha(0f);
        flashRoutine = null;
    }

    private void SetAlpha(float alpha)
    {
        Color c = flashImage.color;
        c.a = alpha;
        flashImage.color = c;
    }
}
