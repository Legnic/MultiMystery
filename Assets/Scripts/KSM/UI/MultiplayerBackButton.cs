using Unity.Netcode;
using UnityEngine;

namespace CoopDemo
{
    // 멀티플레이(방 목록) 화면의 오른쪽 아래에 떠 있는 "타이틀로" 버튼.
    // 방 목록 화면이 떠 있을 때만 보이고, 접속을 시작했거나 게임에 들어가면 숨는다.
    // 이 스크립트가 붙은 오브젝트는 항상 켜져 있어야 한다 (버튼 자신이 아니라 부모에 붙일 것).
    public class MultiplayerBackButton : MonoBehaviour
    {
        [SerializeField] TitleMenuUI title;
        [Tooltip("보였다 숨었다 할 실제 버튼 오브젝트")]
        [SerializeField] GameObject buttonObject;

        void Update()
        {
            if (title == null || buttonObject == null) return;

            var menu = title.MultiplayerMenu;
            var nm = NetworkManager.Singleton;
            bool inLobby = menu != null && menu.enabled
                           && LGH.NetworkPlayer.Local == null
                           && (nm == null || !nm.IsListening);

            if (buttonObject.activeSelf != inLobby) buttonObject.SetActive(inLobby);
        }

        public void OnBackClicked()
        {
            Debug.Log("[멀티플레이] 타이틀로 돌아가기");
            if (title != null) title.ShowTitle();
        }
    }
}
