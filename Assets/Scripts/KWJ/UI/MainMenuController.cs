using UnityEngine;

// 메인화면 버튼을 눌렀을 때 실행할 기능을 모아 둔 스크립트
// 각 버튼의 OnClick 칸에 아래 함수들이 연결되어 있다 (Inspector에서 확인 가능)
public class MainMenuController : MonoBehaviour
{
    // [방 만들기] 2인 접속에서 Host가 된다
    public void OnCreateRoom()
    {
        // TODO: 네트워크(Host 시작) 연결은 다음 단계에서 붙인다
        Debug.Log("[메인화면] 방 만들기 버튼 클릭");
    }

    // [참가하기] 다른 사람이 만든 방에 접속한다
    public void OnJoinRoom()
    {
        // TODO: IP 입력 창을 띄우고 Client로 접속하는 기능은 다음 단계에서 붙인다
        Debug.Log("[메인화면] 참가하기 버튼 클릭");
    }

    // [설정] 소리·화면 설정 창을 연다
    public void OnSettings()
    {
        // TODO: 설정 창은 나중에 만든다
        Debug.Log("[메인화면] 설정 버튼 클릭");
    }

    // [종료] 게임을 끈다
    public void OnQuit()
    {
        Debug.Log("[메인화면] 종료 버튼 클릭");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // 에디터에서는 Play 모드를 멈춘다
#else
        Application.Quit();                                // 빌드된 게임에서는 프로그램을 끈다
#endif
    }
}
