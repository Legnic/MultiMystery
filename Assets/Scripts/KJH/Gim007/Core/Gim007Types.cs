using System;

namespace Gim007
{
    // 기믹 상태 흐름 (명세서 6장):
    //  Idle(둘이 다 잡지 않음) → Ready(둘 다 잡음, 첫 라운드 전 잠깐 호흡) → Round(키 표시·판정)
    //   → 둘 다 성공: Moving(가구가 한 칸 미끄러짐) → 다음 Round … 마지막 칸이면 Complete
    //   → 한쪽이라도 실패: Resting(숨고르기) → 다음 Round
    //  도중에 누가 손을 놓으면 언제든 Idle로 (가구 위치는 유지)
    public enum Gim007State : byte { Idle, Ready, Round, Moving, Resting, Complete }

<<<<<<< HEAD
    // 라운드마다 각자 화면에 표시되는 키 (W/A/S/D + 방향키 4개, 네트워크로 byte 값이 오가므로 순서를 바꾸지 말고 끝에만 추가)
    public enum Gim007Key : byte { W, A, S, D, Up, Down, Left, Right }
=======
    // 라운드마다 각자 화면에 표시되는 키
    public enum Gim007Key : byte { W, A, S, D }
>>>>>>> 59f30c0e01c910926ccee6b1a0e22ef4a1b30780

    // 한 라운드에서 한 사람의 결과
    public enum Gim007RoundResult : byte { Pending, Success, Fail }

    // 손잡이를 잡으려 할 때 결과
    public enum Gim007GripResult
    {
        Ignored,        // 잘못된 손잡이 번호, 이미 내가 잡고 있음
        Locked,         // 완료 후라 잡을 수 없음
        Gripped,        // 잡음
        HandleTaken,    // 그 손잡이는 상대가 먼저 잡았음 → 반대쪽 안내
        AlreadyHolding, // 나는 이미 다른 손잡이를 잡고 있음
    }

    // 판정 담당 → 모두에게 보내는 일회성 사건 (소리·흔들림·문구용)
    public enum Gim007NetEvent
    {
        Ready,      // 둘 다 잡음 → 곧 첫 라운드 (value: 현재 칸)
        RoundStart, // 새 라운드 시작 (value: 라운드 번호)
        Success,    // 둘 다 성공 → 한 칸 전진 (value: 전진 후 칸)
        Fail,       // 실패 → 숨고르기 (value: 실패한 손잡이 비트 1=0번, 2=1번)
        Cancelled,  // 진행 중 한 명이 손을 놓음 (value: 현재 칸)
        Complete,   // 발판 도달 (value: 총 실패 횟수)
    }

    // 판정 규칙·수치 (Gim007Config 에셋 Inspector에서 바꾼다). 기본값은 명세서 제안값.
    [Serializable]
    public class Gim007Rules
    {
        public const int HandleCount = 2;
<<<<<<< HEAD
        public const int KeyCount = 8;
=======
        public const int KeyCount = 4;
>>>>>>> 59f30c0e01c910926ccee6b1a0e22ef4a1b30780

        // 발판까지 필요한 성공 횟수 (명세서 제안 8회). 1회 이동 거리 = 경로 길이 / 이 값
        public int requiredSteps = 8;
        // 키가 뜬 뒤 입력해야 하는 제한 시간(초) (제안 1.5초). 각 컴퓨터가 자기 화면 기준으로 잰다
        public float roundSeconds = 1.5f;
        // 실패 후 숨고르기(초) (제안 2초)
        public float restSeconds = 2f;
        // 둘 다 잡은 뒤 첫 라운드까지 기다리는 시간(초). "하나, 둘" 할 여유
        public float readySeconds = 1.2f;
        // 성공 후 가구가 미끄러지는 동안 다음 라운드를 기다리는 시간(초)
        public float moveSeconds = 0.7f;
        // 판정 담당이 제한 시간 뒤에 더 기다려 주는 시간(초). 게스트 입력이 네트워크로 늦게 도착해도 억울하지 않게
        public float latencyGraceSeconds = 0.35f;
<<<<<<< HEAD
        // true면 표시된 키와 다른 키(W/A/S/D·방향키)를 누르면 바로 실패 (2026-10-23 결정: 오입력도 실패)
=======
        // true면 표시된 키와 다른 W/A/S/D를 누르면 바로 실패 (2026-10-23 결정: 오입력도 실패)
>>>>>>> 59f30c0e01c910926ccee6b1a0e22ef4a1b30780
        public bool wrongKeyFails = true;

        // 규칙이 올바른지 검사. 문제가 없으면 null
        public string Validate()
        {
            if (requiredSteps < 1 || requiredSteps > 200) return "필요 성공 횟수는 1~200이어야 합니다.";
            if (roundSeconds <= 0.1f) return "제한 시간은 0.1초보다 커야 합니다.";
            if (restSeconds < 0f || readySeconds < 0f || moveSeconds < 0f || latencyGraceSeconds < 0f) return "대기 시간 값은 0 이상이어야 합니다.";
            return null;
        }
    }

    // 네트워크로 주고받는 상태 전체.
    // 손잡이를 잡은 사람(Netcode 클라이언트 ID, 없으면 NoPlayer), 진행 칸, 라운드 번호와 두 사람의 키·결과.
    // 키는 둘 다 들어 있지만 각 화면은 "자기 손잡이의 키"만 크게 보여 준다 (명세서 3장 정보 비대칭).
    public struct Gim007SyncState : IEquatable<Gim007SyncState>
    {
        public const ulong NoPlayer = ulong.MaxValue;

        public Gim007State state;
        public ulong grip0, grip1;
        public int step;
        public int round;
        public Gim007Key key0, key1;
        public Gim007RoundResult result0, result1;
        public int failCount;

        public static Gim007SyncState Empty
        {
            get { return new Gim007SyncState { state = Gim007State.Idle, grip0 = NoPlayer, grip1 = NoPlayer }; }
        }

        public ulong Grip(int handle) { return handle == 0 ? grip0 : handle == 1 ? grip1 : NoPlayer; }
        public void SetGrip(int handle, ulong player) { if (handle == 0) grip0 = player; else if (handle == 1) grip1 = player; }

        public Gim007Key Key(int handle) { return handle == 1 ? key1 : key0; }
        public Gim007RoundResult Result(int handle) { return handle == 1 ? result1 : result0; }
        public void SetResult(int handle, Gim007RoundResult r) { if (handle == 0) result0 = r; else if (handle == 1) result1 = r; }

        // player가 잡고 있는 손잡이 번호, 안 잡고 있으면 -1
        public int HandleOf(ulong player)
        {
            if (player == NoPlayer) return -1;
            if (grip0 == player) return 0;
            if (grip1 == player) return 1;
            return -1;
        }

        public int GripCount { get { return (grip0 != NoPlayer ? 1 : 0) + (grip1 != NoPlayer ? 1 : 0); } }
        public bool BothGripped { get { return grip0 != NoPlayer && grip1 != NoPlayer; } }
        public bool IsComplete { get { return state == Gim007State.Complete; } }
        // 둘이 함께 미는 중인가 (라운드·이동·숨고르기 포함)
        public bool IsPushing { get { return state == Gim007State.Ready || state == Gim007State.Round || state == Gim007State.Moving || state == Gim007State.Resting; } }

        public bool Equals(Gim007SyncState o)
        {
            return state == o.state && grip0 == o.grip0 && grip1 == o.grip1 && step == o.step && round == o.round
                && key0 == o.key0 && key1 == o.key1 && result0 == o.result0 && result1 == o.result1 && failCount == o.failCount;
        }

        public override bool Equals(object obj) { return obj is Gim007SyncState && Equals((Gim007SyncState)obj); }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)state;
                h = h * 397 ^ grip0.GetHashCode();
                h = h * 397 ^ grip1.GetHashCode();
                h = h * 397 ^ step;
                h = h * 397 ^ round;
                h = h * 397 ^ ((int)key0 | (int)key1 << 4 | (int)result0 << 8 | (int)result1 << 12);
                return h * 397 ^ failCount;
            }
        }

        static string G(ulong p) { return p == NoPlayer ? "_" : p.ToString(); }
        public override string ToString()
        {
            return state + " 손잡이[" + G(grip0) + "," + G(grip1) + "] 칸 " + step + " 라운드 " + round
                + " 키 " + key0 + "/" + key1 + " 결과 " + result0 + "/" + result1 + " 실패 " + failCount;
        }
    }
}
