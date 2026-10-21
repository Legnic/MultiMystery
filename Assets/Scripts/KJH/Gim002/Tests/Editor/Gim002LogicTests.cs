using NUnit.Framework;

namespace Gim002.Tests
{
    // Gim002Logic EditMode 테스트 (명세서 8-1).
    // 실행: Unity 메뉴 Window > General > Test Runner > EditMode 탭 > Run All
    // 씬이나 Play 모드 없이 판정 로직만 검사한다. 시간은 Tick(초)로 직접 흘려보낸다.
    public class Gim002LogicTests
    {
        Gim002Logic logic;
        Gim002Timing t;

        [SetUp]
        public void SetUp()
        {
            // 기획 가안 수치/장면 그대로 사용
            t = new Gim002Timing();
            logic = new Gim002Logic(Gim002SceneRule.Defaults(), t);
        }

        // ---------- 도우미 ----------

        // GIM-001 해결 + 문 열림 + 태엽 사용 → 장면 1 커튼 닫힘
        void StartGame()
        {
            logic.NotifyGim001Solved();
            logic.NotifyMusicDoorOpened();
            logic.NotifyStageWound();
        }

        // 손잡이를 돌려 커튼을 다 연 상태(Staging, 입력 가능)로 만든다
        void OpenCurtain()
        {
            Assert.IsTrue(logic.NotifyCrankCompleted());
            logic.Tick(t.curtainSeconds);
            Assert.IsFalse(logic.IsBusy);
        }

        // 현재 장면의 정답을 꽂고 판정까지 기다려 Accompany로 만든다
        void StageCorrect()
        {
            var r = logic.CurrentRule;
            logic.RequestSlots(r.Doll, r.Background);
            logic.Tick(t.judgeDelaySeconds);
            Assert.AreEqual(Gim002State.Accompany, logic.State);
        }

        void PlayCorrect()
        {
            foreach (var s in logic.CurrentRule.Symbols) logic.PressKey(s);
            Assert.AreEqual(Gim002State.Perform, logic.State);
        }

        void ClearCurrentScene()
        {
            OpenCurtain();
            StageCorrect();
            PlayCorrect();
            logic.Tick(t.performSeconds + t.cleanupSeconds);
        }

        // ---------- 시작 조건 ----------

        [Test]
        public void 문만_열림이면_Preparing_유지()
        {
            logic.NotifyGim001Solved();
            logic.NotifyMusicDoorOpened();
            Assert.AreEqual(Gim002State.Preparing, logic.State);
            Assert.IsTrue(logic.MusicDoorOpened);
        }

        [Test]
        public void 태엽만_사용이면_Preparing_유지()
        {
            logic.NotifyGim001Solved();
            logic.NotifyStageWound();
            Assert.AreEqual(Gim002State.Preparing, logic.State);
            Assert.IsTrue(logic.StageWound);
        }

        [Test]
        public void 둘다_준비되면_장면1_CurtainClosed()
        {
            StartGame();
            Assert.AreEqual(Gim002State.CurtainClosed, logic.State);
            Assert.AreEqual(1, logic.SceneIndex);
        }

        [Test]
        public void 준비_순서를_바꿔도_결과가_같다()
        {
            logic.NotifyGim001Solved();
            logic.NotifyStageWound();
            logic.NotifyMusicDoorOpened();
            Assert.AreEqual(Gim002State.CurtainClosed, logic.State);
            Assert.AreEqual(1, logic.SceneIndex);
        }

        [Test]
        public void GIM001_해결_전에는_문과_태엽을_무시()
        {
            Assert.IsFalse(logic.NotifyMusicDoorOpened());
            Assert.IsFalse(logic.NotifyStageWound());
            Assert.AreEqual(Gim002State.Locked, logic.State);
        }

        // ---------- 손잡이 ----------

        [Test]
        public void 손잡이는_Preparing에서_무시()
        {
            logic.NotifyGim001Solved();
            logic.NotifyMusicDoorOpened();
            Assert.IsFalse(logic.NotifyCrankCompleted());
            Assert.AreEqual(Gim002State.Preparing, logic.State);
            Assert.IsFalse(logic.CurtainOpen);
        }

        [Test]
        public void 손잡이는_CurtainClosed에서_동작()
        {
            StartGame();
            Assert.IsTrue(logic.NotifyCrankCompleted());
            Assert.AreEqual(Gim002State.Staging, logic.State);
            Assert.IsTrue(logic.CurtainOpen);
            Assert.IsTrue(logic.IsBusy, "커튼이 열리는 1초 동안은 바쁨");
            logic.Tick(t.curtainSeconds);
            Assert.IsFalse(logic.IsBusy);
        }

        [Test]
        public void 커튼이_열리는_중에는_꽂기_거부()
        {
            StartGame();
            logic.NotifyCrankCompleted();
            Assert.IsFalse(logic.RequestSlots(DollId.OwlScope, null));
        }

        [Test]
        public void 커튼이_닫힌_상태에서는_꽂기_거부()
        {
            StartGame();
            Assert.IsFalse(logic.RequestSlots(DollId.OwlScope, null));
            Assert.IsNull(logic.SlotDoll);
        }

        // ---------- 무대 판정 ----------

        [Test]
        public void 슬롯_하나만_차면_판정하지_않음()
        {
            StartGame();
            OpenCurtain();
            Assert.IsTrue(logic.RequestSlots(DollId.OwlScope, null));
            logic.Tick(10f);
            Assert.AreEqual(Gim002State.Staging, logic.State);
            Assert.IsFalse(logic.IsBusy);

            Assert.IsTrue(logic.RequestSlots(null, BackgroundId.NightForest));
            logic.Tick(10f);
            Assert.AreEqual(Gim002State.Staging, logic.State);
        }

        [Test]
        public void 정답_조합은_03초_뒤_Accompany_반주판_열림()
        {
            StartGame();
            OpenCurtain();
            bool? judged = null;
            logic.OnStageJudged += ok => judged = ok;

            logic.RequestSlots(DollId.OwlScope, BackgroundId.NightForest);
            logic.Tick(t.judgeDelaySeconds * 0.5f);
            Assert.AreEqual(Gim002State.Staging, logic.State, "0.3초 전에는 아직 판정 안 함");
            Assert.IsNull(judged);

            logic.Tick(t.judgeDelaySeconds * 0.5f);
            Assert.AreEqual(Gim002State.Accompany, logic.State);
            Assert.IsTrue(logic.ScoreboardOpen);
            Assert.AreEqual(true, judged);
        }

        [Test]
        public void 오답_조합은_CurtainClosed로_복귀_장면번호_유지()
        {
            StartGame();
            OpenCurtain();
            bool? judged = null;
            logic.OnStageJudged += ok => judged = ok;

            logic.RequestSlots(DollId.OwlScope, BackgroundId.Sea);
            logic.Tick(t.judgeDelaySeconds);
            Assert.AreEqual(false, judged);
            Assert.IsTrue(logic.IsBusy);

            logic.Tick(t.stageWrongFlashSeconds);
            Assert.IsNull(logic.SlotDoll, "빨간 화면 뒤 인형·카드 원위치");
            Assert.IsNull(logic.SlotBg);

            logic.Tick(t.returnHomeSeconds + t.curtainSeconds);
            Assert.AreEqual(Gim002State.CurtainClosed, logic.State);
            Assert.IsFalse(logic.CurtainOpen);
            Assert.IsFalse(logic.ScoreboardOpen);
            Assert.AreEqual(1, logic.SceneIndex);
            Assert.AreEqual(0, logic.ProgressCount);
        }

        [Test]
        public void 장면1에서_장면2_정답을_꽂으면_오답()
        {
            StartGame();
            OpenCurtain();
            bool? judged = null;
            logic.OnStageJudged += ok => judged = ok;
            logic.RequestSlots(DollId.FrogCan, BackgroundId.Greenhouse);
            logic.Tick(t.judgeDelaySeconds);
            Assert.AreEqual(false, judged);
        }

        [Test]
        public void 함정은_어느_장면에서도_오답()
        {
            var rules = Gim002SceneRule.Defaults();
            foreach (var r in rules)
            {
                Assert.IsFalse(r.IsStageCorrect(DollId.OwlBook, r.Background), "책 부엉이");
                Assert.IsFalse(r.IsStageCorrect(r.Doll, BackgroundId.Sea), "바다");
                Assert.IsFalse(r.IsStageCorrect(r.Doll, BackgroundId.Castle), "성");
            }

            // 실제 흐름에서도 장면 1에 함정 인형을 꽂으면 오답
            StartGame();
            OpenCurtain();
            bool? judged = null;
            logic.OnStageJudged += ok => judged = ok;
            logic.RequestSlots(DollId.OwlBook, BackgroundId.NightForest);
            logic.Tick(t.judgeDelaySeconds);
            Assert.AreEqual(false, judged);
        }

        [Test]
        public void 슬롯을_바꾸면_마지막_값으로_판정()
        {
            StartGame();
            OpenCurtain();
            logic.RequestSlots(DollId.OwlBook, null);              // 함정 인형 꽂음
            logic.RequestSlots(DollId.OwlScope, null);             // 바꾸기
            logic.RequestSlots(DollId.OwlScope, BackgroundId.NightForest);
            logic.Tick(t.judgeDelaySeconds);
            Assert.AreEqual(Gim002State.Accompany, logic.State);
        }

        // ---------- 반주 판정 ----------

        [Test]
        public void Accompany에서_기호_3개_정답이면_Perform()
        {
            StartGame();
            OpenCurtain();
            StageCorrect();
            bool? judged = null;
            logic.OnPianoJudged += ok => judged = ok;

            logic.PressKey(SymbolId.Sun);
            logic.PressKey(SymbolId.Moon);
            Assert.AreEqual(Gim002State.Accompany, logic.State, "개수가 차기 전에는 판정 안 함");
            logic.PressKey(SymbolId.Star);
            Assert.AreEqual(Gim002State.Perform, logic.State);
            Assert.AreEqual(true, judged);
        }

        [Test]
        public void 반주_오답이면_Accompany_유지_버퍼_비움_1초간_입력_무시()
        {
            StartGame();
            OpenCurtain();
            StageCorrect();
            bool? judged = null;
            logic.OnPianoJudged += ok => judged = ok;

            // 첫 음부터 틀려도 미리 실패시키지 않고 3개가 찰 때 판정
            Assert.AreEqual(PianoInputBuffer.Result.Recorded, logic.PressKey(SymbolId.Moon));
            Assert.AreEqual(PianoInputBuffer.Result.Recorded, logic.PressKey(SymbolId.Sun));
            Assert.IsNull(judged);
            Assert.AreEqual(PianoInputBuffer.Result.Wrong, logic.PressKey(SymbolId.Star));

            Assert.AreEqual(false, judged);
            Assert.AreEqual(Gim002State.Accompany, logic.State);
            Assert.IsTrue(logic.ScoreboardOpen, "반주판은 열린 채 유지");
            Assert.AreEqual(0, logic.Piano.Count, "입력은 비움");

            // 오답 연출 1초 동안은 기록 안 함
            Assert.AreEqual(PianoInputBuffer.Result.Ignored, logic.PressKey(SymbolId.Sun));
            Assert.AreEqual(0, logic.Piano.Count);
            logic.Tick(t.pianoWrongSeconds);
            Assert.AreEqual(PianoInputBuffer.Result.Recorded, logic.PressKey(SymbolId.Sun));
            Assert.AreEqual(1, logic.Piano.Count);
        }

        [Test]
        public void 기호_없는_건반은_버퍼에_들어가지_않음()
        {
            StartGame();
            OpenCurtain();
            StageCorrect();
            Assert.AreEqual(PianoInputBuffer.Result.Ignored, logic.PressKey(null));
            Assert.AreEqual(0, logic.Piano.Count);
        }

        [Test]
        public void Staging과_Perform_중_피아노_입력은_기록되지_않음()
        {
            StartGame();
            OpenCurtain();
            Assert.AreEqual(PianoInputBuffer.Result.Ignored, logic.PressKey(SymbolId.Sun), "Staging");
            Assert.AreEqual(0, logic.Piano.Count);

            StageCorrect();
            PlayCorrect();
            Assert.AreEqual(PianoInputBuffer.Result.Ignored, logic.PressKey(SymbolId.Sun), "Perform");
            Assert.AreEqual(0, logic.Piano.Count);
        }

        [Test]
        public void 커튼닫힘_상태의_피아노_입력도_기록되지_않음()
        {
            StartGame();
            Assert.AreEqual(PianoInputBuffer.Result.Ignored, logic.PressKey(SymbolId.Sun));
        }

        [Test]
        public void Accompany에서_슬롯_변경_요청은_거부()
        {
            StartGame();
            OpenCurtain();
            StageCorrect();
            Assert.IsFalse(logic.RequestSlots(null, BackgroundId.NightForest), "인형 빼기");
            Assert.IsFalse(logic.RequestSlots(DollId.OwlBook, BackgroundId.NightForest), "인형 바꾸기");
            Assert.AreEqual(DollId.OwlScope, logic.SlotDoll);
        }

        // ---------- 장면 진행 ----------

        [Test]
        public void 공연3초_정리1초_후_다음_장면_진행도_증가()
        {
            StartGame();
            OpenCurtain();
            StageCorrect();
            int cleared = 0;
            logic.OnSceneCleared += n => cleared = n;
            PlayCorrect();

            logic.Tick(t.performSeconds * 0.9f);
            Assert.AreEqual(Gim002State.Perform, logic.State);
            Assert.AreEqual(0, logic.ProgressCount);

            logic.Tick(t.performSeconds * 0.1f);
            Assert.AreEqual(1, logic.ProgressCount, "공연이 끝나면 진행 표시등 +1");
            Assert.AreEqual(1, cleared);
            Assert.IsFalse(logic.CurtainOpen);
            Assert.IsFalse(logic.ScoreboardOpen);
            Assert.IsNull(logic.SlotDoll);
            Assert.IsNull(logic.SlotBg);

            logic.Tick(t.cleanupSeconds);
            Assert.AreEqual(Gim002State.CurtainClosed, logic.State);
            Assert.AreEqual(2, logic.SceneIndex);
            Assert.IsFalse(logic.IsBusy);
        }

        [Test]
        public void 장면3_성공하면_Complete_진행도3()
        {
            bool completed = false;
            logic.OnComplete += () => completed = true;
            StartGame();
            ClearCurrentScene();
            ClearCurrentScene();
            Assert.AreEqual(3, logic.SceneIndex);

            OpenCurtain();
            StageCorrect();
            Assert.AreEqual(4, logic.CurrentRule.Symbols.Count, "장면 3은 음이 4개");
            PlayCorrect();
            logic.Tick(t.performSeconds);

            Assert.AreEqual(Gim002State.Complete, logic.State);
            Assert.AreEqual(3, logic.ProgressCount);
            Assert.IsTrue(completed);
        }

        [Test]
        public void 성공한_장면의_인형도_다음_장면에_다시_쓸_수_있다()
        {
            StartGame();
            ClearCurrentScene();
            OpenCurtain();
            // 장면 2에서 장면 1의 인형을 다시 꽂을 수 있어야 한다 (사라지지 않음) — 다만 오답
            Assert.IsTrue(logic.RequestSlots(DollId.OwlScope, null));
        }

        // ---------- 재접속 ----------

        [Test]
        public void 재접속_Staging중이면_같은_장면_CurtainClosed()
        {
            StartGame();
            ClearCurrentScene();
            OpenCurtain();
            logic.RequestSlots(DollId.FrogCan, null);

            logic.ResetCurrentScene();
            Assert.AreEqual(Gim002State.CurtainClosed, logic.State);
            Assert.AreEqual(2, logic.SceneIndex);
            Assert.AreEqual(1, logic.ProgressCount);
            Assert.IsFalse(logic.CurtainOpen);
            Assert.IsNull(logic.SlotDoll);
            Assert.IsFalse(logic.IsBusy);
        }

        [Test]
        public void 재접속_Accompany중이면_같은_장면_CurtainClosed_입력_비움()
        {
            StartGame();
            OpenCurtain();
            StageCorrect();
            logic.PressKey(SymbolId.Sun);

            logic.ResetCurrentScene();
            Assert.AreEqual(Gim002State.CurtainClosed, logic.State);
            Assert.AreEqual(1, logic.SceneIndex);
            Assert.AreEqual(0, logic.ProgressCount);
            Assert.IsFalse(logic.ScoreboardOpen);
            Assert.AreEqual(0, logic.Piano.Count);
        }

        [Test]
        public void 재접속은_Preparing과_Complete에_영향_없음()
        {
            logic.NotifyGim001Solved();
            logic.NotifyStageWound();
            logic.ResetCurrentScene();
            Assert.AreEqual(Gim002State.Preparing, logic.State);
            Assert.IsTrue(logic.StageWound, "열쇠 사용 여부 유지");
        }

        // ---------- 동기화 ----------

        [Test]
        public void 값이_바뀔_때만_OnChanged()
        {
            int changes = 0;
            logic.OnChanged += () => changes++;
            logic.NotifyGim001Solved();
            Assert.AreEqual(1, changes);
            logic.NotifyGim001Solved();     // 무시됨
            logic.Tick(1f);                 // 아무 일 없음
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void 큰_시간을_한번에_넣어도_단계를_차례로_통과()
        {
            StartGame();
            OpenCurtain();
            logic.RequestSlots(DollId.OwlBook, BackgroundId.Castle);
            logic.Tick(100f);
            Assert.AreEqual(Gim002State.CurtainClosed, logic.State);
            Assert.IsFalse(logic.IsBusy);
        }
    }
}
