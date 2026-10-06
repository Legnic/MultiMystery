using System.Collections;
using UnityEngine;

// 연출용 "소리 스펙트럼" 표시. 화면 하단 중앙에 잉크 펜 선 하나가 소리에 맞춰 일렁인다.
// 재생 중인 AudioSource를 넘겨받아 매 프레임 주파수 분석(GetSpectrumData)을 하고,
// 그 결과를 "선의 출렁임 크기"로 바꿔 InkWaveformGraphic에 넘긴다.
//   - 나타날 때 0.5초 페이드인, 사라질 때 1초 페이드아웃 (갑툭튀 없이)
//   - 사라지는 동안 선이 서서히 잔잔해져서 "펜이 멈추는" 느낌으로 끝난다
//
// 왜 막대그래프가 아니라 선인가: 1890년대 배경에 디지털 이퀄라이저 막대는 어울리지 않아서,
// 지진계/필기 기록계처럼 펜이 종이 위에 긋는 선으로 표현했다.
// 출렁임 계산: 선의 가로 위치마다 낮은음(왼쪽) ~ 높은음(오른쪽) 주파수 하나를 맡기고,
//              그 주파수의 세기만큼 천천히 흐르는 사인파의 높이를 키운다.
//
// 사용법: Canvas 아래, CanvasGroup이 붙은 오브젝트(하단 중앙 배치)에 붙이고 자식 InkWaveformGraphic을 연결한다.
//         보통 직접 호출하지 않고 SoundCue가 Show/Hide를 대신 불러준다.
public class SpectrumOverlay : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private InkWaveformGraphic waveform;

    [Header("페이드")]
    [Tooltip("나타나는 시간 (초).")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [Tooltip("소리가 끝난 뒤 사라지는 시간 (초).")]
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("분석")]
    [Tooltip("선을 이루는 점 개수. 많을수록 부드럽지만 조금 더 무겁다.")]
    [SerializeField] private int pointCount = 96;

    [Tooltip("주파수 분석 크기 (2의 거듭제곱: 256/512/1024...). 클수록 촘촘하게 분석한다.")]
    [SerializeField] private int spectrumSize = 512;

    [Tooltip("표시할 가장 낮은/높은 주파수 (Hz). 사람 귀에 의미 있는 대역만 쓴다.")]
    [SerializeField] private float minFrequency = 60f;
    [SerializeField] private float maxFrequency = 6000f;

    [Tooltip("분석 값 → 선 높이 배율. 소리가 너무 작게/크게 보이면 조절한다. (SoundCue의 강도와 곱해짐)")]
    [SerializeField] private float gain = 14f;

    [Tooltip("선이 커질 때/작아질 때 따라가는 속도. 작을수록 느리고 부드럽다 (절제된 톤을 위해 낮게 둠).")]
    [SerializeField] private float riseSpeed = 8f;
    [SerializeField] private float fallSpeed = 3f;

    [Header("선 출렁임")]
    [Tooltip("선 전체에 들어가는 물결 수.")]
    [SerializeField] private float waveCycles = 7f;
    [Tooltip("물결이 옆으로 흐르는 속도 (라디안/초). 느리게 흘러야 차분하다.")]
    [SerializeField] private float waveFlowSpeed = 1.2f;
    [Tooltip("소리가 없어도 남는 아주 작은 떨림 (손으로 그은 선 느낌). 0이면 완전한 직선.")]
    [SerializeField] private float idleWobble = 0.02f;

    private AudioSource source;
    private float intensity = 1f;
    private float[] spectrum;
    private float[] levels;   // 점마다 부드럽게 따라가는 현재 세기
    private float[] heights;  // 실제로 그릴 높이 (-1 ~ 1)
    private float phase;
    private Coroutine fadeRoutine;

    // 지금 화면에 보이는 중인지 (페이드아웃 중이면 false).
    public bool IsShowing { get; private set; }

    private void Awake()
    {
        // 분석 크기는 2의 거듭제곱이어야 하므로, 잘못 넣어도 가장 가까운 값으로 바로잡는다.
        spectrumSize = Mathf.Clamp(Mathf.ClosestPowerOfTwo(spectrumSize), 64, 8192);
        spectrum = new float[spectrumSize];
        levels = new float[Mathf.Max(2, pointCount)];
        heights = new float[levels.Length];
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false; // 연출 표시일 뿐이니 마우스 입력을 막지 않는다
            canvasGroup.interactable = false;
        }
    }

    // 소리 재생이 시작될 때 부른다. intensity는 사물별 "스펙트럼 표시 강도" (1 = 기본).
    public void Show(AudioSource audioSource, float spectrumIntensity = 1f)
    {
        source = audioSource;
        intensity = Mathf.Max(0f, spectrumIntensity);
        IsShowing = true;
        FadeTo(1f, fadeInDuration);
    }

    // 소리가 끝났을 때 부른다. 1초 동안 선이 잔잔해지며 사라진다.
    public void Hide()
    {
        IsShowing = false;
        FadeTo(0f, fadeOutDuration);
    }

    private void Update()
    {
        // 완전히 사라진 상태면 계산을 건너뛴다.
        if (canvasGroup != null && canvasGroup.alpha <= 0f && !IsShowing) return;

        float dt = Time.unscaledDeltaTime;
        bool hasSound = IsShowing && source != null && source.isPlaying;
        if (hasSound) source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        // 한 칸(bin)이 몇 Hz인지: 전체 대역(샘플레이트의 절반)을 칸 수로 나눈 값.
        float binHz = AudioSettings.outputSampleRate * 0.5f / spectrumSize;
        phase += waveFlowSpeed * dt;

        int n = levels.Length;
        for (int i = 0; i < n; i++)
        {
            float x01 = i / (float)(n - 1);
            float target = 0f;
            if (hasSound)
            {
                // 사람 귀는 주파수를 로그 단위로 느끼므로, 가로 위치 → 주파수도 로그로 나눈다
                // (그래야 낮은음이 왼쪽 몇 칸에 몰리지 않고 고르게 퍼진다).
                float f0 = minFrequency * Mathf.Pow(maxFrequency / minFrequency, x01);
                float f1 = minFrequency * Mathf.Pow(maxFrequency / minFrequency, (i + 1) / (float)n);
                int b0 = Mathf.Clamp(Mathf.FloorToInt(f0 / binHz), 0, spectrumSize - 1);
                int b1 = Mathf.Clamp(Mathf.CeilToInt(f1 / binHz), b0 + 1, spectrumSize);
                float sum = 0f;
                for (int b = b0; b < b1; b++) sum += spectrum[b];
                // 제곱근을 씌우면 작은 소리도 눈에 보이고 큰 소리는 너무 튀지 않는다.
                target = Mathf.Clamp01(Mathf.Sqrt(sum / (b1 - b0)) * gain * intensity);
            }

            // 올라갈 땐 조금 빠르게, 내려갈 땐 천천히 (잉크가 마르듯 여운을 남긴다).
            float speed = target > levels[i] ? riseSpeed : fallSpeed;
            levels[i] = Mathf.Lerp(levels[i], target, 1f - Mathf.Exp(-speed * dt));

            // 양 끝은 0으로 모이게 하는 봉투(envelope). 선이 화면 가운데에서만 출렁인다.
            float envelope = Mathf.Sin(x01 * Mathf.PI);
            float wave = Mathf.Sin((x01 * waveCycles * Mathf.PI * 2f) - phase * 2f);
            float wobble = (Mathf.PerlinNoise(x01 * 9f, phase * 0.3f) - 0.5f) * 2f * idleWobble;
            heights[i] = Mathf.Clamp((levels[i] * wave + wobble) * envelope, -1f, 1f);
        }

        if (waveform != null) waveform.SetHeights(heights, phase);
    }

    private void FadeTo(float target, float duration)
    {
        if (canvasGroup == null) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(target, duration));
    }

    private IEnumerator FadeRoutine(float target, float duration)
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            // SmoothStep: 시작과 끝을 부드럽게 해서 "툭" 나타나거나 사라지지 않게 한다.
            canvasGroup.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        canvasGroup.alpha = target;
        if (target <= 0f) source = null; // 다 사라지면 소리 참조를 놓는다
        fadeRoutine = null;
    }
}
