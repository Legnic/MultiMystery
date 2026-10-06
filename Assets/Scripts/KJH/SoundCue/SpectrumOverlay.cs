using System.Collections;
using UnityEngine;

// 연출용 "소리 스펙트럼" 표시. 화면에 잉크 펜 선 하나가 소리에 맞춰 일렁인다.
// (ObjectEcho에서는 암전된 화면 정가운데에 둔다. 위치·크기는 이 오브젝트의 RectTransform으로 정한다)
// 재생 중인 AudioSource를 넘겨받아 매 프레임 소리를 분석하고,
// 그 결과를 "선의 출렁임 크기"로 바꿔 InkWaveformGraphic에 넘긴다.
//   - 나타날 때 0.5초 페이드인, 사라질 때 1초 페이드아웃 (갑툭튀 없이)
//   - 사라지는 동안 선이 서서히 잔잔해져서 "펜이 멈추는" 느낌으로 끝난다
//
// 왜 막대그래프가 아니라 선인가: 1890년대 배경에 디지털 이퀄라이저 막대는 어울리지 않아서,
// 지진계/필기 기록계처럼 펜이 종이 위에 긋는 선으로 표현했다.
//
// 선 높이 계산 순서 (매 프레임):
//   1) 주파수 분석: 가로 위치마다 낮은음(왼쪽) ~ 높은음(오른쪽) 주파수 하나를 맡겨 그 세기를 구한다.
//   2) 자동 음량 맞춤: 최근 가장 큰 세기를 기준으로 나눠, 소리가 크든 작든 비슷한 폭으로 출렁이게 한다.
//   3) dB 곡선: 세기를 사람 귀처럼 로그(dB) 단위로 바꿔 작은 소리의 변화도 보이게 한다.
//   4) 실제 파형 섞기: 소리의 실제 진동 모양을 조금 섞어 두드림 같은 순간 반응을 살린다.
// 반응 방식(2~4와 속도)은 SpectrumResponse 값 묶음으로 정하고, 사물마다 프리셋으로 바꿀 수 있다.
//
// 사용법: Canvas 아래, CanvasGroup이 붙은 오브젝트에 붙이고 자식 InkWaveformGraphic을 연결한다.
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

    [Tooltip("실제 파형을 읽어 올 샘플 수 (2의 거듭제곱). 1024 ≈ 0.02초 분량.")]
    [SerializeField] private int waveformSampleSize = 1024;

    [Tooltip("표시할 가장 낮은/높은 주파수 (Hz). 사람 귀에 의미 있는 대역만 쓴다.")]
    [SerializeField] private float minFrequency = 60f;
    [SerializeField] private float maxFrequency = 6000f;

    [Tooltip("소리가 없어도 남는 아주 작은 떨림 (손으로 그은 선 느낌). 0이면 완전한 직선.")]
    [SerializeField] private float idleWobble = 0.02f;

    [Header("반응 (기본값)")]
    [Tooltip("사물에 프리셋(SpectrumResponseProfile)이 연결되지 않았을 때 쓰는 기본 반응 값.")]
    [SerializeField] private SpectrumResponse defaultResponse = new SpectrumResponse();

    private AudioSource source;
    private float intensity = 1f;
    private SpectrumResponse response; // 이번 재생에 쓰는 반응 값 (프리셋 또는 기본값)

    private float[] spectrum;
    private float[] samples;       // 실제 파형 (시간에 따른 진동 값, -1 ~ 1)
    private float[] rawLevels;     // 이번 프레임의 점별 세기 (자동 음량 맞춤 전)
    private float[] levels;        // 점마다 부드럽게 따라가는 현재 세기
    private float[] waveShape;     // 점마다 부드럽게 따라가는 실제 파형 값
    private float[] heights;       // 실제로 그릴 높이 (-1 ~ 1)
    private float phase;

    // 자동 음량 맞춤의 기준값 (최근 가장 큰 값, 시간이 지나면 서서히 줄어듦).
    private float spectrumReference;
    private float waveformReference;

    private Coroutine fadeRoutine;

    // 지금 화면에 보이는 중인지 (페이드아웃 중이면 false).
    public bool IsShowing { get; private set; }

    private void Awake()
    {
        // 분석 크기는 2의 거듭제곱이어야 하므로, 잘못 넣어도 가장 가까운 값으로 바로잡는다.
        spectrumSize = Mathf.Clamp(Mathf.ClosestPowerOfTwo(spectrumSize), 64, 8192);
        waveformSampleSize = Mathf.Clamp(Mathf.ClosestPowerOfTwo(waveformSampleSize), 64, 8192);
        spectrum = new float[spectrumSize];
        samples = new float[waveformSampleSize];

        int n = Mathf.Max(2, pointCount);
        rawLevels = new float[n];
        levels = new float[n];
        waveShape = new float[n];
        heights = new float[n];

        response = defaultResponse;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false; // 연출 표시일 뿐이니 마우스 입력을 막지 않는다
            canvasGroup.interactable = false;
        }
    }

    // 소리 재생이 시작될 때 부른다. intensity는 사물별 "스펙트럼 표시 강도" (1 = 기본).
    // profile이 있으면 그 반응 값을, 없으면 이 컴포넌트의 기본 반응 값을 쓴다.
    public void Show(AudioSource audioSource, float spectrumIntensity = 1f, SpectrumResponseProfile profile = null)
    {
        source = audioSource;
        intensity = Mathf.Max(0f, spectrumIntensity);
        response = profile != null && profile.response != null ? profile.response : defaultResponse;

        // 새 소리마다 기준값을 다시 잡는다 (앞 소리의 큰 값이 남아 새 소리가 작게 보이지 않도록).
        spectrumReference = 0f;
        waveformReference = 0f;

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
        SpectrumResponse r = response ?? defaultResponse;
        bool hasSound = IsShowing && source != null && source.isPlaying;
        int n = levels.Length;

        if (hasSound)
        {
            source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
            source.GetOutputData(samples, 0);
            ComputeRawLevels(r);
        }
        else
        {
            System.Array.Clear(rawLevels, 0, n);
        }

        // ── 2) 자동 음량 맞춤용 기준값: 이번 프레임 최대값과 "서서히 줄어드는 이전 기준" 중 큰 값 ──
        float decay = r.normalizeRelease > 0f ? Mathf.Exp(-dt / r.normalizeRelease) : 0f;
        float frameMax = 0f;
        for (int i = 0; i < n; i++) frameMax = Mathf.Max(frameMax, rawLevels[i]);
        spectrumReference = Mathf.Max(frameMax, spectrumReference * decay);
        // 기준값으로 나누면 "가장 큰 순간 = 1"이 된다. 목표 높이(normalizeTarget)는 dB 변환 뒤에 곱한다
        // (dB 변환 전에 곱하면 0.85가 로그를 거쳐 거의 1로 바뀌어 버리기 때문).
        float spectrumScale = r.autoNormalize ? 1f / Mathf.Max(spectrumReference, r.minReference) : 1f;
        float heightScale = (r.autoNormalize ? r.normalizeTarget : 1f) * intensity;

        float samplePeak = 0f;
        if (hasSound) for (int s = 0; s < samples.Length; s++) samplePeak = Mathf.Max(samplePeak, Mathf.Abs(samples[s]));
        // 오실로스코프처럼 "아래→위로 0을 지나는 지점"부터 읽는다. 매 프레임 같은 위상에서 시작해야
        // 반복되는 소리의 모양이 화면에 멈춰 보이고, 부드럽게 따라가게 해도 서로 상쇄되지 않는다.
        int window = samples.Length / 2;
        int trigger = hasSound ? FindRisingZeroCrossing(window) : 0;
        waveformReference = Mathf.Max(samplePeak, waveformReference * decay);
        // 실제 파형은 원래 -1~1 범위라 자동 맞춤을 꺼도 쓸 수 있지만, 작게 녹음된 소리는 거의 안 보이므로 맞춤을 권장.
        float waveformScale = r.autoNormalize ? r.normalizeTarget / Mathf.Max(waveformReference, r.minReference * 0.1f) : 1f;

        phase += r.waveFlowSpeed * dt;
        float riseK = 1f - Mathf.Exp(-r.riseSpeed * dt);
        float fallK = 1f - Mathf.Exp(-r.fallSpeed * dt);
        float shapeK = 1f - Mathf.Exp(-r.waveformSmoothing * dt);

        for (int i = 0; i < n; i++)
        {
            float x01 = i / (float)(n - 1);

            // ── 3) dB 곡선: 자동 맞춤까지 끝난 세기를 로그 단위로 바꿔 0~1로 펼친다 ──
            float target = ToDisplayLevel(rawLevels[i] * spectrumScale, r) * heightScale;
            target = Mathf.Clamp01(target);

            // 올라갈 땐 빠르게, 내려갈 땐 조금 느리게 (잉크가 마르듯 짧은 여운).
            levels[i] = Mathf.Lerp(levels[i], target, target > levels[i] ? riseK : fallK);

            // ── 4) 실제 파형: 가로 위치에 맞는 샘플을 읽어 부드럽게 따라가게 한다 (지글거림 방지) ──
            float sample = hasSound ? samples[trigger + Mathf.RoundToInt(x01 * (window - 1))] * waveformScale * intensity : 0f;
            waveShape[i] = Mathf.Lerp(waveShape[i], Mathf.Clamp(sample, -1f, 1f), shapeK);

            // 주파수 세기로 만든 흐르는 물결과 실제 진동 모양을 비율대로 섞는다.
            float flowingWave = levels[i] * Mathf.Sin((x01 * r.waveCycles * Mathf.PI * 2f) - phase * 2f);
            float wave = Mathf.Lerp(flowingWave, waveShape[i], r.waveformBlend);

            // 양 끝은 0으로 모이게 하는 봉투(envelope). 끝에서 edgeFalloff 구간만큼 부드럽게 0으로 모인다.
            float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Min(x01, 1f - x01) / r.edgeFalloff);
            float wobble = (Mathf.PerlinNoise(x01 * 9f, phase * 0.3f) - 0.5f) * 2f * idleWobble;
            heights[i] = Mathf.Clamp((wave + wobble) * envelope, -1f, 1f);
        }

        if (waveform != null) waveform.SetHeights(heights, phase);
    }

    // ── 1) 주파수 분석: 점마다 맡은 주파수 구간의 평균 세기를 구한다 ──
    private void ComputeRawLevels(SpectrumResponse r)
    {
        // 한 칸(bin)이 몇 Hz인지: 전체 대역(샘플레이트의 절반)을 칸 수로 나눈 값.
        float binHz = AudioSettings.outputSampleRate * 0.5f / spectrumSize;
        int n = rawLevels.Length;
        for (int i = 0; i < n; i++)
        {
            // 사람 귀는 주파수를 로그 단위로 느끼므로, 가로 위치 → 주파수도 로그로 나눈다
            // (그래야 낮은음이 왼쪽 몇 칸에 몰리지 않고 고르게 퍼진다).
            float f0 = minFrequency * Mathf.Pow(maxFrequency / minFrequency, i / (float)n);
            float f1 = minFrequency * Mathf.Pow(maxFrequency / minFrequency, (i + 1) / (float)n);
            int b0 = Mathf.Clamp(Mathf.FloorToInt(f0 / binHz), 0, spectrumSize - 1);
            int b1 = Mathf.Clamp(Mathf.CeilToInt(f1 / binHz), b0 + 1, spectrumSize);
            float sum = 0f;
            for (int b = b0; b < b1; b++) sum += spectrum[b];
            // 제곱근 = 에너지 → 진폭으로 바꾸는 계산. 여기에 gain을 곱한 값이 "세기"다.
            // 높은음 보정: 가장 낮은 주파수에서 몇 옥타브 위인지만큼 dB를 더한다 (10^(dB/20) = 진폭 배율).
            float octaves = Mathf.Log(Mathf.Sqrt(f0 * f1) / minFrequency, 2f);
            float tilt = Mathf.Pow(10f, r.highFrequencyTilt * octaves / 20f);
            rawLevels[i] = Mathf.Sqrt(sum / (b1 - b0)) * r.gain * tilt;
        }
    }

    // 앞쪽 절반(searchLength) 안에서 값이 음수→양수로 바뀌는 첫 위치를 찾는다. 없으면 0 (그냥 처음부터 읽음).
    private int FindRisingZeroCrossing(int searchLength)
    {
        for (int s = 1; s < searchLength; s++)
        {
            if (samples[s - 1] < 0f && samples[s] >= 0f) return s;
        }
        return 0;
    }

    // 세기(0~)를 화면 높이(0~1)로 바꾼다. dB 모드면 바닥값~0dB 구간을 0~1로 펼친다.
    private static float ToDisplayLevel(float amplitude, SpectrumResponse r)
    {
        if (!r.useDecibelScale) return amplitude;
        if (amplitude <= 0.00001f) return 0f;
        // 20·log10(진폭) = dB. 1(=가장 큰 소리 기준)이 0dB, 작을수록 음수.
        float db = 20f * Mathf.Log10(amplitude);
        return Mathf.InverseLerp(r.decibelFloor, 0f, db);
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
