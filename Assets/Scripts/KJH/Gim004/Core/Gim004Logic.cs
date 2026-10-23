using System;

namespace Gim004
{
    // GIM-004 온실 화분 퍼즐 판정 로직 (순수 C#, Unity 없이 테스트 가능).
    // 판정 담당(호스트, 혼자면 로컬) 한 곳에서만 돌린다. 화면 쪽은 Snapshot()과 이벤트만 본다.
    //
    // 규칙 (명세서 6장):
    //  - 화분을 선반 자리에 놓으면 정답 여부와 관계없이 그 자리 버튼만 눌린다 (개별 판정 없음).
    //  - 찬 자리에 다른 화분을 놓으면 서로 바뀐다: 든 화분이 놓이고, 있던 화분은 처음 자리로 (= 선반 상태에서 빠짐).
    //  - 선반 위 화분을 빼면 그 자리가 빈다.
    //  - 4자리가 모두 차면 judgeDelaySeconds 뒤 자동 판정.
    //    오답: 선반 화분 4개가 모두 처음 자리로 (선반 비움), 다시 배치. 정답: 완료, 선반 잠금(뺄 수 없음).
    public class Gim004Logic
    {
        readonly Gim004Rules rules;
        Gim004SyncState s = Gim004SyncState.Empty;
        float judgeTimer;

        public event Action OnChanged;
        public event Action OnJudging;
        public event Action OnWrong;
        public event Action OnComplete;

        public Gim004Logic(Gim004Rules rules)
        {
            this.rules = rules ?? new Gim004Rules();
        }

        public Gim004State State { get { return s.state; } }
        public int WrongCount { get { return s.wrongCount; } }
        public bool IsComplete { get { return s.state == Gim004State.Complete; } }
        public float JudgeRemaining { get { return s.state == Gim004State.Judging ? judgeTimer : 0f; } }

        public Gim004SyncState Snapshot() { return s; }
        public int Slot(int i) { return s.Slot(i); }

        static bool ValidSlot(int slot) { return slot >= 0 && slot < Gim004Rules.SlotCount; }
        static bool ValidPot(int pot) { return pot >= 0 && pot < Gim004Rules.PotCount; }

        // 화분 pot을 자리 slot에 놓는다
        public Gim004PlaceResult Place(int slot, int pot)
        {
            if (!ValidSlot(slot) || !ValidPot(pot)) return Gim004PlaceResult.Ignored;
            if (s.state != Gim004State.Placing) return Gim004PlaceResult.Locked;
            if (s.Slot(slot) == pot) return Gim004PlaceResult.Ignored;

            var result = Gim004PlaceResult.Placed;
            int from = s.SlotOf(pot);
            if (from >= 0) { s.SetSlot(from, -1); result = Gim004PlaceResult.Moved; }   // 다른 자리에서 옮겨 옴
            if (s.Slot(slot) >= 0 && result == Gim004PlaceResult.Placed) result = Gim004PlaceResult.Swapped;
            s.SetSlot(slot, pot);   // 있던 화분은 선반 상태에서 빠진다 → 화면에서 처음 자리로 돌아감

            if (s.FilledCount == Gim004Rules.SlotCount)
            {
                s.state = Gim004State.Judging;
                judgeTimer = Math.Max(0f, rules.judgeDelaySeconds);
                OnChanged?.Invoke();
                OnJudging?.Invoke();
                if (judgeTimer <= 0f) Judge();
                return result;
            }
            OnChanged?.Invoke();
            return result;
        }

        // 자리 slot의 화분을 뺀다 (손에 들기). 성공하면 true
        public bool Take(int slot)
        {
            if (!ValidSlot(slot) || s.state != Gim004State.Placing) return false;
            if (s.Slot(slot) < 0) return false;
            s.SetSlot(slot, -1);
            OnChanged?.Invoke();
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (s.state != Gim004State.Judging) return;
            judgeTimer -= deltaTime;
            if (judgeTimer <= 0f) Judge();
        }

        // 지금 선반 배치가 정답인가 (왼쪽부터 answer와 같은가)
        public bool IsCorrect()
        {
            var ans = rules.answer;
            if (ans == null || ans.Length != Gim004Rules.SlotCount) return false;
            for (int i = 0; i < Gim004Rules.SlotCount; i++) if (s.Slot(i) != (int)ans[i]) return false;
            return true;
        }

        void Judge()
        {
            if (IsCorrect())
            {
                s.state = Gim004State.Complete;
                OnChanged?.Invoke();
                OnComplete?.Invoke();
                return;
            }
            for (int i = 0; i < Gim004Rules.SlotCount; i++) s.SetSlot(i, -1);
            s.wrongCount++;
            s.state = Gim004State.Placing;
            OnChanged?.Invoke();
            OnWrong?.Invoke();
        }
    }
}
