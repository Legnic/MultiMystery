using System.Collections.Generic;

namespace Gim002
{
    // 반주 입력 버퍼 (기획 6-2 "반주 판정").
    // - 기호 건반만 기록한다. 기호 없는 건반(null)은 소리만 나고 여기에는 들어오지 않는다.
    // - 기록 개수가 장면 음 수와 같아지는 순간 한 번만 순서를 비교하고, 결과와 상관없이 버퍼를 비운다.
    // - 중간에 틀려도 미리 실패시키지 않는다 (개수가 찰 때까지 기다림).
    // "지금 기록해도 되는가(반주판 열림 등)"는 이 클래스가 아니라 Gim002Logic이 판단한다. 이 클래스는 순수하게 쌓고 비교만 한다.
    public class PianoInputBuffer
    {
        public enum Result
        {
            Ignored,   // 기호 없는 건반이라 기록 안 함
            Recorded,  // 기록함. 아직 개수가 덜 참
            Correct,   // 개수가 차서 판정 → 정답 (버퍼 비움)
            Wrong      // 개수가 차서 판정 → 오답 (버퍼 비움)
        }

        readonly List<SymbolId> pressed = new List<SymbolId>();

        public int Count { get { return pressed.Count; } }
        public IReadOnlyList<SymbolId> Pressed { get { return pressed; } }

        // symbol: 누른 건반의 기호 (기호 없는 건반이면 null). expected: 현재 장면의 정답 기호 순서.
        public Result Push(SymbolId? symbol, IReadOnlyList<SymbolId> expected)
        {
            if (!symbol.HasValue || expected == null || expected.Count == 0) return Result.Ignored;

            pressed.Add(symbol.Value);
            if (pressed.Count < expected.Count) return Result.Recorded;

            // 개수가 찼다 → 순서 비교
            bool ok = true;
            for (int i = 0; i < expected.Count; i++)
                if (pressed[i] != expected[i]) { ok = false; break; }
            pressed.Clear();
            return ok ? Result.Correct : Result.Wrong;
        }

        public void Clear() { pressed.Clear(); }
    }
}
