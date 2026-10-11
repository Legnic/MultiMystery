using UnityEngine;

namespace CoopDemo
{
    // 더미 타이틀 화면. 각 버튼의 OnClick에 아래 함수들이 연결되어 있다.
    // 옵션 버튼은 아직 기능이 없어서 연결하지 않았다.
    public class TitleMenuUI : MonoBehaviour
    {
        [Tooltip("시작을 누르면 숨길 타이틀 화면 루트. 비워두면 이 오브젝트 자체를 숨긴다.")]
        [SerializeField] GameObject titleRoot;

        [Tooltip("시작을 누르면 켤 멀티플레이 메뉴 컴포넌트. 타이틀이 떠 있는 동안에는 꺼져 있어야 한다. 비워두면 아무것도 켜지 않는다.")]
        [SerializeField] Behaviour multiplayerMenu;

        public GameObject TitleRoot => titleRoot != null ? titleRoot : gameObject;
        public Behaviour MultiplayerMenu => multiplayerMenu;

        public void OnStartClicked()
        {
            Debug.Log("[타이틀] 시작 버튼 클릭");
            if (multiplayerMenu != null) multiplayerMenu.enabled = true;
            TitleRoot.SetActive(false);
        }

        public void OnQuitClicked()
        {
            Debug.Log("[타이틀] 나가기 버튼 클릭");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // 멀티플레이 화면에서 타이틀로 돌아올 때 MultiplayerBackButton이 호출한다.
        public void ShowTitle()
        {
            if (multiplayerMenu != null) multiplayerMenu.enabled = false;
            TitleRoot.SetActive(true);
        }
    }
}
