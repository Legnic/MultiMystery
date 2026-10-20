using System;

namespace Gim002
{
    // GIM-002 네트워크 어댑터. 화면 쪽 스크립트(손잡이, 슬롯, 건반 등)는 이 인터페이스로 "요청"만 보내고,
    // 결과는 OnStateSynced(상태)와 OnEvent(일회성 연출 신호)로 받는다.
    // 판정은 IsAuthority인 곳(호스트, 혼자 플레이면 자기 자신) 한 곳에서만 한다.
    //
    // 구현:
    //  - LocalGim002Net : 네트워크 없이 바로 판정 (한 씬 테스트, 혼자 플레이)
    //  - Gim002NetSync  : Netcode for GameObjects. 클라이언트는 RPC로 요청, 호스트가 판정 후 모두에게 전달
    public interface IGim002Net
    {
        bool IsAuthority { get; }                    // 이 기기가 판정 담당인지
        Gim002SyncState Current { get; }             // 마지막으로 받은 동기화 상태

        void RequestGim001Solved();                  // GIM-001 해결 (판정 담당만 호출)
        void RequestMusicDoorOpened();               // 1층 → 권한자
        void RequestStageWound();                    // 2층 → 권한자
        void RequestCrankCompleted();                // 2층 → 권한자
        void RequestSlotChanged(DollId? doll, BackgroundId? bg); // 2층 → 권한자 (바뀐 뒤의 두 슬롯 값)
        void RequestPianoKey(SymbolId symbol);       // 1층 → 권한자 (EV_KEY_PRESSED)

        void Tick(float deltaTime);                  // 판정 담당이면 로직 시간을 흘린다. 아니면 아무것도 안 함

        event Action<Gim002SyncState> OnStateSynced; // 권한자 → 모두
        event Action<Gim002NetEvent, int> OnEvent;   // 권한자 → 모두
    }
}
