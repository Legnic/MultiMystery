using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Gim005.Tests
{
    // GIM-005 판정 로직 + 미로 구조 테스트 (Test Runner > EditMode)
    public class Gim005LogicTests
    {
        static Gim005Logic NewLogic(Gim005Rules rules = null, int seed = 1)
        {
            return new Gim005Logic(rules ?? new Gim005Rules(), seed);
        }

        // 이번 시도에 배정되지 않은 문양 하나
        static int UnassignedSymbol(Gim005Logic l)
        {
            for (int s = 0; s < 13; s++) if (l.GateOfSymbol(s) < 0) return s;
            return -1;
        }

        // ---------------- 상태 흐름 ----------------

        [Test]
        public void StartsReady_TimerDoesNotRun()
        {
            var l = NewLogic();
            Assert.AreEqual(Gim005State.Ready, l.State);
            l.Tick(50f);
            Assert.AreEqual(Gim005State.Ready, l.State);
            Assert.AreEqual(180f, l.Remaining, 1e-4);
        }

        [Test]
        public void EnterMaze_StartsTimer_OnlyFromReady()
        {
            var l = NewLogic();
            Assert.IsTrue(l.EnterMaze());
            Assert.AreEqual(Gim005State.Running, l.State);
            Assert.IsFalse(l.EnterMaze());
            l.Tick(10f);
            Assert.AreEqual(170f, l.Remaining, 1e-4);
        }

        [Test]
        public void ButtonsIgnoredBeforeStart()
        {
            var l = NewLogic();
            Assert.AreEqual(Gim005PressResult.Ignored, l.PressSymbol(l.SymbolOfGate(0)));
            Assert.IsFalse(l.IsGateOpen(0));
        }

        // ---------------- 배정 ----------------

        [Test]
        public void Assignment_EightDistinctSymbolsOutOf13()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var l = NewLogic(seed: seed);
                var syms = Enumerable.Range(0, 8).Select(l.SymbolOfGate).ToList();
                Assert.AreEqual(8, syms.Distinct().Count());
                Assert.IsTrue(syms.All(s => s >= 0 && s < 13));
            }
        }

        [Test]
        public void NewAttempt_ReassignsEveryGateToDifferentSymbol()
        {
            var l = NewLogic(seed: 7);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var before = Enumerable.Range(0, 8).Select(l.SymbolOfGate).ToArray();
                l.EnterMaze();
                l.Tick(181f);           // 시간 초과
                l.Tick(2.5f);           // 흐려짐 연출 끝 → Ready
                Assert.AreEqual(Gim005State.Ready, l.State);
                var after = Enumerable.Range(0, 8).Select(l.SymbolOfGate).ToArray();
                for (int g = 0; g < 8; g++) Assert.AreNotEqual(before[g], after[g], "덤불 G" + (g + 1) + "에 같은 문양이 다시 배정됨");
                Assert.AreEqual(8, after.Distinct().Count());
            }
        }

        // ---------------- 버튼 판정 ----------------

        [Test]
        public void CorrectSymbol_OpensItsGate_AndLights()
        {
            var l = NewLogic();
            l.EnterMaze();
            int gate = -1;
            l.OnGateOpened += g => gate = g;
            Assert.AreEqual(Gim005PressResult.Opened, l.PressSymbol(l.SymbolOfGate(2)));
            Assert.AreEqual(2, gate);
            Assert.IsTrue(l.IsGateOpen(2));
            Assert.IsTrue(l.Snapshot().IsSymbolOpen(l.SymbolOfGate(2)));
            Assert.IsFalse(l.IsGateOpen(0));
        }

        [Test]
        public void AlreadyOpenSymbol_DoesNothing()
        {
            var l = NewLogic();
            l.EnterMaze();
            l.PressSymbol(l.SymbolOfGate(0));
            float before = l.Remaining;
            Assert.AreEqual(Gim005PressResult.AlreadyOpen, l.PressSymbol(l.SymbolOfGate(0)));
            Assert.AreEqual(before, l.Remaining);
            Assert.AreEqual(0f, l.LockRemaining);
        }

        [Test]
        public void WrongSymbol_PenaltyAndLock_KeepsGatesAndAssignment()
        {
            var l = NewLogic();
            l.EnterMaze();
            l.PressSymbol(l.SymbolOfGate(0));
            var assign = Enumerable.Range(0, 8).Select(l.SymbolOfGate).ToArray();
            bool warned = false;
            l.OnWrong += s => warned = true;

            Assert.AreEqual(Gim005PressResult.Wrong, l.PressSymbol(UnassignedSymbol(l)));
            Assert.IsTrue(warned);
            Assert.AreEqual(165f, l.Remaining, 1e-4);
            Assert.AreEqual(3f, l.LockRemaining, 1e-4);
            Assert.IsTrue(l.IsGateOpen(0), "기본 정책(시간 차감)에서는 열린 덤불이 유지된다");
            CollectionAssert.AreEqual(assign, Enumerable.Range(0, 8).Select(l.SymbolOfGate).ToArray());
        }

        [Test]
        public void PanelLocked_ForThreeSeconds_ThenAcceptsAgain()
        {
            var l = NewLogic();
            l.EnterMaze();
            l.PressSymbol(UnassignedSymbol(l));
            Assert.AreEqual(Gim005PressResult.Locked, l.PressSymbol(l.SymbolOfGate(1)));
            l.Tick(2.9f);
            Assert.AreEqual(Gim005PressResult.Locked, l.PressSymbol(l.SymbolOfGate(1)));
            l.Tick(0.2f);
            Assert.AreEqual(Gim005PressResult.Opened, l.PressSymbol(l.SymbolOfGate(1)));
        }

        [Test]
        public void RegrowPolicy_ClosesOpenedGates_NoTimePenalty()
        {
            var l = NewLogic(new Gim005Rules { wrongPolicy = Gim005WrongPolicy.RegrowOpened });
            l.EnterMaze();
            l.PressSymbol(l.SymbolOfGate(0));
            l.PressSymbol(l.SymbolOfGate(2));
            l.PressSymbol(UnassignedSymbol(l));
            Assert.IsFalse(l.IsGateOpen(0));
            Assert.IsFalse(l.IsGateOpen(2));
            Assert.AreEqual(180f, l.Remaining, 1e-4);
        }

        [Test]
        public void BothPolicy_ClosesAndPenalizes()
        {
            var l = NewLogic(new Gim005Rules { wrongPolicy = Gim005WrongPolicy.Both });
            l.EnterMaze();
            l.PressSymbol(l.SymbolOfGate(0));
            l.PressSymbol(UnassignedSymbol(l));
            Assert.IsFalse(l.IsGateOpen(0));
            Assert.AreEqual(165f, l.Remaining, 1e-4);
        }

        [Test]
        public void WrongPenalty_CanCauseTimeOut()
        {
            var l = NewLogic();
            l.EnterMaze();
            l.Tick(170f);
            l.PressSymbol(UnassignedSymbol(l));
            Assert.AreEqual(Gim005State.TimeOut, l.State);
        }

        [Test]
        public void BruteForce_AllThirteenButtons_CannotOpenEverything()
        {
            // B 혼자 무차별 입력: 3초 잠금 + 15초 차감 때문에 13개를 다 눌러 보는 동안 시간이 바닥난다
            var l = NewLogic();
            l.EnterMaze();
            for (int s = 0; s < 13 && l.State == Gim005State.Running; s++)
            {
                while (l.LockRemaining > 0f) l.Tick(0.1f);
                l.PressSymbol(s);
            }
            // 5번의 오답 = 75초 + 잠금 15초. 열 수는 있어도 A 없이는 중앙 버튼을 누를 수 없다
            Assert.AreEqual(Gim005State.Running, l.State);
            Assert.Less(l.Remaining, 180f - 5 * 15f + 0.01f);
        }

        // ---------------- 시간 ----------------

        [Test]
        public void Bell_RingsOnceAt30Seconds()
        {
            var l = NewLogic();
            int bells = 0;
            l.OnBell += () => bells++;
            l.EnterMaze();
            l.Tick(149f);
            Assert.AreEqual(0, bells);
            l.Tick(1.5f);
            Assert.AreEqual(1, bells);
            l.Tick(10f);
            Assert.AreEqual(1, bells);
        }

        [Test]
        public void TimeOut_ThenFx_ThenReadyWithClosedGatesAndFullTime()
        {
            var l = NewLogic();
            int timeouts = 0;
            l.OnTimeOut += () => timeouts++;
            l.EnterMaze();
            l.PressSymbol(l.SymbolOfGate(0));
            l.Tick(180.5f);
            Assert.AreEqual(Gim005State.TimeOut, l.State);
            Assert.AreEqual(1, timeouts);
            Assert.AreEqual(Gim005PressResult.Ignored, l.PressSymbol(l.SymbolOfGate(1)), "시간 초과 처리 중에는 버튼 무시");
            Assert.IsFalse(l.PressCenter());

            l.Tick(1.9f);
            Assert.AreEqual(Gim005State.TimeOut, l.State);
            l.Tick(0.2f);
            Assert.AreEqual(Gim005State.Ready, l.State);
            Assert.AreEqual(2, l.Attempt);
            Assert.AreEqual(180f, l.Remaining, 1e-4);
            for (int g = 0; g < 8; g++) Assert.IsFalse(l.IsGateOpen(g));
            Assert.IsTrue(l.EnterMaze(), "입구를 다시 지나면 재시작");
        }

        // ---------------- 완료 / 재접속 ----------------

        [Test]
        public void CenterButton_CompletesOnlyWhileRunning()
        {
            var l = NewLogic();
            Assert.IsFalse(l.PressCenter());
            l.EnterMaze();
            bool done = false;
            l.OnComplete += () => done = true;
            Assert.IsTrue(l.PressCenter());
            Assert.IsTrue(done);
            Assert.AreEqual(Gim005State.Complete, l.State);
            for (int g = 0; g < 8; g++) Assert.IsTrue(l.IsGateOpen(g), "완료 후 모든 덤불이 사라진다");
        }

        [Test]
        public void AfterComplete_EverythingIgnored_TimerStopped()
        {
            var l = NewLogic();
            l.EnterMaze();
            l.Tick(20f);
            l.PressCenter();
            float t = l.Remaining;
            l.Tick(500f);
            Assert.AreEqual(Gim005State.Complete, l.State);
            Assert.AreEqual(t, l.Remaining);
            Assert.AreEqual(Gim005PressResult.Ignored, l.PressSymbol(0));
            Assert.IsFalse(l.EnterMaze());
            l.ResetAttempt();
            Assert.AreEqual(Gim005State.Complete, l.State, "재접속해도 완료 여부는 유지");
        }

        [Test]
        public void Reconnect_ResetsRunningAttempt()
        {
            var l = NewLogic();
            var before = Enumerable.Range(0, 8).Select(l.SymbolOfGate).ToArray();
            l.EnterMaze();
            l.PressSymbol(l.SymbolOfGate(0));
            bool reset = false;
            l.OnAttemptReset += () => reset = true;
            l.ResetAttempt();
            Assert.IsTrue(reset);
            Assert.AreEqual(Gim005State.Ready, l.State);
            Assert.IsFalse(l.IsGateOpen(0));
            Assert.AreEqual(180f, l.Remaining, 1e-4);
            CollectionAssert.AreNotEqual(before, Enumerable.Range(0, 8).Select(l.SymbolOfGate).ToArray());
        }

        [Test]
        public void Reconnect_WhileReady_DoesNothing()
        {
            var l = NewLogic();
            bool reset = false;
            l.OnAttemptReset += () => reset = true;
            l.ResetAttempt();
            Assert.IsFalse(reset);
            Assert.AreEqual(1, l.Attempt);
        }

        [Test]
        public void Snapshot_RoundTripsAssignmentAndMask()
        {
            var l = NewLogic(seed: 3);
            l.EnterMaze();
            l.PressSymbol(l.SymbolOfGate(4));
            var s = l.Snapshot();
            for (int g = 0; g < 8; g++)
            {
                Assert.AreEqual(l.SymbolOfGate(g), s.SymbolOfGate(g));
                Assert.AreEqual(l.IsGateOpen(g), s.IsGateOpen(g));
                Assert.AreEqual(g, s.GateOfSymbol(l.SymbolOfGate(g)));
            }
        }

        [Test]
        public void InvalidRules_Throw()
        {
            Assert.Throws<System.ArgumentException>(() => new Gim005Logic(new Gim005Rules { gateCount = 9 }, 0));
            Assert.Throws<System.ArgumentException>(() => new Gim005Logic(new Gim005Rules { symbolCount = 5 }, 0));
        }

        // ---------------- 미로 구조 (기획 2장 표) ----------------

        static bool[] Open(params int[] gates)
        {
            var o = new bool[8];
            foreach (var g in gates) o[g] = true;
            return o;
        }

        static bool ReachesPlaza(bool[] open)
        {
            return Gim005MazeLayout.Reachable(open).Any(k => Gim005MazeLayout.IsPlaza(new Gim005MazeLayout.Cell(k % 7, k / 7)));
        }

        [Test]
        public void Maze_TruePath_G1_G3_G6_G7_ReachesCenter()
        {
            Assert.IsTrue(ReachesPlaza(Open(Gim005MazeLayout.TruePathGates)));
        }

        [Test]
        public void Maze_EveryTruePathGateIsRequired()
        {
            foreach (int missing in Gim005MazeLayout.TruePathGates)
            {
                var gates = Enumerable.Range(0, 8).Where(g => g != missing).ToArray();
                Assert.IsFalse(ReachesPlaza(Open(gates)), "G" + (missing + 1) + " 없이도 중앙에 닿음");
            }
        }

        [Test]
        public void Maze_NoGatesOpen_OnlyEntranceArea()
        {
            var r = Gim005MazeLayout.Reachable(Open());
            // 입구, 좌우 통로, J2(G1 앞에서 멈춤) 전까지: (3,0) (2,0) (4,0) (5,0)
            CollectionAssert.AreEquivalent(new[] { 3, 2, 4, 5 }, r.ToArray());
        }

        [Test]
        public void Maze_EveryFakePathIsDeadEnd()
        {
            // 각 가짜 길만 열었을 때(진짜 길 마지막 G7은 닫힘) 광장에 닿지 않는다
            Assert.IsFalse(ReachesPlaza(Open(0, 1)), "F1");
            Assert.IsFalse(ReachesPlaza(Open(3)), "F2");
            Assert.IsFalse(ReachesPlaza(Open(7)), "F3");
            Assert.IsFalse(ReachesPlaza(Open(0, 2, 4)), "F4");
            // 진짜 길 G7만 빼고 전부 열어도 닿지 않는다 (가짜 길끼리 이어지지 않음)
            Assert.IsFalse(ReachesPlaza(Open(0, 1, 2, 3, 4, 5, 7)));
        }

        [Test]
        public void Maze_FakePathsUseExpectedGates()
        {
            // F2 끝(엿보기 칸 (5,4))은 G4만 열면 닿고, 광장 칸과 이웃하지만 벽으로 막혀 있다
            var r = Gim005MazeLayout.Reachable(Open(3));
            Assert.IsTrue(r.Contains(Gim005MazeLayout.Key(new Gim005MazeLayout.Cell(5, 4))));
            Assert.IsFalse(Gim005MazeLayout.TryGetEdge(new Gim005MazeLayout.Cell(5, 4), new Gim005MazeLayout.Cell(4, 4), out _));
            // F4 칸 (1,2)는 G1, G3, G5가 모두 열려야 닿는다
            var f4 = Gim005MazeLayout.Key(new Gim005MazeLayout.Cell(1, 2));
            Assert.IsTrue(Gim005MazeLayout.Reachable(Open(0, 2, 4)).Contains(f4));
            Assert.IsFalse(Gim005MazeLayout.Reachable(Open(0, 2)).Contains(f4));
        }

        [Test]
        public void Maze_AllCorridorCellsUsed_AndEachGateOnce()
        {
            var all = Gim005MazeLayout.Reachable(Enumerable.Repeat(true, 8).ToArray());
            Assert.AreEqual(49, all.Count, "광장 9칸 + 통로 40칸 모두 어딘가로 이어진다");
            var gates = Gim005MazeLayout.Edges.Where(e => e.gate >= 0).Select(e => e.gate).OrderBy(g => g).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(0, 8).ToArray(), gates);
        }
    }
}
