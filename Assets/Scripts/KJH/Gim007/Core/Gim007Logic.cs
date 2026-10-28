using System;

namespace Gim007
{
    // GIM-007 함께 미는 안방 가구 판정 로직 (순수 C#, Unity 없이 테스트 가능).
    // 판정 담당(호스트, 혼자면 로컬) 한 곳에서만 돌린다. 화면 쪽은 Snapshot()과 이벤트만 본다.
    //
    // 규칙 (명세서 6장·7장):
    //  - 손잡이 2개. 먼저 잡은 사람이 아무 쪽이나 잡는다. 같은 손잡이는 먼저 도착한 요청이 차지한다.
    //  - 한 명만 잡으면 가구는 움직이지 않는다 (Idle 유지, 잡은 채로 기다릴 수 있음).
    //  - 둘 다 잡으면 readySeconds 뒤 라운드 시작: 두 손잡이에 W/A/S/D·방향키 8개 중 하나씩 독립적으로 랜덤.
    //  - 라운드 판정은 각 컴퓨터가 자기 화면 기준으로 잰 결과(성공/실패)를 보고(Report)하고, 여기서 모은다.
    //    둘 다 성공 → 한 칸 전진(Moving) / 한쪽이라도 실패 또는 판정 담당 기준 제한 시간+여유 초과 → 숨고르기(Resting).
    //    진행 칸은 실패해도 줄지 않는다.
    //  - 진행 중 누가 손을 놓으면 라운드 취소, 칸 유지, Idle. 다시 둘이 잡으면 이어서.
    //  - 마지막 칸에 도달하면 Complete. 두 사람 손잡이를 자동으로 놓는다. 이후 잡기 불가.
    public class Gim007Logic
    {
        public const ulong NoPlayer = Gim007SyncState.NoPlayer;

        readonly Gim007Rules rules;
        readonly Random rng;
        Gim007SyncState s = Gim007SyncState.Empty;
        float timer;

        public event Action OnChanged;
        public event Action<Gim007NetEvent, int> OnEvent;

        public Gim007Logic(Gim007Rules rules, int seed = 0)
        {
            this.rules = rules ?? new Gim007Rules();
            rng = seed == 0 ? new Random() : new Random(seed);
        }

        public Gim007Rules Rules { get { return rules; } }
        public Gim007State State { get { return s.state; } }
        public int Step { get { return s.step; } }
        public bool IsComplete { get { return s.state == Gim007State.Complete; } }
        public float TimerRemaining { get { return timer; } }

        public Gim007SyncState Snapshot() { return s; }

        // 테스트·네트워크 복구용: 저장된 진행 칸에서 시작 (손잡이는 비운다)
        public void Restore(int step, bool complete)
        {
            s = Gim007SyncState.Empty;
            s.step = Math.Max(0, Math.Min(step, rules.requiredSteps));
            if (complete || s.step >= rules.requiredSteps) { s.step = rules.requiredSteps; s.state = Gim007State.Complete; }
            timer = 0f;
            OnChanged?.Invoke();
        }

        // 개발자 모드 전용 (DevGimmickPanel): 가구를 끝 칸까지 밀어 바로 완료 상태로 만든다 (문 열림 연출까지 일어나도록 Complete 사건도 보낸다).
        public void DebugForceComplete()
        {
            if (s.IsComplete) return;
            s.step = rules.requiredSteps;
            s.state = Gim007State.Complete;
            s.grip0 = s.grip1 = NoPlayer;
            timer = 0f;
            OnChanged?.Invoke();
            OnEvent?.Invoke(Gim007NetEvent.Complete, s.failCount);
        }

        // ---------------- 손잡이 ----------------

        public Gim007GripResult Grip(int handle, ulong player)
        {
            if (handle < 0 || handle >= Gim007Rules.HandleCount || player == NoPlayer) return Gim007GripResult.Ignored;
            if (s.IsComplete) return Gim007GripResult.Locked;
            ulong cur = s.Grip(handle);
            if (cur == player) return Gim007GripResult.Ignored;
            if (cur != NoPlayer) return Gim007GripResult.HandleTaken;
            if (s.HandleOf(player) >= 0) return Gim007GripResult.AlreadyHolding;

            s.SetGrip(handle, player);
            if (s.BothGripped)
            {
                s.state = Gim007State.Ready;
                s.result0 = s.result1 = Gim007RoundResult.Pending;
                timer = rules.readySeconds;
                OnChanged?.Invoke();
                OnEvent?.Invoke(Gim007NetEvent.Ready, s.step);
                if (timer <= 0f) StartRound();
                return Gim007GripResult.Gripped;
            }
            OnChanged?.Invoke();
            return Gim007GripResult.Gripped;
        }

        // player가 잡은 손잡이를 놓는다 (손 떼기, 접속 끊김). 잡고 있었으면 true
        public bool Release(ulong player)
        {
            int h = s.HandleOf(player);
            if (h < 0) return false;
            s.SetGrip(h, NoPlayer);
            bool wasPushing = s.IsPushing;
            if (wasPushing)
            {
                s.state = Gim007State.Idle;
                s.result0 = s.result1 = Gim007RoundResult.Pending;
                timer = 0f;
            }
            OnChanged?.Invoke();
            if (wasPushing) OnEvent?.Invoke(Gim007NetEvent.Cancelled, s.step);
            return true;
        }

        // ---------------- 라운드 ----------------

        // player가 라운드 round의 결과를 보고한다. 지난 라운드의 늦은 보고, 이미 낸 결과는 무시한다.
        public bool Report(ulong player, int round, bool success)
        {
            if (s.state != Gim007State.Round || round != s.round) return false;
            int h = s.HandleOf(player);
            if (h < 0 || s.Result(h) != Gim007RoundResult.Pending) return false;

            s.SetResult(h, success ? Gim007RoundResult.Success : Gim007RoundResult.Fail);
            if (!success) { FailRound(); return true; }
            if (s.result0 == Gim007RoundResult.Success && s.result1 == Gim007RoundResult.Success) { SucceedRound(); return true; }
            OnChanged?.Invoke();
            return true;
        }

        public void Tick(float deltaTime)
        {
            switch (s.state)
            {
                case Gim007State.Ready:
                case Gim007State.Moving:
                case Gim007State.Resting:
                    timer -= deltaTime;
                    if (timer <= 0f) StartRound();
                    break;
                case Gim007State.Round:
                    timer -= deltaTime;
                    if (timer <= 0f)
                    {
                        // 제한 시간 + 여유가 지나도 결과가 안 온 사람은 시간 초과 실패
                        if (s.result0 == Gim007RoundResult.Pending) s.result0 = Gim007RoundResult.Fail;
                        if (s.result1 == Gim007RoundResult.Pending) s.result1 = Gim007RoundResult.Fail;
                        FailRound();
                    }
                    break;
            }
        }

        // 각 컴퓨터가 보고하기 전에 쓰는 공용 입력 규칙.
        // true = 성공 보고, false = 실패 보고, null = 무시(틀린 키를 실패로 보지 않는 설정일 때)
        public static bool? JudgeKey(Gim007Rules rules, Gim007Key shown, Gim007Key pressed)
        {
            if (shown == pressed) return true;
            return rules.wrongKeyFails ? false : (bool?)null;
        }

        void StartRound()
        {
            s.round++;
            s.key0 = (Gim007Key)rng.Next(Gim007Rules.KeyCount);
            s.key1 = (Gim007Key)rng.Next(Gim007Rules.KeyCount);
            s.result0 = s.result1 = Gim007RoundResult.Pending;
            s.state = Gim007State.Round;
            timer = rules.roundSeconds + rules.latencyGraceSeconds;
            OnChanged?.Invoke();
            OnEvent?.Invoke(Gim007NetEvent.RoundStart, s.round);
        }

        void SucceedRound()
        {
            s.step++;
            if (s.step >= rules.requiredSteps)
            {
                s.step = rules.requiredSteps;
                s.state = Gim007State.Complete;
                s.grip0 = s.grip1 = NoPlayer;   // 자동으로 손을 뗀다
                timer = 0f;
                OnChanged?.Invoke();
                OnEvent?.Invoke(Gim007NetEvent.Success, s.step);
                OnEvent?.Invoke(Gim007NetEvent.Complete, s.failCount);
                return;
            }
            s.state = Gim007State.Moving;
            timer = rules.moveSeconds;
            OnChanged?.Invoke();
            OnEvent?.Invoke(Gim007NetEvent.Success, s.step);
        }

        void FailRound()
        {
            int who = (s.result0 == Gim007RoundResult.Fail ? 1 : 0) | (s.result1 == Gim007RoundResult.Fail ? 2 : 0);
            s.failCount++;
            s.state = Gim007State.Resting;
            timer = rules.restSeconds;
            OnChanged?.Invoke();
            OnEvent?.Invoke(Gim007NetEvent.Fail, who);
        }
    }
}
