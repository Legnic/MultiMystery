using UnityEngine;

namespace Gim002
{
    // 층별 효과음 스피커 (기획 6-4 "피드백 연출"). 1층 피아노와 2층 무대에 하나씩 둔다.
    // 게임 효과음은 네트워크(무전기)로 보내지 않고 각 층에서 따로 재생한다는 기획에 맞춰,
    // 3D 소리(가까이 있어야 들림)로 이 위치에서만 재생한다. 각 칸을 비워 두면 그 사건에는 소리를 내지 않는다.
    // "임시 소리 사용"을 켜 두면 비어 있는 칸 대신 코드로 만든 임시 소리를 낸다(사운드 에셋이 오기 전 테스트용).
    [RequireComponent(typeof(AudioSource))]
    public class Gim002FeedbackSpeaker : MonoBehaviour
    {
        [Tooltip("이 스피커가 어느 층용인지 (Console 로그 구분용)")]
        [SerializeField] string label = "1층";

        [Header("이 층에서 낼 소리 (사용하지 않을 사건은 'play' 체크 해제)")]
        [SerializeField] bool playStageWrong = true;
        [SerializeField] AudioClip stageWrongClip;     // 무대 오답 경고음 (양쪽)
        [SerializeField] bool playStageRight = true;
        [SerializeField] AudioClip stageRightClip;     // 반주판 열리는 소리 (2층만)
        [SerializeField] bool playPianoWrong = true;
        [SerializeField] AudioClip pianoWrongClip;     // 틀린 화음 (1층)
        [SerializeField] bool playPerformStart = true;
        [SerializeField] AudioClip performStartClip;   // 반주 정답 → 공연 시작 성공음 (양쪽)
        [SerializeField] bool playSceneClear = true;
        [SerializeField] AudioClip sceneClearClip;     // 장면 성공음 (양쪽)
        [SerializeField] bool playComplete = true;
        [SerializeField] AudioClip completeClip;       // 완료 연출음 (양쪽)

        [Header("선택 사항")]
        [Tooltip("건반이 기록될 때(EV_KEY_PRESSED) 같은 음을 이 스피커에서도 울림 (2층 오르골용, 기본 OFF)")]
        [SerializeField] bool echoPianoKeys;

        [Tooltip("소리 칸이 비어 있으면 코드로 만든 임시 소리를 냄")]
        [SerializeField] bool useFallbackTones = true;

        AudioSource source;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;   // 3D: 같은 층 근처에서만 들리게
        }

        void Start()
        {
            var c = Gim002Controller.Instance;
            if (c == null) return;
            c.EventReceived += OnEvent;
            c.StateChanged += OnState;
        }

        void OnDestroy()
        {
            var c = Gim002Controller.Instance;
            if (c == null) return;
            c.EventReceived -= OnEvent;
            c.StateChanged -= OnState;
        }

        void OnState(Gim002SyncState prev, Gim002SyncState now)
        {
            if (now.state == Gim002State.Perform && prev.state == Gim002State.Accompany && playPerformStart)
                Play(performStartClip, Gim002Tones.Success(), "반주 정답 성공음");
        }

        void OnEvent(Gim002NetEvent e, int value)
        {
            switch (e)
            {
                case Gim002NetEvent.StageWrong: if (playStageWrong) Play(stageWrongClip, Gim002Tones.Warning(), "무대 오답 경고음"); break;
                case Gim002NetEvent.StageRight: if (playStageRight) Play(stageRightClip, Gim002Tones.Open(), "반주판 열림"); break;
                case Gim002NetEvent.PianoWrong: if (playPianoWrong) Play(pianoWrongClip, Gim002Tones.WrongChord(), "반주 오답 화음"); break;
                case Gim002NetEvent.SceneClear: if (playSceneClear) Play(sceneClearClip, Gim002Tones.Success(), "장면 " + value + " 성공음"); break;
                case Gim002NetEvent.Complete: if (playComplete) Play(completeClip, Gim002Tones.Complete(), "완료 연출음"); break;
                case Gim002NetEvent.KeyPressed:
                    if (echoPianoKeys)
                    {
                        var key = Gim002PianoKey.FindBySymbol((SymbolId)value);
                        if (key != null) source.PlayOneShot(key.NoteClip, 0.6f);
                    }
                    break;
            }
        }

        void Play(AudioClip clip, AudioClip fallback, string what)
        {
            if (clip == null && useFallbackTones) clip = fallback;
            if (clip != null) source.PlayOneShot(clip);
            else Debug.Log("[GIM-002 " + label + " 소리 자리] " + what, this);
        }
    }
}
