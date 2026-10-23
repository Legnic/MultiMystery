using System;

namespace Gim007
{
    // GIM-007 네트워크 어댑터. 화면 쪽 스크립트(손잡이, 미는 플레이어)는 이 인터페이스로 "요청"만 보내고,
    // 결과는 OnStateSynced(상태)와 OnEvent(일회성 연출 신호)로 받는다. 판정은 IsAuthority인 곳 한 곳에서만 한다.
    //
    // 구현:
    //  - LocalGim007Net : 네트워크 없이 바로 판정 (혼자 테스트)
    //  - Gim007NetSync  : Netcode for GameObjects. 클라이언트는 RPC로 요청, 호스트가 판정 후 모두에게 전달
    //
    // 요청에는 "누가"를 넣지 않는다: 네트워크에서는 판정 담당이 RPC를 보낸 클라이언트 ID로 정하고,
    // 로컬에서는 LocalPlayerId를 쓴다 (다른 사람 이름으로 요청을 보낼 수 없게).
    public interface IGim007Net
    {
        bool IsAuthority { get; }
        Gim007SyncState Current { get; }
        ulong LocalPlayerId { get; }

        void RequestGrip(int handle);                   // 손잡이 잡기
        void RequestRelease();                          // 손 떼기
        void RequestReport(int round, bool success);    // 이번 라운드 내 결과

        void Tick(float deltaTime);                     // 판정 담당이면 타이머를 흘린다

        event Action<Gim007SyncState> OnStateSynced;
        event Action<Gim007NetEvent, int> OnEvent;
    }
}
