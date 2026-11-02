using UnityEngine;

namespace CoopDemo
{
    // 멀티플레이(방 목록/만들기) 화면의 오른쪽 아래에 떠 있는 "타이틀로" 버튼.
    // 플레이어가 실제로 스폰돼 게임에 들어가기 전까지는 계속 보여야 한다 — 방 만들기 직후 Relay 연결이 끝나
    // NetworkManager.IsListening이 True가 돼도(플레이어 스폰은 그보다 나중) 여전히 "방 목록/만들기" 화면이므로,
    // MultiplayerMenu 자신의 InGame 판정(NetworkPlayer.Local != null)과 똑같은 기준만 쓴다.
    // 이 스크립트가 붙은 오브젝트는 항상 켜져 있어야 한다 (버튼 자신이 아니라 부모에 붙일 것).
    public class MultiplayerBackButton : MonoBehaviour
    {
        [SerializeField] TitleMenuUI title;
        [Tooltip("보였다 숨었다 할 실제 버튼 오브젝트")]
        [SerializeField] GameObject buttonObject;

        bool returning;

        void Update()
        {
            if (title == null || buttonObject == null) return;

            var menu = title.MultiplayerMenu;
            bool inLobby = menu != null && menu.enabled && LGH.NetworkPlayer.Local == null;

            if (buttonObject.activeSelf != inLobby) buttonObject.SetActive(inLobby);
        }

        // 연타 방지 + 세션 정리. 방을 만들었거나(세션 생성됨) 참가 중이었다면 타이틀로 돌아가기 전에
        // 반드시 정리해야 한다 — 안 그러면 방이 목록에 계속 남거나 NetworkManager가 켜진 채로 남는다.
        public async void OnBackClicked()
        {
            if (returning) return;
            returning = true;
            Debug.Log("[멀티플레이] 타이틀로 돌아가기");
            if (buttonObject != null) buttonObject.SetActive(false);

            var menu = FindAnyObjectByType<LGH.MultiplayerMenu>(FindObjectsInactive.Include);
            if (menu != null) await menu.LeaveRoomAsync(backToTitle: true);

            if (title != null) title.ShowTitle();
            returning = false;
        }
    }
}
