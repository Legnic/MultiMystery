using System;

namespace Gim004
{
    // 기믹 상태 흐름: Placing(화분 배치 중) → Judging(4자리가 다 차서 판정 대기) → 오답이면 Placing / 정답이면 Complete
    public enum Gim004State : byte { Placing, Judging, Complete }

    // 화분 7개의 개발용 ID. 게임 화면에는 절대 표시하지 않는다 (명세서 2장).
    //  A 흰 종꽃 2송이·둥근 잎(정답)   B 흰 종꽃 3송이·뾰족 잎
    //  C 옅은 노란 별꽃·뾰족 잎        D 흰 별꽃(가운데 진노랑)·둥근 잎(정답)
    //  E 잎 안쪽 구멍(정답)            F 잎 가장자리 갈라짐(정답)          G 기둥 선인장
    public enum Gim004PotId : byte { A, B, C, D, E, F, G }

    // 선반 자리에 화분을 놓았을 때 결과
    public enum Gim004PlaceResult
    {
        Ignored,    // 잘못된 번호, 이미 그 자리에 같은 화분
        Locked,     // 판정 중 / 완료 후라 선반을 바꿀 수 없음
        Placed,     // 빈 자리에 놓음
        Swapped,    // 찬 자리에 놓음 → 있던 화분은 처음 자리로
        Moved,      // 다른 선반 자리에 있던 화분을 이 자리로 옮김
    }

    // 판정 담당 → 모두에게 보내는 일회성 사건
    public enum Gim004NetEvent
    {
        Judging,    // 4자리가 다 참 (value: 시도 번호)
        Wrong,      // 오답 → 화분 4개 원위치 (value: 누적 오답 횟수)
        Complete,   // 정답 → 정원문·정원 관리실 문 개방
    }

    // 판정 규칙 (Gim004Config 에셋 Inspector에서 바꾼다)
    [Serializable]
    public class Gim004Rules
    {
        public const int SlotCount = 4;
        public const int PotCount = 7;

        // 선반 정면에서 바라본 왼쪽부터의 정답 (명세서 확정: D-E-A-F)
        public Gim004PotId[] answer = { Gim004PotId.D, Gim004PotId.E, Gim004PotId.A, Gim004PotId.F };

        // 4번째 화분이 놓인 뒤 판정까지 기다리는 시간(초). 놓이는 모습을 보고 나서 결과가 나오게 한다.
        public float judgeDelaySeconds = 0.8f;

        // 규칙이 올바른지 검사. 문제가 없으면 null
        public string Validate()
        {
            if (answer == null || answer.Length != SlotCount) return "정답은 화분 " + SlotCount + "개여야 합니다.";
            for (int i = 0; i < answer.Length; i++)
            {
                if ((int)answer[i] >= PotCount) return "정답 " + (i + 1) + "번째 화분 ID가 잘못되었습니다.";
                for (int j = 0; j < i; j++)
                    if (answer[i] == answer[j]) return "정답에 같은 화분(" + answer[i] + ")이 두 번 들어 있습니다.";
            }
            if (judgeDelaySeconds < 0f) return "판정 대기 시간은 0 이상이어야 합니다.";
            return null;
        }
    }

    // 네트워크로 주고받는 상태 전체. 선반 자리 4개에 놓인 화분(-1 = 비어 있음), 상태, 오답 횟수.
    // 화분이 "손에 들려 있음"은 동기화하지 않는다 (공용 들기 시스템 원칙: 손은 로컬 전용).
    // 선반에 없는 화분 = 처음 자리(또는 누군가의 손)이다.
    public struct Gim004SyncState : IEquatable<Gim004SyncState>
    {
        public Gim004State state;
        public sbyte s0, s1, s2, s3;
        public int wrongCount;

        public static Gim004SyncState Empty
        {
            get { return new Gim004SyncState { state = Gim004State.Placing, s0 = -1, s1 = -1, s2 = -1, s3 = -1 }; }
        }

        // 자리 i에 놓인 화분 번호 (0~6), 비어 있으면 -1
        public int Slot(int i)
        {
            switch (i)
            {
                case 0: return s0;
                case 1: return s1;
                case 2: return s2;
                case 3: return s3;
                default: return -1;
            }
        }

        public void SetSlot(int i, int pot)
        {
            sbyte v = (sbyte)(pot < 0 ? -1 : pot);
            switch (i)
            {
                case 0: s0 = v; break;
                case 1: s1 = v; break;
                case 2: s2 = v; break;
                case 3: s3 = v; break;
            }
        }

        // 화분 pot이 놓인 자리 번호, 선반에 없으면 -1
        public int SlotOf(int pot)
        {
            if (pot < 0) return -1;
            for (int i = 0; i < Gim004Rules.SlotCount; i++) if (Slot(i) == pot) return i;
            return -1;
        }

        public int FilledCount
        {
            get { int n = 0; for (int i = 0; i < Gim004Rules.SlotCount; i++) if (Slot(i) >= 0) n++; return n; }
        }

        public bool IsComplete { get { return state == Gim004State.Complete; } }

        public bool Equals(Gim004SyncState o)
        {
            return state == o.state && s0 == o.s0 && s1 == o.s1 && s2 == o.s2 && s3 == o.s3 && wrongCount == o.wrongCount;
        }

        public override bool Equals(object obj) { return obj is Gim004SyncState && Equals((Gim004SyncState)obj); }
        public override int GetHashCode() { return ((int)state * 397) ^ (s0 << 24 | (s1 & 0xFF) << 16 | (s2 & 0xFF) << 8 | (s3 & 0xFF)) ^ wrongCount; }

        static string P(int v) { return v < 0 ? "_" : ((Gim004PotId)v).ToString(); }
        public override string ToString() { return state + " [" + P(s0) + P(s1) + P(s2) + P(s3) + "] 오답 " + wrongCount; }
    }
}
