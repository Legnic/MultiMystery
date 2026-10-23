using System;

namespace Gim004
{
    // GIM-004 네트워크 어댑터. 화면 쪽 스크립트(선반 자리, 화분)는 이 인터페이스로 "요청"만 보내고,
    // 결과는 OnStateSynced(상태)와 OnEvent(일회성 연출 신호)로 받는다. 판정은 IsAuthority인 곳 한 곳에서만 한다.
    //
    // 구현:
    //  - LocalGim004Net : 네트워크 없이 바로 판정 (혼자 테스트)
    //  - Gim004NetSync  : Netcode for GameObjects. 클라이언트는 RPC로 요청, 호스트가 판정 후 모두에게 전달
    public interface IGim004Net
    {
        bool IsAuthority { get; }
        Gim004SyncState Current { get; }

        void RequestPlace(int slot, int pot);   // 1층: 화분을 선반 자리에 놓음 (찬 자리면 교체)
        void RequestTake(int slot);             // 1층: 선반 자리의 화분을 뺌

        void Tick(float deltaTime);             // 판정 담당이면 판정 대기 시간을 흘린다

        event Action<Gim004SyncState> OnStateSynced;
        event Action<Gim004NetEvent, int> OnEvent;
    }
}
