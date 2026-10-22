using UnityEngine;
using UnityEngine.UI;

namespace CoopDemo
{
    // 결과창. 엔딩(서사)과 책임을 분리 — 여기는 "플레이가 끝났다"는 확인과 타이틀로의 복귀만 담당한다.
    // 켜질 때마다(OnEnable) GameFlowSync의 클리어 시간을 한 번 읽어서 표시한다 — 호스트/게스트 전부 같은 서버 시간 기준 단일 값.
    public class ResultCanvasController : MonoBehaviour
    {
        [SerializeField] Text clearTimeText;
        [SerializeField] Button titleButton;

        bool returning;

        void OnEnable()
        {
            returning = false;
            if (titleButton != null) { titleButton.interactable = true; titleButton.onClick.AddListener(OnTitleClicked); }
            RefreshClearTime();
        }

        void OnDisable()
        {
            if (titleButton != null) titleButton.onClick.RemoveListener(OnTitleClicked);
        }

        void RefreshClearTime()
        {
            if (clearTimeText == null) return;
            double seconds = GameFlowSync.Instance != null ? GameFlowSync.Instance.ClearSeconds : -1;
            if (seconds < 0) { clearTimeText.text = "클리어 시간 --:--:--"; return; }
            var t = System.TimeSpan.FromSeconds(seconds);
            clearTimeText.text = $"클리어 시간 {(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }

        // 연타 방지: 한 번 누르면 그 이후로는 아무 것도 하지 않는다 (중복 세션 정리 호출 방지).
        async void OnTitleClicked()
        {
            if (returning) return;
            returning = true;
            if (titleButton != null) titleButton.interactable = false;

            // 순서: 네트워크 세션 정리 -> 로컬 UI 정리 -> 타이틀 표시
            // TitleMenuUI는 TitleRoot에 붙어 있는데 게임 중엔 TitleRoot가 꺼져 있으므로 반드시 Include로 찾아야 한다.
            var menu = FindAnyObjectByType<LGH.MultiplayerMenu>(FindObjectsInactive.Include);
            if (menu != null) await menu.LeaveRoomAsync();

            gameObject.SetActive(false);   // 다음 게임을 새로 시작했을 때 예전 결과창이 잠깐 비치지 않도록 즉시 숨긴다

            var titleUi = FindAnyObjectByType<TitleMenuUI>(FindObjectsInactive.Include);
            if (titleUi != null) titleUi.ShowTitle();
        }
    }
}
