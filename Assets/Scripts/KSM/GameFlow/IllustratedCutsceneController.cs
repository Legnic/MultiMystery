using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CoopDemo
{
    // 도입부/엔딩 영상을 로컬에서 재생한다. 완료/스킵 후 기존 2인 완료 판정은 GameFlowSync가 담당한다.
    [DisallowMultipleComponent]
    public class IllustratedCutsceneController : MonoBehaviour
    {
        public enum Kind { Intro, Ending }

        public Kind kind = Kind.Intro;
        [SerializeField] VideoClip videoClip;
        [Tooltip("영상의 처음과 마지막에 검정 화면과 디졸브하는 시간(초). 영상 재생 시간을 기준으로 한다.")]
        [SerializeField, Min(0f)] float dissolveSeconds = 3f;
        [SerializeField] Button skipButton;
        [SerializeField] GameObject playbackRoot;
        [SerializeField] GameObject waitingRoot;
        [SerializeField] Text waitingText;

        // 기존 씬의 임시 컷 UI 참조. 영상으로 교체한 뒤에는 표시하지 않는다.
        [SerializeField, HideInInspector] Image illustration;
        [SerializeField, HideInInspector] Text caption;
        [SerializeField, HideInInspector] Button nextButton;

        VideoPlayer player;
        RawImage videoImage;
        RenderTexture videoTexture;
        Coroutine preparation;
        bool localFinished;

        void OnEnable()
        {
            localFinished = false;
            if (illustration != null) illustration.gameObject.SetActive(false);
            if (caption != null) caption.gameObject.SetActive(false);
            if (nextButton != null) nextButton.gameObject.SetActive(false);
            if (waitingRoot != null) waitingRoot.SetActive(false);
            if (playbackRoot != null) playbackRoot.SetActive(true);
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.interactable = true;
                skipButton.onClick.AddListener(FinishLocal);
            }

            if (videoClip == null || playbackRoot == null)
            {
                Debug.LogError($"[Cutscene] {kind}: 영상 또는 PlaybackRoot가 지정되지 않았습니다.", this);
                FinishLocal();
                return;
            }

            EnsureVideoPlayer();
            SetVideoOpacity(0f);
            // 영상 종료 후 대기 화면에서도 뒤의 게임 화면이 비치지 않게 한다.
            var canvasBackground = transform.Find("Background");
            if (canvasBackground != null && canvasBackground.TryGetComponent<Image>(out var image))
                image.color = Color.black;
            player.loopPointReached += OnVideoEnded;
            player.errorReceived += OnVideoError;
            preparation = StartCoroutine(PrepareAndPlay());
        }

        void EnsureVideoPlayer()
        {
            if (player != null) return;

            // 전체 화면 검정 배경 위에 원본 비율을 유지한 영상을 배치한다.
            var background = new GameObject("VideoBackground", typeof(RectTransform), typeof(Image));
            background.layer = playbackRoot.layer;
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.SetParent(playbackRoot.transform, false);
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            background.transform.SetAsFirstSibling();
            background.GetComponent<Image>().color = Color.black;

            var surface = new GameObject("Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            surface.layer = playbackRoot.layer;
            surface.transform.SetParent(background.transform, false);
            videoImage = surface.GetComponent<RawImage>();
            videoImage.raycastTarget = false;
            videoImage.enabled = false;
            SetVideoOpacity(0f);
            var fitter = surface.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)videoClip.width / Mathf.Max(1f, videoClip.height);

            var audio = surface.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            player = surface.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.waitForFirstFrame = true;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            player.source = VideoSource.VideoClip;
            player.clip = videoClip;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            if (videoClip.audioTrackCount > 0)
            {
                player.controlledAudioTrackCount = 1;
                player.EnableAudioTrack(0, true);
                player.SetTargetAudioSource(0, audio);
            }

            // 영상은 클릭을 가로채지 않고, 스킵 버튼은 항상 그 위에 표시된다.
            if (skipButton != null) skipButton.transform.SetAsLastSibling();
        }

        IEnumerator PrepareAndPlay()
        {
            // 첫 yield 이후부터 실패 시 코루틴을 안전하게 중단할 수 있다.
            yield return null;
            player.Prepare();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!player.isPrepared && !localFinished && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (localFinished) yield break;
            if (!player.isPrepared)
            {
                OnVideoError(player, "영상 준비 시간이 초과되었습니다.");
                yield break;
            }

            videoTexture = new RenderTexture((int)player.width, (int)player.height, 0, RenderTextureFormat.ARGB32);
            videoTexture.Create();
            var previous = RenderTexture.active;
            RenderTexture.active = videoTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
            player.targetTexture = videoTexture;
            videoImage.texture = videoTexture;
            videoImage.enabled = true;
            preparation = null;
            player.Play();
        }

        void Update()
        {
            if (localFinished || player == null || !player.isPrepared || !player.isPlaying) return;

            // 별도 타이머 대신 디코더의 재생 위치를 사용해 버퍼링/프레임 지연에도
            // 첫 프레임은 검정, 마지막 프레임은 검정으로 맞춘다.
            double duration = player.length > 0d ? player.length : videoClip.length;
            float fade = Mathf.Min(dissolveSeconds, (float)(duration * 0.5d));
            float opacity = 1f;
            if (fade > 0f)
            {
                float fadeIn = Mathf.Clamp01((float)(player.time / fade));
                float fadeOut = Mathf.Clamp01((float)((duration - player.time) / fade));
                opacity = Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
            }
            SetVideoOpacity(opacity);
        }

        void SetVideoOpacity(float opacity)
        {
            if (videoImage != null) videoImage.color = new Color(1f, 1f, 1f, opacity);
        }

        void OnVideoEnded(VideoPlayer source)
        {
            SetVideoOpacity(0f);
            FinishLocal();
        }

        void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogError($"[Cutscene] {kind} 영상 재생 실패: {message}", this);
            // 한쪽 기기의 디코딩 실패 때문에 두 플레이어가 영구 대기하지 않도록 완료 처리한다.
            FinishLocal();
        }

        public void RefreshWaitingText()
        {
            if (localFinished && waitingText != null)
                waitingText.text = "상대 플레이어를 기다리는 중...";
        }

        void StopVideo()
        {
            SetVideoOpacity(0f);
            if (preparation != null) { StopCoroutine(preparation); preparation = null; }
            if (player != null) { player.Stop(); player.targetTexture = null; }
            if (videoImage != null) { videoImage.texture = null; videoImage.enabled = false; }
            if (videoTexture != null)
            {
                videoTexture.Release();
                Destroy(videoTexture);
                videoTexture = null;
            }
        }

        void OnDisable()
        {
            if (skipButton != null) skipButton.onClick.RemoveListener(FinishLocal);
            if (player != null)
            {
                player.loopPointReached -= OnVideoEnded;
                player.errorReceived -= OnVideoError;
            }
            StopVideo();
        }

        // 버튼 연타·영상 종료 콜백 중복에도 한 번만 보고한다.
        void FinishLocal()
        {
            if (localFinished) return;
            localFinished = true;
            StopVideo();
            if (playbackRoot != null) playbackRoot.SetActive(false);
            if (waitingRoot != null) waitingRoot.SetActive(true);
            RefreshWaitingText();

            if (GameFlowSync.Instance == null) return;
            if (kind == Kind.Intro) GameFlowSync.Instance.RequestIntroFinished();
            else GameFlowSync.Instance.RequestEndingFinished();
        }
    }
}
