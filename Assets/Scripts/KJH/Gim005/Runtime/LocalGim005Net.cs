using System;

namespace Gim005
{
    // 네트워크 없이 동작하는 어댑터. 요청을 바로 Gim005Logic에 넘기고 결과를 바로 돌려준다.
    // 용도: ① 혼자 테스트 ② 네트워크 어댑터(Gim005NetSync)의 호스트 쪽 "판정 엔진"으로 재사용.
    public class LocalGim005Net : IGim005Net
    {
        public Gim005Logic Logic { get; private set; }

        public bool IsAuthority { get { return true; } }
        public Gim005SyncState Current { get { return Logic.Snapshot(); } }

        public event Action<Gim005SyncState> OnStateSynced;
        public event Action<Gim005NetEvent, int> OnEvent;

        public LocalGim005Net(Gim005Config config)
        {
            Logic = new Gim005Logic(config.rules, Environment.TickCount);

            // 로직의 C# 이벤트를 네트워크 이벤트 형식으로 바꿔 전달한다
            Logic.OnChanged += () => OnStateSynced?.Invoke(Logic.Snapshot());
            Logic.OnStarted += () => Raise(Gim005NetEvent.Started, Logic.Attempt);
            Logic.OnGateOpened += g => Raise(Gim005NetEvent.GateOpened, g);
            Logic.OnWrong += s => Raise(Gim005NetEvent.Wrong, s);
            Logic.OnBell += () => Raise(Gim005NetEvent.Bell, (int)Logic.Remaining);
            Logic.OnTimeOut += () => Raise(Gim005NetEvent.TimeOut, Logic.Attempt);
            Logic.OnAttemptReset += () => Raise(Gim005NetEvent.AttemptReset, Logic.Attempt);
            Logic.OnComplete += () => Raise(Gim005NetEvent.Complete, Logic.Attempt);
        }

        void Raise(Gim005NetEvent e, int value) { OnEvent?.Invoke(e, value); }

        public void RequestEnter() { Logic.EnterMaze(); }
        public void RequestPress(int symbol) { Logic.PressSymbol(symbol); }
        public void RequestCenter() { Logic.PressCenter(); }

        // 재접속 처리: 진행 중 시도만 처음으로 (완료 여부는 유지)
        public void ResetAttempt() { Logic.ResetAttempt(); }

        public void Tick(float deltaTime) { Logic.Tick(deltaTime); }
    }
}
