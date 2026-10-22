using UnityEngine;

namespace CoopDemo
{
    // GameFlowSync의 단계를 보고 이 컴퓨터의 화면(Canvas)과 커서 상태만 맞추는 곳. 판정은 절대 하지 않는다(표시만).
    // 이동/상호작용은 이 스크립트가 직접 막지 않는다 — Cursor.lockState를 Playing일 때만 Locked로 둬서,
    // 이미 있는 FirstPersonController/PlayerInteractor의 "커서 풀리면 멈춘다" 로직을 그대로 재사용한다.
    public class GameFlowUI : MonoBehaviour
    {
        [SerializeField] GameObject introCanvas;
        [SerializeField] GameObject endingCanvas;
        [SerializeField] GameObject resultCanvas;

        GameFlowSync sync;
        GameStage shown = (GameStage)(-1);   // 아직 한 번도 반영 안 함 표시

        void Update()
        {
            if (sync == null)
            {
                sync = GameFlowSync.Instance;
                if (sync == null) return;
                sync.StageChanged += OnStageChanged;
                Apply(sync.Stage);
            }

            // Playing 중의 커서 상태(잠금/해제)는 옵션·소지품이 각자 관리하므로 건드리지 않는다.
            // 그 외 단계에서는 계속 풀려 있어야 하므로 혹시 뭔가 잠가 놨더라도 매 프레임 되돌린다.
            if (shown != GameStage.Playing && Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void OnDestroy()
        {
            if (sync != null) sync.StageChanged -= OnStageChanged;
        }

        void OnStageChanged(GameStage stage) { Apply(stage); }

        void Apply(GameStage stage)
        {
            bool wasPlaying = shown == GameStage.Playing;
            shown = stage;

            if (introCanvas != null) introCanvas.SetActive(stage == GameStage.Intro);
            if (endingCanvas != null) endingCanvas.SetActive(stage == GameStage.Ending);
            if (resultCanvas != null) resultCanvas.SetActive(stage == GameStage.Result);

            // Playing에 막 들어선 순간에만 커서를 잠근다. 그 뒤로는 Options/소지품이 자유롭게 풀었다 잠갔다 한다.
            if (stage == GameStage.Playing && !wasPlaying)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
