using System;

namespace Gim005
{
    // 기믹 상태 흐름: Ready(입구 앞 대기) → Running(미로 진행) → TimeOut(의식 흐려짐) → Ready … → Complete
    public enum Gim005State : byte { Ready, Running, TimeOut, Complete }

    // 오답(이번 시도에 배정되지 않은 문양 버튼)을 눌렀을 때의 벌칙
    public enum Gim005WrongPolicy
    {
        TimePenalty,    // 남은 시간 차감 (기획 6장 기본값)
        RegrowOpened,   // 이번 시도에 열린 덤불이 다시 자란다
        Both,           // 둘 다
    }

    // 문양 버튼을 눌렀을 때 판정 결과
    public enum Gim005PressResult
    {
        Ignored,        // 지금은 받지 않음 (Ready / TimeOut / Complete, 잘못된 번호)
        Locked,         // 오답 후 패널 잠금 중
        AlreadyOpen,    // 이미 연 문양 → 아무 일 없음
        Opened,         // 배정된 덤불이 사라짐
        Wrong,          // 오답 → 벌칙
    }

    // 판정 담당 → 모두에게 보내는 일회성 사건. 값(value)의 뜻은 사건마다 다르다.
    public enum Gim005NetEvent
    {
        Started,        // 입구 통과, 타이머 시작 (value: 시도 번호)
        GateOpened,     // 덤불 하나가 사라짐 (value: 덤불 번호 0~7 = G1~G8)
        Wrong,          // 오답 (value: 누른 문양 번호)
        Bell,           // 남은 시간 경고 종소리 (value: 남은 초)
        TimeOut,        // 시간 초과 → 흐려짐 연출 시작 (value: 시도 번호)
        AttemptReset,   // 재접속 등으로 진행 중 시도를 처음으로 (value: 새 시도 번호)
        Complete,       // 중앙 버튼 → 기믹 완료
    }

    // 판정 규칙 수치 (전부 가안 🟡 — Gim005Config 에셋 Inspector에서 바꾼다)
    [Serializable]
    public class Gim005Rules
    {
        public const int MaxGates = 8;      // 동기화 상태가 덤불 8개(바이트 1개) 기준
        public const int MaxSymbols = 16;   // 덤불 하나에 문양 번호 4비트

        public int symbolCount = 13;        // 버튼 패널의 문양 수
        public int gateCount = 8;           // 문양 덤불 수 (G1~G8)
        public float timeLimit = 180f;      // 제한 시간 (초)
        public Gim005WrongPolicy wrongPolicy = Gim005WrongPolicy.TimePenalty;
        public float wrongTimePenalty = 15f;    // 오답 시 시간 차감 (초)
        public float panelLockSeconds = 3f;     // 오답 후 패널 잠금 (초)
        public float timeoutFxSeconds = 2f;     // 시간 초과 흐려짐 연출 (초). 끝나면 다음 시도(Ready)
        public float bellAtRemaining = 30f;     // 남은 시간이 이 값 이하가 되면 종소리 1회 (0이면 끔)

        public void Validate()
        {
            if (gateCount < 1 || gateCount > MaxGates) throw new ArgumentException("gateCount는 1~" + MaxGates + " 사이여야 합니다: " + gateCount);
            if (symbolCount < gateCount || symbolCount > MaxSymbols) throw new ArgumentException("symbolCount는 gateCount~" + MaxSymbols + " 사이여야 합니다: " + symbolCount);
            if (timeLimit <= 0f) throw new ArgumentException("timeLimit은 0보다 커야 합니다");
        }
    }

    // 네트워크로 주고받는 상태 한 장. 화면 쪽은 이것만 보고 그린다.
    public struct Gim005SyncState
    {
        public Gim005State state;
        public int attempt;             // 몇 번째 시도인지 (1부터)
        public uint assignment;         // 덤불별 문양 번호 (덤불 i = (assignment >> 4i) & 0xF)
        public byte openMask;           // 이번 시도에 열린 덤불 (비트 i = 덤불 i)
        public int gateCount;
        public float remaining;         // 이 상태가 만들어진 순간의 남은 시간 (초)
        public float lockRemaining;     // 이 상태가 만들어진 순간의 패널 잠금 남은 시간 (초)

        public int SymbolOfGate(int gate) { return (int)((assignment >> (gate * 4)) & 0xF); }
        public bool IsGateOpen(int gate) { return state == Gim005State.Complete || (openMask & (1 << gate)) != 0; }

        // 이 문양이 배정된 덤불 번호 (없으면 -1)
        public int GateOfSymbol(int symbol)
        {
            for (int g = 0; g < gateCount; g++) if (SymbolOfGate(g) == symbol) return g;
            return -1;
        }

        // 버튼 불빛: 이번 시도에 이 문양으로 덤불을 열었는가
        public bool IsSymbolOpen(int symbol)
        {
            int g = GateOfSymbol(symbol);
            return g >= 0 && (openMask & (1 << g)) != 0;
        }

        public static uint Pack(int[] symbols)
        {
            uint v = 0;
            for (int i = 0; i < symbols.Length; i++) v |= (uint)(symbols[i] & 0xF) << (i * 4);
            return v;
        }

        public override string ToString()
        {
            var s = state + " 시도" + attempt + " 남은" + remaining.ToString("0.0") + "s 덤불[";
            for (int g = 0; g < gateCount; g++) s += (g > 0 ? " " : "") + "G" + (g + 1) + "=" + (SymbolOfGate(g) + 1) + (IsGateOpen(g) ? "(열림)" : "");
            return s + "]" + (lockRemaining > 0f ? " 잠금" + lockRemaining.ToString("0.0") + "s" : "");
        }
    }
}
