using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoopDemo
{
    // 방(세션)이 끝나면 씬을 통째로 다시 불러와 모든 기믹·문·물건을 처음 상태로 되돌린다.
    // 씬을 다시 불러오지 않으면 초상화 각도 같은 기믹 상태가 메모리에 남아, 같은 실행 안에서 새 방을 만들면
    // 이전 판 진행이 그대로 보인다(RoomResetter는 문·쪽지·별 상자만 되돌린다).
    // 사용: 세션 정리(LeaveAsync/Shutdown) 뒤 Request() 한 번. 실제 재로드는 다음 프레임부터 처리한다
    // (NGO 콜백 도중에 NetworkManager를 지우지 않기 위해, 그리고 Shutdown이 끝날 때까지 기다리기 위해).
    public class RoomSceneReloader : MonoBehaviour
    {
        // 재로드 뒤 타이틀 대신 방 목록 화면을 바로 열지 (TitleMenuUI.Start가 읽고 지운다)
        public static bool OpenLobbyAfterReload;
        // 재로드 뒤 방 목록 화면에 띄울 안내 문구 (LGH.MultiplayerMenu가 읽고 지운다)
        public static string PendingStatus;

        static RoomSceneReloader pending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            OpenLobbyAfterReload = false;
            PendingStatus = null;
            pending = null;
        }

        public static void Request(bool openLobby, string status = null)
        {
            OpenLobbyAfterReload = openLobby;
            if (status != null) PendingStatus = status;
            if (pending != null) return;   // 이미 예약됨 (연타·콜백 중복)
            var go = new GameObject("RoomSceneReloader");
            DontDestroyOnLoad(go);
            pending = go.AddComponent<RoomSceneReloader>();
        }

        void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsListening || nm.ShutdownInProgress))
            {
                if (!nm.ShutdownInProgress) nm.Shutdown();
                return;   // Shutdown이 끝날 때까지 기다린다
            }

            // NetworkManager는 스스로 DontDestroyOnLoad라 씬을 다시 불러와도 남는다 → 지워서
            // 새로 불러온 씬의 NetworkManager가 Singleton이 되게 한다 (안 지우면 매번 하나씩 쌓인다).
            if (nm != null) Destroy(nm.gameObject);

            var scene = SceneManager.GetActiveScene();
            Debug.Log("[게임 흐름] 방이 끝나서 씬을 다시 불러옵니다: " + scene.name);
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);

            pending = null;
            Destroy(gameObject);
        }
    }
}
