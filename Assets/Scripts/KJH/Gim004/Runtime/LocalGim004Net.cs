using System;

namespace Gim004
{
    // 네트워크 없이 동작하는 어댑터. 요청을 바로 Gim004Logic에 넘기고 결과를 바로 돌려준다.
    // 용도: ① 혼자 테스트 ② 네트워크 어댑터(Gim004NetSync)의 호스트 쪽 "판정 엔진"으로 재사용.
    public class LocalGim004Net : IGim004Net
    {
        public Gim004Logic Logic { get; private set; }

        public bool IsAuthority { get { return true; } }
        public Gim004SyncState Current { get { return Logic.Snapshot(); } }

        public event Action<Gim004SyncState> OnStateSynced;
        public event Action<Gim004NetEvent, int> OnEvent;

        public LocalGim004Net(Gim004Config config)
        {
            Logic = new Gim004Logic(config.rules);
            Logic.OnChanged += () => OnStateSynced?.Invoke(Logic.Snapshot());
            Logic.OnJudging += () => OnEvent?.Invoke(Gim004NetEvent.Judging, Logic.WrongCount + 1);
            Logic.OnWrong += () => OnEvent?.Invoke(Gim004NetEvent.Wrong, Logic.WrongCount);
            Logic.OnComplete += () => OnEvent?.Invoke(Gim004NetEvent.Complete, Logic.WrongCount);
        }

        public void RequestPlace(int slot, int pot) { Logic.Place(slot, pot); }
        public void RequestTake(int slot) { Logic.Take(slot); }
        public void Tick(float deltaTime) { Logic.Tick(deltaTime); }
    }
}
