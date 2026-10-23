using System;

namespace Gim007
{
    // 네트워크 없이 동작하는 어댑터. 요청을 바로 Gim007Logic에 넘기고 결과를 바로 돌려준다.
    // 용도: ① 혼자 테스트 ② 네트워크 어댑터(Gim007NetSync)의 호스트 쪽 "판정 엔진"으로 재사용.
    public class LocalGim007Net : IGim007Net
    {
        public const ulong OfflinePlayerId = 0;

        public Gim007Logic Logic { get; private set; }

        public bool IsAuthority { get { return true; } }
        public Gim007SyncState Current { get { return Logic.Snapshot(); } }
        public ulong LocalPlayerId { get { return OfflinePlayerId; } }

        public event Action<Gim007SyncState> OnStateSynced;
        public event Action<Gim007NetEvent, int> OnEvent;

        public LocalGim007Net(Gim007Config config)
        {
            Logic = new Gim007Logic(config.rules);
            Logic.OnChanged += () => OnStateSynced?.Invoke(Logic.Snapshot());
            Logic.OnEvent += (e, v) => OnEvent?.Invoke(e, v);
        }

        public void RequestGrip(int handle) { Logic.Grip(handle, OfflinePlayerId); }
        public void RequestRelease() { Logic.Release(OfflinePlayerId); }
        public void RequestReport(int round, bool success) { Logic.Report(OfflinePlayerId, round, success); }
        public void Tick(float deltaTime) { Logic.Tick(deltaTime); }

        // 판정 엔진으로 쓸 때: 특정 플레이어 이름으로 처리 (네트워크 어댑터가 RPC 보낸 사람 ID를 넣는다)
        public void GripAs(ulong player, int handle) { Logic.Grip(handle, player); }
        public void ReleaseAs(ulong player) { Logic.Release(player); }
        public void ReportAs(ulong player, int round, bool success) { Logic.Report(player, round, success); }
    }
}
