using System.Collections.Generic;
using NUnit.Framework;

namespace Gim007.Tests
{
    // GIM-007 판정 규칙 테스트 (명세서 6장·7장·9장 핵심 테스트 표 기준). Test Runner > EditMode에서 실행.
    public class Gim007LogicTests
    {
        const ulong P1 = 0, P2 = 1, P3 = 2;

        static Gim007Logic NewLogic(int steps = 8)
        {
            return new Gim007Logic(new Gim007Rules
            {
                requiredSteps = steps, roundSeconds = 1.5f, restSeconds = 2f, readySeconds = 1f, moveSeconds = 0.5f, latencyGraceSeconds = 0.3f,
            }, seed: 1234);
        }

        // 둘 다 잡고 첫 라운드까지 진행
        static Gim007Logic Pushing(int steps = 8)
        {
            var l = NewLogic(steps);
            l.Grip(0, P1);
            l.Grip(1, P2);
            l.Tick(1f);
            Assert.AreEqual(Gim007State.Round, l.State);
            return l;
        }

        static void SucceedRound(Gim007Logic l)
        {
            int r = l.Snapshot().round;
            l.Report(P1, r, true);
            l.Report(P2, r, true);
            if (l.State == Gim007State.Moving) l.Tick(0.5f);
        }

        [Test]
        public void DefaultRules_AreValid_AndMatchSpecProposal()
        {
            var r = new Gim007Rules();
            Assert.IsNull(r.Validate());
            Assert.AreEqual(8, r.requiredSteps);
            Assert.AreEqual(1.5f, r.roundSeconds);
            Assert.AreEqual(2f, r.restSeconds);
            Assert.IsTrue(r.wrongKeyFails);
        }

        [Test]
        public void Validate_RejectsBadValues()
        {
            Assert.IsNotNull(new Gim007Rules { requiredSteps = 0 }.Validate());
            Assert.IsNotNull(new Gim007Rules { roundSeconds = 0f }.Validate());
            Assert.IsNotNull(new Gim007Rules { restSeconds = -1f }.Validate());
        }

        [Test]
        public void StartsIdle_NobodyGripping()
        {
            var s = NewLogic().Snapshot();
            Assert.AreEqual(Gim007State.Idle, s.state);
            Assert.AreEqual(0, s.GripCount);
            Assert.AreEqual(0, s.step);
            Assert.AreEqual(-1, s.HandleOf(P1));
        }

        // 9장: A 혼자 / B 혼자 가구 잡기 → 움직이지 않음
        [TestCase(0)]
        [TestCase(1)]
        public void SoloGrip_StaysIdle_NoRounds(int handle)
        {
            var l = NewLogic();
            Assert.AreEqual(Gim007GripResult.Gripped, l.Grip(handle, P1));
            l.Tick(10f);
            Assert.AreEqual(Gim007State.Idle, l.State);
            Assert.AreEqual(0, l.Snapshot().round);
            Assert.AreEqual(0, l.Step);
            Assert.IsFalse(l.Report(P1, 1, true));
        }

        // 명세서 2장: 손잡이는 먼저 잡은 사람이 아무 쪽이나
        [Test]
        public void EitherPlayerCanTakeEitherHandle()
        {
            var l = NewLogic();
            Assert.AreEqual(Gim007GripResult.Gripped, l.Grip(1, P2));
            Assert.AreEqual(Gim007GripResult.Gripped, l.Grip(0, P1));
            Assert.AreEqual(1, l.Snapshot().HandleOf(P2));
            Assert.AreEqual(0, l.Snapshot().HandleOf(P1));
        }

        // 7장: 같은 손잡이 동시 요청 → 먼저 도착한 쪽 점유
        [Test]
        public void SameHandle_FirstComesWins()
        {
            var l = NewLogic();
            l.Grip(0, P1);
            Assert.AreEqual(Gim007GripResult.HandleTaken, l.Grip(0, P2));
            Assert.AreEqual(P1, l.Snapshot().grip0);
            Assert.AreEqual(Gim007GripResult.Gripped, l.Grip(1, P2));
        }

        [Test]
        public void OnePlayerCannotHoldBothHandles()
        {
            var l = NewLogic();
            l.Grip(0, P1);
            Assert.AreEqual(Gim007GripResult.AlreadyHolding, l.Grip(1, P1));
            Assert.AreEqual(Gim007GripResult.Ignored, l.Grip(0, P1));
            Assert.AreEqual(Gim007State.Idle, l.State);
        }

        [Test]
        public void BothGrip_GoesReady_ThenRoundAfterReadySeconds()
        {
            var l = NewLogic();
            var events = new List<Gim007NetEvent>();
            l.OnEvent += (e, v) => events.Add(e);
            l.Grip(0, P1);
            l.Grip(1, P2);
            Assert.AreEqual(Gim007State.Ready, l.State);
            l.Tick(0.5f);
            Assert.AreEqual(Gim007State.Ready, l.State);
            l.Tick(0.6f);
            Assert.AreEqual(Gim007State.Round, l.State);
            Assert.AreEqual(1, l.Snapshot().round);
            CollectionAssert.AreEqual(new[] { Gim007NetEvent.Ready, Gim007NetEvent.RoundStart }, events);
        }

        // 9장: 둘이 잡고 둘 다 입력 성공 → 한 칸 전진
        [Test]
        public void BothSucceed_AdvancesOneStep_ThenNextRound()
        {
            var l = Pushing();
            int r = l.Snapshot().round;
            Assert.IsTrue(l.Report(P1, r, true));
            Assert.AreEqual(Gim007State.Round, l.State);   // 한 명만 성공 → 아직 대기
            Assert.AreEqual(0, l.Step);
            Assert.IsTrue(l.Report(P2, r, true));
            Assert.AreEqual(Gim007State.Moving, l.State);
            Assert.AreEqual(1, l.Step);
            l.Tick(0.5f);
            Assert.AreEqual(Gim007State.Round, l.State);
            Assert.AreEqual(r + 1, l.Snapshot().round);
        }

        // 9장: 한쪽 오입력 → 숨고르기 후 다음 라운드, 위치 유지
        [Test]
        public void OneFails_RestsThenNextRound_StepKept()
        {
            var l = Pushing();
            SucceedRound(l);
            int r = l.Snapshot().round;
            l.Report(P1, r, true);
            l.Report(P2, r, false);
            Assert.AreEqual(Gim007State.Resting, l.State);
            Assert.AreEqual(1, l.Step);
            Assert.AreEqual(1, l.Snapshot().failCount);
            l.Tick(1.9f);
            Assert.AreEqual(Gim007State.Resting, l.State);
            l.Tick(0.2f);
            Assert.AreEqual(Gim007State.Round, l.State);
            Assert.AreEqual(1, l.Step);
        }

        [Test]
        public void FailEvent_ReportsWhichHandleFailed()
        {
            var l = Pushing();
            int failedBits = -1;
            l.OnEvent += (e, v) => { if (e == Gim007NetEvent.Fail) failedBits = v; };
            l.Report(P2, l.Snapshot().round, false);   // P2 = 손잡이 1번
            Assert.AreEqual(2, failedBits);
        }

        // 9장: 한쪽 시간 초과 → 판정 담당 기준 제한 시간 + 여유 뒤 실패
        [Test]
        public void Timeout_FailsAfterRoundPlusGrace()
        {
            var l = Pushing();
            l.Report(P1, l.Snapshot().round, true);
            l.Tick(1.5f);
            Assert.AreEqual(Gim007State.Round, l.State);   // 여유 시간 동안은 늦은 보고를 기다린다
            l.Tick(0.31f);
            Assert.AreEqual(Gim007State.Resting, l.State);
            Assert.AreEqual(Gim007RoundResult.Fail, l.Snapshot().result1);
            Assert.AreEqual(0, l.Step);
        }

        [Test]
        public void LateReport_WithinGrace_StillCounts()
        {
            var l = Pushing();
            int r = l.Snapshot().round;
            l.Report(P1, r, true);
            l.Tick(1.6f);
            Assert.IsTrue(l.Report(P2, r, true));
            Assert.AreEqual(1, l.Step);
        }

        [Test]
        public void StaleOrDuplicateReports_AreIgnored()
        {
            var l = Pushing();
            int r = l.Snapshot().round;
            Assert.IsFalse(l.Report(P1, r - 1, true));     // 지난 라운드
            Assert.IsFalse(l.Report(P3, r, true));         // 안 잡은 사람
            Assert.IsTrue(l.Report(P1, r, true));
            Assert.IsFalse(l.Report(P1, r, false));        // 이미 낸 결과는 못 바꾼다
            Assert.AreEqual(Gim007RoundResult.Success, l.Snapshot().result0);
        }

        // 9장: 푸시 도중 한 명이 손을 놓음 → 라운드 취소, 위치 유지, 재잡기 시 이어서
        [Test]
        public void ReleaseDuringPush_CancelsRound_KeepsStep_ResumesOnRegrip()
        {
            var l = Pushing();
            SucceedRound(l);
            SucceedRound(l);
            var events = new List<Gim007NetEvent>();
            l.OnEvent += (e, v) => events.Add(e);
            Assert.IsTrue(l.Release(P2));
            Assert.AreEqual(Gim007State.Idle, l.State);
            Assert.AreEqual(2, l.Step);
            Assert.AreEqual(P1, l.Snapshot().grip0);
            Assert.AreEqual(Gim007SyncState.NoPlayer, l.Snapshot().grip1);
            CollectionAssert.Contains(events, Gim007NetEvent.Cancelled);
            l.Tick(5f);
            Assert.AreEqual(Gim007State.Idle, l.State);

            l.Grip(1, P2);
            l.Tick(1f);
            Assert.AreEqual(Gim007State.Round, l.State);
            SucceedRound(l);
            Assert.AreEqual(3, l.Step);
        }

        // 7장: 접속 끊김 = 그 사람 손잡이 해제와 같다 (판정 담당이 Release 호출)
        [Test]
        public void Disconnect_ReleasesOnlyThatPlayer()
        {
            var l = Pushing();
            l.Release(P2);
            Assert.AreEqual(0, l.Snapshot().HandleOf(P1));
            Assert.AreEqual(-1, l.Snapshot().HandleOf(P2));
            Assert.IsFalse(l.Release(P2));
        }

        [Test]
        public void ReleaseWhileAlone_JustFreesHandle()
        {
            var l = NewLogic();
            var events = new List<Gim007NetEvent>();
            l.OnEvent += (e, v) => events.Add(e);
            l.Grip(0, P1);
            Assert.IsTrue(l.Release(P1));
            Assert.AreEqual(0, l.Snapshot().GripCount);
            CollectionAssert.IsEmpty(events);   // 진행 중이 아니었으므로 취소 사건 없음
        }

        // 9장: 발판 도달 → 완료, 두 플레이어 자동 손 떼기, 이후 잡기 불가
        [Test]
        public void RequiredSteps_Completes_AutoReleases_AndLocks()
        {
            var l = Pushing(3);
            var events = new List<Gim007NetEvent>();
            l.OnEvent += (e, v) => events.Add(e);
            SucceedRound(l);
            SucceedRound(l);
            SucceedRound(l);
            Assert.AreEqual(Gim007State.Complete, l.State);
            Assert.AreEqual(3, l.Step);
            Assert.AreEqual(0, l.Snapshot().GripCount);
            Assert.AreEqual(Gim007NetEvent.Complete, events[events.Count - 1]);
            Assert.AreEqual(Gim007GripResult.Locked, l.Grip(0, P1));
            l.Tick(10f);
            Assert.AreEqual(Gim007State.Complete, l.State);
        }

        [Test]
        public void FullRun_WithFailures_StillNeedsExactlyRequiredSuccesses()
        {
            var l = Pushing(8);
            int successes = 0, guard = 0;
            while (!l.IsComplete && guard++ < 100)
            {
                int r = l.Snapshot().round;
                bool fail = guard % 3 == 0;
                l.Report(P1, r, true);
                l.Report(P2, r, !fail);
                if (!fail) successes++;
                l.Tick(2.1f);   // 이동/숨고르기 대기
            }
            Assert.IsTrue(l.IsComplete);
            Assert.AreEqual(8, successes);
            Assert.Greater(l.Snapshot().failCount, 0);
        }

        // 명세서 3장: 두 사람의 키는 독립적으로 랜덤 — 여러 라운드에서 서로 다른 키가 나오는 경우가 있어야 한다
        [Test]
        public void Keys_AreIndependentPerHandle()
        {
            var l = Pushing(200);
            bool differ = false, same = false;
            var seen0 = new HashSet<Gim007Key>();
            for (int i = 0; i < 160; i++)
            {
                var s = l.Snapshot();
                seen0.Add(s.key0);
                if (s.key0 != s.key1) differ = true; else same = true;
                SucceedRound(l);
            }
            Assert.IsTrue(differ);
            Assert.IsTrue(same);
            Assert.AreEqual(Gim007Rules.KeyCount, seen0.Count);   // W/A/S/D + 방향키 8개가 모두 나온다
        }

        [Test]
        public void JudgeKey_WrongKeyFailsOnlyWhenEnabled()
        {
            var on = new Gim007Rules { wrongKeyFails = true };
            var off = new Gim007Rules { wrongKeyFails = false };
            Assert.AreEqual(true, Gim007Logic.JudgeKey(on, Gim007Key.S, Gim007Key.S));
            Assert.AreEqual(false, Gim007Logic.JudgeKey(on, Gim007Key.S, Gim007Key.A));
            Assert.AreEqual(true, Gim007Logic.JudgeKey(off, Gim007Key.D, Gim007Key.D));
            Assert.IsNull(Gim007Logic.JudgeKey(off, Gim007Key.D, Gim007Key.W));
        }

        [Test]
        public void Restore_SetsStepAndCompletion()
        {
            var l = NewLogic(8);
            l.Restore(5, false);
            Assert.AreEqual(5, l.Step);
            Assert.AreEqual(Gim007State.Idle, l.State);
            l.Restore(8, false);
            Assert.AreEqual(Gim007State.Complete, l.State);
        }

        [Test]
        public void SyncState_EqualityAndHelpers()
        {
            var a = Gim007SyncState.Empty;
            var b = Gim007SyncState.Empty;
            Assert.AreEqual(a, b);
            b.SetGrip(1, P2);
            Assert.AreNotEqual(a, b);
            Assert.AreEqual(1, b.HandleOf(P2));
            Assert.IsFalse(b.BothGripped);
            Assert.AreEqual(-1, b.HandleOf(Gim007SyncState.NoPlayer));
        }
    }
}
