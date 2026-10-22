using System;

namespace Gim005
{
    // GIM-005 덤불 정원 미로의 판정 로직 (Unity 없이 동작하는 순수 C#, EditMode 테스트 대상).
    // 판정 담당(호스트, 혼자 플레이면 자기 자신) 한 곳에서만 돌린다. 시간은 Tick(dt)으로만 흐른다.
    //
    // 규칙 요약 (기획 6장):
    //  - Ready에서 A가 입구를 지나면(EnterMaze) Running, 타이머 시작.
    //  - 문양 13종 중 8종이 G1~G8에 중복 없이 배정된다. 새 시도마다 다시 배정하고, 같은 덤불에 직전 시도와 같은 문양은 오지 않는다.
    //  - B가 문양 버튼을 누르면: 배정된 문양 → 그 덤불이 열림 / 이미 연 문양 → 무시 / 배정 안 된 문양 → 오답 벌칙 + 패널 잠금.
    //  - 시간이 0이 되면 TimeOut(흐려짐 연출) → 끝나면 덤불 재생장 + 문양 재배정 + 타이머 초기화 → Ready.
    //  - Running 중 중앙 버튼을 누르면 Complete. 이후 모든 입력 무시.
    public class Gim005Logic
    {
        readonly Gim005Rules rules;
        readonly Random rng;
        readonly int[] assign;
        bool[] open;
        float remaining, lockRemaining, fxRemaining;
        bool bellRung;

        public Gim005State State { get; private set; }
        public int Attempt { get; private set; }
        public float Remaining { get { return remaining; } }
        public float LockRemaining { get { return lockRemaining; } }
        public int GateCount { get { return rules.gateCount; } }

        public event Action OnChanged;              // 상태가 바뀔 때마다 (동기화용)
        public event Action OnStarted;
        public event Action<int> OnGateOpened;      // 덤불 번호
        public event Action<int> OnWrong;           // 누른 문양 번호
        public event Action OnBell;
        public event Action OnTimeOut;
        public event Action OnAttemptReset;
        public event Action OnComplete;

        public Gim005Logic(Gim005Rules rules, int seed)
        {
            rules.Validate();
            this.rules = rules;
            rng = new Random(seed);
            assign = new int[rules.gateCount];
            open = new bool[rules.gateCount];
            for (int i = 0; i < assign.Length; i++) assign[i] = -1;
            Attempt = 1;
            Reassign();
            remaining = rules.timeLimit;
            State = Gim005State.Ready;
        }

        public int SymbolOfGate(int gate) { return assign[gate]; }
        public bool IsGateOpen(int gate) { return State == Gim005State.Complete || open[gate]; }

        public int GateOfSymbol(int symbol)
        {
            for (int g = 0; g < assign.Length; g++) if (assign[g] == symbol) return g;
            return -1;
        }

        // A가 입구를 통과 → 타이머 시작
        public bool EnterMaze()
        {
            if (State != Gim005State.Ready) return false;
            State = Gim005State.Running;
            remaining = rules.timeLimit;
            lockRemaining = 0f;
            bellRung = false;
            OnStarted?.Invoke();
            OnChanged?.Invoke();
            return true;
        }

        // B가 문양 버튼을 누름 (symbol: 0 ~ symbolCount-1)
        public Gim005PressResult PressSymbol(int symbol)
        {
            if (State != Gim005State.Running || symbol < 0 || symbol >= rules.symbolCount) return Gim005PressResult.Ignored;
            if (lockRemaining > 0f) return Gim005PressResult.Locked;

            int gate = GateOfSymbol(symbol);
            if (gate < 0)
            {
                ApplyWrong(symbol);
                return Gim005PressResult.Wrong;
            }
            if (open[gate]) return Gim005PressResult.AlreadyOpen;

            open[gate] = true;
            OnGateOpened?.Invoke(gate);
            OnChanged?.Invoke();
            return Gim005PressResult.Opened;
        }

        void ApplyWrong(int symbol)
        {
            lockRemaining = rules.panelLockSeconds;
            if (rules.wrongPolicy != Gim005WrongPolicy.RegrowOpened) remaining -= rules.wrongTimePenalty;
            if (rules.wrongPolicy != Gim005WrongPolicy.TimePenalty) for (int g = 0; g < open.Length; g++) open[g] = false;
            OnWrong?.Invoke(symbol);
            if (remaining <= 0f) BeginTimeOut();
            else OnChanged?.Invoke();
        }

        // A가 중앙 버튼을 누름
        public bool PressCenter()
        {
            if (State != Gim005State.Running) return false;
            State = Gim005State.Complete;
            for (int g = 0; g < open.Length; g++) open[g] = true;
            lockRemaining = 0f;
            OnComplete?.Invoke();
            OnChanged?.Invoke();
            return true;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (State == Gim005State.Running)
            {
                bool lockEnded = false;
                if (lockRemaining > 0f) { lockRemaining = Math.Max(0f, lockRemaining - dt); lockEnded = lockRemaining <= 0f; }
                remaining -= dt;
                if (!bellRung && rules.bellAtRemaining > 0f && remaining <= rules.bellAtRemaining && remaining > 0f)
                {
                    bellRung = true;
                    OnBell?.Invoke();
                }
                if (remaining <= 0f) BeginTimeOut();
                else if (lockEnded) OnChanged?.Invoke();   // 잠금 해제를 화면에 알림
            }
            else if (State == Gim005State.TimeOut)
            {
                fxRemaining -= dt;
                if (fxRemaining <= 0f) StartNewAttempt();
            }
        }

        // 재접속 처리 (기획 7장): 완료 여부는 유지, 진행 중 시도는 Ready로 되돌리고 문양 재배정
        public void ResetAttempt()
        {
            if (State != Gim005State.Running && State != Gim005State.TimeOut) return;
            StartNewAttempt();
            OnAttemptReset?.Invoke();
        }

        void BeginTimeOut()
        {
            State = Gim005State.TimeOut;
            remaining = 0f;
            lockRemaining = 0f;
            fxRemaining = rules.timeoutFxSeconds;
            OnTimeOut?.Invoke();
            OnChanged?.Invoke();
            if (fxRemaining <= 0f) StartNewAttempt();
        }

        void StartNewAttempt()
        {
            Attempt++;
            open = new bool[rules.gateCount];
            Reassign();
            remaining = rules.timeLimit;
            lockRemaining = 0f;
            bellRung = false;
            State = Gim005State.Ready;
            OnChanged?.Invoke();
        }

        // 13종 중 gateCount종을 중복 없이 뽑아 덤불에 배정. 직전 시도와 같은 덤불에 같은 문양이 오지 않게 다시 뽑는다.
        void Reassign()
        {
            var prev = (int[])assign.Clone();
            var pool = new int[rules.symbolCount];
            for (int attempt = 0; attempt < 200; attempt++)
            {
                for (int i = 0; i < pool.Length; i++) pool[i] = i;
                // 앞에서부터 gateCount개만 섞는 부분 Fisher–Yates
                for (int i = 0; i < assign.Length; i++)
                {
                    int j = rng.Next(i, pool.Length);
                    int t = pool[i]; pool[i] = pool[j]; pool[j] = t;
                }
                bool ok = true;
                for (int g = 0; g < assign.Length; g++) if (pool[g] == prev[g]) { ok = false; break; }
                if (ok || attempt == 199) { Array.Copy(pool, assign, assign.Length); return; }
            }
        }

        public Gim005SyncState Snapshot()
        {
            byte mask = 0;
            for (int g = 0; g < open.Length; g++) if (open[g]) mask |= (byte)(1 << g);
            return new Gim005SyncState
            {
                state = State,
                attempt = Attempt,
                assignment = Gim005SyncState.Pack(assign),
                openMask = mask,
                gateCount = rules.gateCount,
                remaining = Math.Max(0f, remaining),
                lockRemaining = lockRemaining,
            };
        }
    }
}
