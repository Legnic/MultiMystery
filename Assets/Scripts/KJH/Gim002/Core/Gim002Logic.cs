using System;
using System.Collections.Generic;

namespace Gim002
{
    // GIM-002 판정 로직 (기획 6-3 상태 전이표를 그대로 옮긴 것). MonoBehaviour가 아닌 순수 C# 클래스다.
    //
    // 왜 순수 클래스인가?
    //  - 판정은 호스트 한 곳에서만 해야 한다. 화면(커튼, 인형 등)과 분리해 두면 호스트는 이 객체를 돌리고,
    //    클라이언트는 결과(Gim002SyncState)만 받아 화면을 맞추면 된다.
    //  - EditMode 테스트에서 Unity 씬 없이 new로 만들어 바로 검사할 수 있다.
    //
    // 시간 처리: 코루틴 대신 Tick(deltaTime)으로 시간을 흘려보낸다. 테스트에서 Tick(0.3f)처럼 시간을 직접 넣을 수 있다.
    // 연출 중(커튼 동작, 판정 대기, 오답 연출, 공연, 정리)에는 IsBusy가 true이고 그동안 들어온 요청은 무시한다.
    public class Gim002Logic
    {
        // ---------- 결과 알림 (C# event) ----------
        public event Action<Gim002State> OnStateChanged;   // 상태가 바뀜
        public event Action<bool> OnStageJudged;           // 무대 판정 (true = 정답)
        public event Action<bool> OnPianoJudged;           // 반주 판정 (true = 정답)
        public event Action<SymbolId> OnKeyRecorded;       // 기호 건반이 기록됨 (2층 오르골 등 선택 연출용)
        public event Action<int> OnSceneCleared;           // 장면 성공 (장면 번호)
        public event Action OnComplete;                    // 3장면 모두 성공
        public event Action OnChanged;                     // 동기화할 값(Snapshot) 중 하나라도 바뀜 → 네트워크로 보낼 때 사용

        // ---------- 동기화되는 상태 ----------
        public Gim002State State { get; private set; }
        public int SceneIndex { get; private set; }        // 1~SceneCount
        public int ProgressCount { get; private set; }     // 0~SceneCount
        public bool MusicDoorOpened { get; private set; }
        public bool StageWound { get; private set; }
        public bool CurtainOpen { get; private set; }
        public DollId? SlotDoll { get; private set; }
        public BackgroundId? SlotBg { get; private set; }
        public bool ScoreboardOpen { get; private set; }

        // ---------- 내부 상태 ----------
        public bool IsBusy { get { return phase != Phase.None; } }
        public int SceneCount { get { return rules.Length; } }
        public Gim002SceneRule CurrentRule { get { return rules[Math.Max(0, Math.Min(SceneIndex, rules.Length) - 1)]; } }
        public PianoInputBuffer Piano { get { return piano; } }

        // 연출 진행 단계. 각 단계는 timer초 동안 유지되고, 끝나면 Advance()가 다음 단계로 넘긴다.
        enum Phase
        {
            None,
            CurtainOpening,     // 손잡이 성공 → 커튼 열리는 중
            JudgeDelay,         // 두 슬롯이 다 참 → 판정 전 대기 (0.3초)
            StageWrongFlash,    // 무대 오답 빨간 화면
            StageWrongReturn,   // 인형·카드가 제자리로 돌아가는 중
            StageWrongClosing,  // 커튼 닫히는 중
            PianoWrong,         // 반주 오답 연출 (건반 기록 안 함)
            Performing,         // 인형 공연
            Cleanup             // 공연 후 정리 (커튼 닫힘, 원위치, 반주판 닫힘)
        }

        readonly Gim002SceneRule[] rules;
        readonly Gim002Timing timing;
        readonly PianoInputBuffer piano = new PianoInputBuffer();
        // 시간 비교 허용 오차. 0.1초씩 여러 번 더하면 float 오차로 3.0이 2.9999998이 되는 경우가 있어서, 그만큼은 "끝남"으로 본다.
        const float Epsilon = 0.0001f;

        Phase phase = Phase.None;
        float timer;
        Gim002SyncState lastSnapshot;

        public Gim002Logic(IList<Gim002SceneRule> sceneRules, Gim002Timing timing)
        {
            if (sceneRules == null || sceneRules.Count == 0) throw new ArgumentException("장면 데이터가 최소 1개는 있어야 합니다.");
            rules = new Gim002SceneRule[sceneRules.Count];
            sceneRules.CopyTo(rules, 0);
            this.timing = timing ?? new Gim002Timing();
            State = Gim002State.Locked;
            SceneIndex = 1;
            lastSnapshot = Snapshot();
        }

        // 현재 동기화 상태를 한 덩어리로 만든다 (네트워크 전송·화면 갱신용).
        public Gim002SyncState Snapshot()
        {
            return new Gim002SyncState
            {
                state = State,
                sceneIndex = SceneIndex,
                progressCount = ProgressCount,
                musicDoorOpened = MusicDoorOpened,
                stageWound = StageWound,
                curtainOpen = CurtainOpen,
                slotDoll = Gim002SyncState.Encode(SlotDoll),
                slotBg = Gim002SyncState.Encode(SlotBg),
                scoreboardOpen = ScoreboardOpen,
            };
        }

        // =====================================================================
        // 입력 (요청). 모두 "받아들였으면 true, 무시했으면 false"를 돌려준다.
        // =====================================================================

        // LOCKED → PREPARING: GIM-001(초상화 퍼즐)이 풀렸다.
        public bool NotifyGim001Solved()
        {
            if (State != Gim002State.Locked) return false;
            SetState(Gim002State.Preparing);
            Commit();
            return true;
        }

        // PREPARING: 1층 음악실 문이 열쇠로 열렸다 → 2층 "반주자" 표시등 ON
        public bool NotifyMusicDoorOpened()
        {
            if (State != Gim002State.Preparing || MusicDoorOpened) return false;
            MusicDoorOpened = true;
            TryStart();
            Commit();
            return true;
        }

        // PREPARING: 2층 무대에 태엽 열쇠를 썼다 → 무대 조명 ON, 1층 "무대" 표시등 ON
        public bool NotifyStageWound()
        {
            if (State != Gim002State.Preparing || StageWound) return false;
            StageWound = true;
            TryStart();
            Commit();
            return true;
        }

        // 두 표시등이 모두 켜지면 장면 1을 커튼 닫힌 상태로 시작한다. 순서는 상관없다.
        void TryStart()
        {
            if (MusicDoorOpened && StageWound) SetState(Gim002State.CurtainClosed);
        }

        // CURTAIN_CLOSED → STAGING: 손잡이를 2초 돌렸다. (2초를 세는 건 손잡이 쪽, 여기는 "다 돌렸다"는 결과만 받는다)
        public bool NotifyCrankCompleted()
        {
            if (State != Gim002State.CurtainClosed || IsBusy) return false;
            CurtainOpen = true;
            SetState(Gim002State.Staging);
            StartPhase(Phase.CurtainOpening, timing.curtainSeconds);
            Commit();
            return true;
        }

        // STAGING: 슬롯 내용 변경 요청 (꽂기 / 바꾸기 / 빼기 모두 "바뀐 뒤의 두 슬롯 값"으로 보낸다).
        // 두 슬롯이 모두 차면 judgeDelay초 뒤 판정한다. 그 전에 하나만 찬 상태에서는 아무 일도 없다.
        public bool RequestSlots(DollId? doll, BackgroundId? background)
        {
            // 커튼이 열린 Staging에서만, 연출 중이 아닐 때만. Accompany 이후에는 슬롯이 잠겨 거부된다.
            if (State != Gim002State.Staging || IsBusy) return false;
            SlotDoll = doll;
            SlotBg = background;
            if (SlotDoll.HasValue && SlotBg.HasValue) StartPhase(Phase.JudgeDelay, timing.judgeDelaySeconds);
            Commit();
            return true;
        }

        // 피아노 건반 입력. symbol이 null이면 기호 없는 건반(소리만 남).
        // 반주판이 열린 ACCOMPANY이고 연출 중이 아닐 때만 기록한다. 소리는 이 결과와 상관없이 건반 쪽에서 항상 낸다.
        public PianoInputBuffer.Result PressKey(SymbolId? symbol)
        {
            if (State != Gim002State.Accompany || IsBusy || !symbol.HasValue) return PianoInputBuffer.Result.Ignored;

            var result = piano.Push(symbol, CurrentRule.Symbols);
            OnKeyRecorded?.Invoke(symbol.Value);

            if (result == PianoInputBuffer.Result.Correct)
            {
                // 반주 정답 → 공연
                OnPianoJudged?.Invoke(true);
                SetState(Gim002State.Perform);
                StartPhase(Phase.Performing, timing.performSeconds);
            }
            else if (result == PianoInputBuffer.Result.Wrong)
            {
                // 반주 오답 → 반주판은 열린 채, 입력만 비움(버퍼가 이미 비움) + 잠깐 기록 중지
                OnPianoJudged?.Invoke(false);
                StartPhase(Phase.PianoWrong, timing.pianoWrongSeconds);
            }
            Commit();
            return result;
        }

        // 재접속: 진행 중이던 장면만 처음(CURTAIN_CLOSED)으로 되돌린다. 성공한 장면 수와 열쇠 사용 여부는 그대로 둔다.
        public void ResetCurrentScene()
        {
            if (State < Gim002State.CurtainClosed || State > Gim002State.Perform) return;
            phase = Phase.None;
            timer = 0f;
            piano.Clear();
            CurtainOpen = false;
            ScoreboardOpen = false;
            SlotDoll = null;
            SlotBg = null;
            SetState(Gim002State.CurtainClosed);
            Commit();
        }

        // =====================================================================
        // 시간 진행
        // =====================================================================

        // 매 프레임(또는 테스트에서 원하는 만큼) 호출. 큰 값을 한 번에 넣어도 여러 단계를 차례로 통과한다.
        public void Tick(float deltaTime)
        {
            if (phase == Phase.None || deltaTime <= 0f) return;
            timer -= deltaTime;
            // timer가 0 이하가 될 때마다 다음 단계로. 남은 시간(-timer)은 다음 단계에 이어서 쓴다.
            while (phase != Phase.None && timer <= Epsilon)
            {
                float carry = Math.Max(0f, -timer);
                Advance();
                if (phase != Phase.None) timer -= carry;
            }
            Commit();
        }

        // 현재 단계가 끝났을 때 할 일 (상태 전이표의 "처리" 칸)
        void Advance()
        {
            switch (phase)
            {
                case Phase.CurtainOpening:
                    EndPhase();
                    break;

                case Phase.JudgeDelay:
                    Judge();
                    break;

                case Phase.StageWrongFlash:
                    // 빨간 화면이 끝나면 인형·카드를 제자리로 (슬롯을 비우면 화면 쪽이 ReturnHome을 실행한다)
                    SlotDoll = null;
                    SlotBg = null;
                    StartPhase(Phase.StageWrongReturn, timing.returnHomeSeconds);
                    break;

                case Phase.StageWrongReturn:
                    CurtainOpen = false;
                    StartPhase(Phase.StageWrongClosing, timing.curtainSeconds);
                    break;

                case Phase.StageWrongClosing:
                    EndPhase();
                    SetState(Gim002State.CurtainClosed);
                    break;

                case Phase.PianoWrong:
                    EndPhase();
                    break;

                case Phase.Performing:
                    // 공연 끝 → 진행 표시등 +1
                    ProgressCount++;
                    OnSceneCleared?.Invoke(SceneIndex);
                    if (SceneIndex >= rules.Length)
                    {
                        // 마지막 장면 → 완료. 무대는 공연이 끝난 모습 그대로 둔다.
                        EndPhase();
                        SetState(Gim002State.Complete);
                        OnComplete?.Invoke();
                    }
                    else
                    {
                        // 다음 장면 준비: 커튼 닫힘, 원위치, 반주판 닫힘, 슬롯 잠금 해제
                        CurtainOpen = false;
                        ScoreboardOpen = false;
                        SlotDoll = null;
                        SlotBg = null;
                        StartPhase(Phase.Cleanup, timing.cleanupSeconds);
                    }
                    break;

                case Phase.Cleanup:
                    EndPhase();
                    SceneIndex++;
                    piano.Clear();
                    SetState(Gim002State.CurtainClosed);
                    break;

                default:
                    EndPhase();
                    break;
            }
        }

        // 무대 판정 (1단계, 2층 책임)
        void Judge()
        {
            if (!SlotDoll.HasValue || !SlotBg.HasValue) { EndPhase(); return; }

            if (CurrentRule.IsStageCorrect(SlotDoll.Value, SlotBg.Value))
            {
                // 정답 → 슬롯 잠금(= Accompany 상태), 반주판 열림
                EndPhase();
                piano.Clear();
                ScoreboardOpen = true;
                SetState(Gim002State.Accompany);
                OnStageJudged?.Invoke(true);
            }
            else
            {
                // 오답 → 빨간 화면 → 제자리 → 커튼 닫힘 → CURTAIN_CLOSED (장면 번호는 그대로)
                StartPhase(Phase.StageWrongFlash, timing.stageWrongFlashSeconds);
                OnStageJudged?.Invoke(false);
            }
        }

        // ---------- 도우미 ----------

        void StartPhase(Phase p, float seconds)
        {
            phase = p;
            timer = seconds;
            // 0초짜리 단계는 바로 다음 단계로 넘긴다 (수치를 0으로 바꿔도 멈추지 않게)
            while (phase != Phase.None && timer <= Epsilon) Advance();
        }

        void EndPhase()
        {
            phase = Phase.None;
            timer = 0f;
        }

        void SetState(Gim002State s)
        {
            if (State == s) return;
            State = s;
            OnStateChanged?.Invoke(s);
        }

        // 동기화 값이 바뀌었으면 OnChanged를 한 번 알린다.
        void Commit()
        {
            var now = Snapshot();
            if (now.SameAs(lastSnapshot)) return;
            lastSnapshot = now;
            OnChanged?.Invoke();
        }
    }
}
