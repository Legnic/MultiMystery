using System;

namespace Gim005
{
    // GIM-005 네트워크 어댑터. 화면 쪽 스크립트(입구, 버튼 패널, 중앙 버튼…)는 이 인터페이스로 "요청"만 보내고,
    // 결과는 OnStateSynced(상태)와 OnEvent(일회성 연출 신호)로 받는다. 판정은 IsAuthority인 곳 한 곳에서만 한다.
    //
    // 구현:
    //  - LocalGim005Net : 네트워크 없이 바로 판정 (혼자 테스트)
    //  - Gim005NetSync  : Netcode for GameObjects. 클라이언트는 RPC로 요청, 호스트가 판정 후 모두에게 전달
    public interface IGim005Net
    {
        bool IsAuthority { get; }
        Gim005SyncState Current { get; }

        void RequestEnter();                // 1층 A: 입구 통과
        void RequestPress(int symbol);      // 2층 B: 문양 버튼
        void RequestCenter();               // 1층 A: 중앙 버튼

        void Tick(float deltaTime);         // 판정 담당이면 로직 시간을 흘린다

        event Action<Gim005SyncState> OnStateSynced;
        event Action<Gim005NetEvent, int> OnEvent;
    }
}
