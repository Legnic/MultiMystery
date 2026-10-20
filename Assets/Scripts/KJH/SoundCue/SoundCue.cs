using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

// "연출용 소리 한 번 재생 + 스펙트럼 표시"를 묶은 재생기.
// 연출 코드는 Play(클립, 볼륨, 강도)만 부르면 되고, 소리가 끝나면 스펙트럼 페이드아웃까지 이 컴포넌트가 알아서 한다.
// ObjectEcho(사물에 손 얹기) 말고도, 나중에 "축음기 듣기", "벽 너머 소리 엿듣기" 같은 연출에서 그대로 재사용할 수 있다.
//
// 소리는 2D(거리 감쇠 없음)로 재생한다: 사물의 소리가 "머릿속에 직접 들리는" 능력 연출이기 때문.
// 출력 그룹(AudioMixerGroup)을 환경음과 다른 그룹(예: Echo)으로 두면, 환경음을 줄여도 이 소리는 줄지 않는다.
// 사용법: Player 아래 빈 오브젝트 "SoundCue"에 붙인다 (AudioSource는 없으면 자동으로 추가됨).
[RequireComponent(typeof(AudioSource))]
public class SoundCue : MonoBehaviour
{
    [Tooltip("스펙트럼 선을 그릴 UI. 비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private SpectrumOverlay spectrumOverlay;

    [Tooltip("이 소리가 나갈 믹서 그룹 (예: Echo). 환경음 그룹과 분리해야 환경음 줄이기에 같이 줄지 않는다. 비워도 동작한다.")]
    [SerializeField] private AudioMixerGroup outputGroup;

    [Tooltip("Stop()으로 중간에 끊을 때 소리를 줄이는 시간 (초). 0이면 뚝 끊겨서 '틱' 소리가 날 수 있다.")]
    [SerializeField] private float stopFadeDuration = 0.3f;

    private AudioSource audioSource;
    private Coroutine watchRoutine;

    // 소리가 재생 중인지 (끝나서 스펙트럼 페이드아웃이 시작되면 false).
    public bool IsPlaying { get; private set; }

    // 소리가 끝났을 때(끝까지 재생 또는 Stop) 한 번 호출된다.
    public event Action Finished;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D: 어디서 듣든 같은 크기 (능력 연출용)
        if (outputGroup != null) audioSource.outputAudioMixerGroup = outputGroup;
        if (spectrumOverlay == null) spectrumOverlay = FindAnyObjectByType<SpectrumOverlay>(FindObjectsInactive.Include);
    }

    // 클립을 재생하고 스펙트럼을 띄운다. 이미 재생 중이면 앞의 소리는 끊고 새로 시작한다.
    // responseProfile: 스펙트럼 반응 프리셋(잔잔/보통/예민 등). 비우면 SpectrumOverlay의 기본 반응을 쓴다.
    public void Play(AudioClip clip, float volume = 1f, float spectrumIntensity = 1f, SpectrumResponseProfile responseProfile = null)
    {
        if (clip == null)
        {
            Debug.LogWarning("[SoundCue] 재생할 AudioClip이 비어 있습니다.", this);
            return;
        }

        if (watchRoutine != null) StopCoroutine(watchRoutine);
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.volume = Mathf.Clamp01(volume);
        audioSource.Play();
        IsPlaying = true;

        if (spectrumOverlay != null) spectrumOverlay.Show(audioSource, spectrumIntensity, responseProfile);
        watchRoutine = StartCoroutine(WatchRoutine());
    }

    // 재생 중인 소리를 짧게 줄이며 멈춘다 (연출 중단 등).
    public void Stop()
    {
        if (!IsPlaying) return;
        if (watchRoutine != null) StopCoroutine(watchRoutine);
        // 꺼진 오브젝트에서는 코루틴을 시작할 수 없으므로 페이드 없이 바로 멈춘다.
        if (!isActiveAndEnabled)
        {
            audioSource.Stop();
            FinishPlayback();
            return;
        }
        watchRoutine = StartCoroutine(StopRoutine());
    }

    // 소리가 자연스럽게 끝날 때까지 지켜보다가 끝나면 마무리한다.
    private IEnumerator WatchRoutine()
    {
        // Play() 직후 한 프레임은 isPlaying이 아직 준비되지 않았을 수 있어서 한 번 기다린다.
        yield return null;
        while (audioSource.isPlaying) yield return null;
        FinishPlayback();
    }

    private IEnumerator StopRoutine()
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;
        while (elapsed < stopFadeDuration && audioSource.isPlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / stopFadeDuration);
            yield return null;
        }
        audioSource.Stop();
        FinishPlayback();
    }

    private void FinishPlayback()
    {
        watchRoutine = null;
        IsPlaying = false;
        if (spectrumOverlay != null) spectrumOverlay.Hide(); // 여기서 1초 페이드아웃이 시작된다
        Finished?.Invoke();
    }

    private void OnDisable()
    {
        // 오브젝트가 꺼지면 코루틴도 멈추므로, 스펙트럼이 화면에 남지 않게 정리한다.
        if (IsPlaying)
        {
            if (audioSource != null) audioSource.Stop();
            IsPlaying = false;
            if (spectrumOverlay != null) spectrumOverlay.Hide();
        }
        watchRoutine = null;
    }
}
