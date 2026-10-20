using System;

namespace Gim002
{
    // 네트워크 없이 동작하는 어댑터. 요청을 바로 Gim002Logic에 넘기고, 결과를 바로 돌려준다.
    // 용도: ① 1층과 2층을 한 씬에서 혼자 테스트 ② 네트워크 어댑터(Gim002NetSync)의 호스트 쪽 "판정 엔진"으로 재사용.
    public class LocalGim002Net : IGim002Net
    {
        public Gim002Logic Logic { get; private set; }

        public bool IsAuthority { get { return true; } }
        public Gim002SyncState Current { get { return Logic.Snapshot(); } }

        public event Action<Gim002SyncState> OnStateSynced;
        public event Action<Gim002NetEvent, int> OnEvent;

        public LocalGim002Net(Gim002Config config)
        {
            Logic = new Gim002Logic(config.BuildRules(), config.timing);

            // 로직의 C# 이벤트를 네트워크 이벤트 형식으로 바꿔 전달한다
            Logic.OnChanged += () => OnStateSynced?.Invoke(Logic.Snapshot());
            Logic.OnStageJudged += ok => Raise(ok ? Gim002NetEvent.StageRight : Gim002NetEvent.StageWrong, Logic.SceneIndex);
            Logic.OnPianoJudged += ok => { if (!ok) Raise(Gim002NetEvent.PianoWrong, Logic.SceneIndex); };
            Logic.OnKeyRecorded += s => Raise(Gim002NetEvent.KeyPressed, (int)s);
            Logic.OnSceneCleared += n => Raise(Gim002NetEvent.SceneClear, n);
            Logic.OnComplete += () => Raise(Gim002NetEvent.Complete, Logic.ProgressCount);
        }

        void Raise(Gim002NetEvent e, int value) { OnEvent?.Invoke(e, value); }

        public void RequestGim001Solved() { Logic.NotifyGim001Solved(); }
        public void RequestMusicDoorOpened() { Logic.NotifyMusicDoorOpened(); }
        public void RequestStageWound() { Logic.NotifyStageWound(); }
        public void RequestCrankCompleted() { Logic.NotifyCrankCompleted(); }
        public void RequestSlotChanged(DollId? doll, BackgroundId? bg) { Logic.RequestSlots(doll, bg); }
        public void RequestPianoKey(SymbolId symbol) { Logic.PressKey(symbol); }

        // 재접속 처리: 진행 중이던 장면만 처음으로 (성공 장면·열쇠 사용 여부는 유지)
        public void ResetCurrentScene() { Logic.ResetCurrentScene(); }

        public void Tick(float deltaTime) { Logic.Tick(deltaTime); }
    }
}
