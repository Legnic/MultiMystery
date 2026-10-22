using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CoopDemo
{
    // 도입부(4컷)/엔딩에서 공용으로 쓰는 "그림+대사" 컷신 재생기. Time.timeScale과 무관하게 돈다(Time.unscaledDeltaTime만 사용).
    // 끝나면(끝까지 보거나 스킵) GameFlowSync에 본인 완료만 보고하고, 상대를 기다리는 화면으로 바뀐다.
    // 실제 네트워크 게임을 멈추지 않는다 — 이 스크립트는 로컬 UI 표시와 "다 봤다" 보고만 한다.
    [DisallowMultipleComponent]
    public class IllustratedCutsceneController : MonoBehaviour
    {
        [System.Serializable]
        public struct Cut
        {
            public Sprite image;
            [TextArea(2, 4)] public string caption;
            [Tooltip("이 컷을 최소 이만큼은 보여준다(초, 실시간). 0이면 즉시 다음으로 넘어갈 수 있다.")]
            public float minSeconds;
        }

        public enum Kind { Intro, Ending }

        [Tooltip("Intro면 양쪽 다 끝났을 때 GameFlowSync가 Playing으로, Ending이면 Result로 전환한다.")]
        public Kind kind = Kind.Intro;

        [Header("컷 목록 (유니티 기본 UI 소스로 임시 제작 가능 — 실제 일러스트는 나중에 교체)")]
        public Cut[] cuts;

        [Header("UI 참조")]
        [SerializeField] Image illustration;
        [SerializeField] Text caption;
        [SerializeField] Button nextButton;
        [SerializeField] Button skipButton;
        [SerializeField] GameObject playbackRoot;   // 그림+대사+버튼 묶음
        [SerializeField] GameObject waitingRoot;    // "상대 플레이어를 기다리는 중..." 패널
        [SerializeField] Text waitingText;

        int index;
        bool localFinished;
        Coroutine autoAdvance;

        void OnEnable()
        {
            index = 0;
            localFinished = false;
            if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);
            ShowCurrentCut();
        }

        void OnDisable()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(OnNextClicked);
            if (skipButton != null) skipButton.onClick.RemoveListener(OnSkipClicked);
            if (autoAdvance != null) { StopCoroutine(autoAdvance); autoAdvance = null; }
        }

        // GameFlowUI가 "아직 상대를 기다리는 중"이면 매 프레임 불러서 대기 문구를 갱신한다.
        public void RefreshWaitingText()
        {
            if (!localFinished || waitingText == null) return;
            waitingText.text = "상대 플레이어를 기다리는 중...";
        }

        void ShowCurrentCut()
        {
            if (playbackRoot != null) playbackRoot.SetActive(true);
            if (waitingRoot != null) waitingRoot.SetActive(false);

            if (cuts == null || cuts.Length == 0) { FinishLocal(); return; }
            index = Mathf.Clamp(index, 0, cuts.Length - 1);
            var cut = cuts[index];
            if (illustration != null) { illustration.sprite = cut.image; illustration.enabled = cut.image != null; }
            if (caption != null) caption.text = cut.caption;
            if (nextButton != null) nextButton.interactable = false;

            if (autoAdvance != null) StopCoroutine(autoAdvance);
            autoAdvance = StartCoroutine(EnableNextAfter(cut.minSeconds));
        }

        IEnumerator EnableNextAfter(float seconds)
        {
            if (seconds > 0f) yield return new WaitForSecondsRealtime(seconds);
            if (nextButton != null) nextButton.interactable = true;
        }

        void OnNextClicked()
        {
            if (localFinished) return;
            index++;
            if (cuts == null || index >= cuts.Length) FinishLocal();
            else ShowCurrentCut();
        }

        void OnSkipClicked() { FinishLocal(); }

        // 버튼 연타·콜백 중복에도 한 번만 보고한다.
        void FinishLocal()
        {
            if (localFinished) return;
            localFinished = true;
            if (autoAdvance != null) { StopCoroutine(autoAdvance); autoAdvance = null; }

            if (playbackRoot != null) playbackRoot.SetActive(false);
            if (waitingRoot != null) waitingRoot.SetActive(true);
            RefreshWaitingText();

            if (GameFlowSync.Instance == null) return;
            if (kind == Kind.Intro) GameFlowSync.Instance.RequestIntroFinished();
            else GameFlowSync.Instance.RequestEndingFinished();
        }
    }
}
