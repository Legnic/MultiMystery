using System.Collections.Generic;
using NUnit.Framework;

namespace Gim004.Tests
{
    // GIM-004 판정 규칙 테스트 (명세서 6장·9장 핵심 테스트 표 기준). Test Runner > EditMode에서 실행.
    public class Gim004LogicTests
    {
        const int A = (int)Gim004PotId.A, B = (int)Gim004PotId.B, C = (int)Gim004PotId.C, D = (int)Gim004PotId.D;
        const int E = (int)Gim004PotId.E, F = (int)Gim004PotId.F, G = (int)Gim004PotId.G;

        static Gim004Logic NewLogic(float delay = 0.8f)
        {
            return new Gim004Logic(new Gim004Rules { judgeDelaySeconds = delay });
        }

        static void Fill(Gim004Logic l, int a, int b, int c, int d)
        {
            l.Place(0, a); l.Place(1, b); l.Place(2, c); l.Place(3, d);
        }

        [Test]
        public void DefaultRules_AreValid_AndAnswerIsDEAF()
        {
            var r = new Gim004Rules();
            Assert.IsNull(r.Validate());
            CollectionAssert.AreEqual(new[] { Gim004PotId.D, Gim004PotId.E, Gim004PotId.A, Gim004PotId.F }, r.answer);
        }

        [Test]
        public void Validate_RejectsDuplicateOrWrongLength()
        {
            Assert.IsNotNull(new Gim004Rules { answer = new[] { Gim004PotId.D, Gim004PotId.D, Gim004PotId.A, Gim004PotId.F } }.Validate());
            Assert.IsNotNull(new Gim004Rules { answer = new[] { Gim004PotId.D, Gim004PotId.E, Gim004PotId.A } }.Validate());
        }

        [Test]
        public void StartsEmpty()
        {
            var l = NewLogic();
            Assert.AreEqual(Gim004State.Placing, l.State);
            for (int i = 0; i < 4; i++) Assert.AreEqual(-1, l.Slot(i));
        }

        [Test]
        public void Placing_LessThanFour_DoesNotJudge()
        {
            var l = NewLogic(0f);
            bool judged = false;
            l.OnWrong += () => judged = true;
            l.OnComplete += () => judged = true;
            l.Place(0, D); l.Place(1, E); l.Place(2, A);
            l.Tick(10f);
            Assert.IsFalse(judged);
            Assert.AreEqual(Gim004State.Placing, l.State);
        }

        [Test]
        public void CorrectOrder_Completes_AfterDelay()
        {
            var l = NewLogic(0.8f);
            int complete = 0;
            l.OnComplete += () => complete++;
            Fill(l, D, E, A, F);
            Assert.AreEqual(Gim004State.Judging, l.State);
            l.Tick(0.5f);
            Assert.AreEqual(0, complete);
            l.Tick(0.4f);
            Assert.AreEqual(1, complete);
            Assert.AreEqual(Gim004State.Complete, l.State);
        }

        [Test]
        public void ZeroDelay_JudgesImmediately()
        {
            var l = NewLogic(0f);
            Fill(l, D, E, A, F);
            Assert.IsTrue(l.IsComplete);
        }

        [TestCase(D, F, A, E)]   // E·F 위치를 바꿈
        [TestCase(D, E, A, G)]   // G를 넣음
        [TestCase(D, E, B, F)]   // A 대신 B
        [TestCase(C, E, A, F)]   // D 대신 C
        [TestCase(F, A, E, D)]   // 좌우 반대
        [TestCase(F, E, D, A)]   // 사진 번호(1~4) 순서를 자리 순서로 착각
        public void WrongOrders_AreRejected_AndShelfClears(int a, int b, int c, int d)
        {
            var l = NewLogic(0f);
            int wrong = 0;
            l.OnWrong += () => wrong++;
            Fill(l, a, b, c, d);
            Assert.AreEqual(1, wrong);
            Assert.AreEqual(Gim004State.Placing, l.State);
            Assert.AreEqual(1, l.WrongCount);
            for (int i = 0; i < 4; i++) Assert.AreEqual(-1, l.Slot(i), "오답 후 선반 화분은 모두 처음 자리로");
        }

        [Test]
        public void AfterWrong_CanRetryAndComplete()
        {
            var l = NewLogic(0f);
            Fill(l, D, F, A, E);
            Fill(l, D, E, A, F);
            Assert.IsTrue(l.IsComplete);
            Assert.AreEqual(1, l.WrongCount);
        }

        [Test]
        public void PlacingOnFilledSlot_Swaps()
        {
            var l = NewLogic();
            l.Place(0, C);
            Assert.AreEqual(Gim004PlaceResult.Swapped, l.Place(0, D));
            Assert.AreEqual(D, l.Slot(0));
            Assert.AreEqual(-1, l.Snapshot().SlotOf(C), "밀려난 화분은 선반 상태에서 빠져 처음 자리로");
        }

        [Test]
        public void PlacingPotAlreadyOnShelf_MovesIt()
        {
            var l = NewLogic();
            l.Place(0, D);
            Assert.AreEqual(Gim004PlaceResult.Moved, l.Place(2, D));
            Assert.AreEqual(-1, l.Slot(0));
            Assert.AreEqual(D, l.Slot(2));
        }

        [Test]
        public void SamePotSameSlot_IsIgnored()
        {
            var l = NewLogic();
            l.Place(1, E);
            int changes = 0;
            l.OnChanged += () => changes++;
            Assert.AreEqual(Gim004PlaceResult.Ignored, l.Place(1, E));
            Assert.AreEqual(0, changes);
        }

        [Test]
        public void Take_EmptiesSlot()
        {
            var l = NewLogic();
            l.Place(3, F);
            Assert.IsTrue(l.Take(3));
            Assert.AreEqual(-1, l.Slot(3));
            Assert.IsFalse(l.Take(3), "빈 자리는 뺄 수 없다");
        }

        [Test]
        public void InvalidInput_IsIgnored()
        {
            var l = NewLogic();
            Assert.AreEqual(Gim004PlaceResult.Ignored, l.Place(-1, A));
            Assert.AreEqual(Gim004PlaceResult.Ignored, l.Place(4, A));
            Assert.AreEqual(Gim004PlaceResult.Ignored, l.Place(0, 7));
            Assert.IsFalse(l.Take(9));
        }

        [Test]
        public void DuringJudging_ShelfIsLocked()
        {
            var l = NewLogic(1f);
            Fill(l, D, E, A, F);
            Assert.AreEqual(Gim004PlaceResult.Locked, l.Place(0, C));
            Assert.IsFalse(l.Take(0));
        }

        [Test]
        public void AfterComplete_ShelfIsLocked()
        {
            var l = NewLogic(0f);
            Fill(l, D, E, A, F);
            Assert.AreEqual(Gim004PlaceResult.Locked, l.Place(0, C));
            Assert.IsFalse(l.Take(2), "성공 후 선반 화분은 뺄 수 없다");
            Assert.AreEqual(A, l.Slot(2));
        }

        [Test]
        public void AnswerIsDataDriven()
        {
            var l = new Gim004Logic(new Gim004Rules
            {
                answer = new[] { Gim004PotId.G, Gim004PotId.C, Gim004PotId.B, Gim004PotId.A },
                judgeDelaySeconds = 0f,
            });
            Fill(l, G, C, B, A);
            Assert.IsTrue(l.IsComplete);
        }

        [Test]
        public void SyncState_SlotOfAndEquality()
        {
            var s = Gim004SyncState.Empty;
            s.SetSlot(2, A);
            Assert.AreEqual(2, s.SlotOf(A));
            Assert.AreEqual(-1, s.SlotOf(B));
            Assert.AreEqual(1, s.FilledCount);
            var t = Gim004SyncState.Empty;
            Assert.AreNotEqual(s, t);
            t.SetSlot(2, A);
            Assert.AreEqual(s, t);
        }

        // 기록일지 배치 기록 4줄을 사진 번호(1~4)에 대한 조건으로 바꿔, 가능한 순서 24가지 중 하나만 남는지 확인한다.
        //  1번은 맨 오른쪽 / 3번은 맨 왼쪽 / 2번은 1번보다 두 칸 왼쪽 / 4번은 2번과 1번 사이
        //  사진 → 화분: 1=F, 2=E, 3=D, 4=A  → 유일한 답이 D-E-A-F 여야 한다.
        [Test]
        public void PlacementRecord_HasUniqueSolution_DEAF()
        {
            var photoToPot = new Dictionary<int, int> { { 1, F }, { 2, E }, { 3, D }, { 4, A } };
            var solutions = new List<int[]>();
            foreach (var perm in Permutations(new[] { 1, 2, 3, 4 }))
            {
                int pos1 = System.Array.IndexOf(perm, 1), pos2 = System.Array.IndexOf(perm, 2);
                int pos3 = System.Array.IndexOf(perm, 3), pos4 = System.Array.IndexOf(perm, 4);
                if (pos1 != 3) continue;
                if (pos3 != 0) continue;
                if (pos2 != pos1 - 2) continue;
                if (!(pos4 > System.Math.Min(pos1, pos2) && pos4 < System.Math.Max(pos1, pos2))) continue;
                solutions.Add(perm);
            }
            Assert.AreEqual(1, solutions.Count);
            var pots = new int[4];
            for (int i = 0; i < 4; i++) pots[i] = photoToPot[solutions[0][i]];
            var rules = new Gim004Rules();
            for (int i = 0; i < 4; i++) Assert.AreEqual((int)rules.answer[i], pots[i]);
        }

        static IEnumerable<int[]> Permutations(int[] items)
        {
            if (items.Length <= 1) { yield return items; yield break; }
            for (int i = 0; i < items.Length; i++)
            {
                var rest = new List<int>(items);
                rest.RemoveAt(i);
                foreach (var p in Permutations(rest.ToArray()))
                {
                    var r = new int[items.Length];
                    r[0] = items[i];
                    p.CopyTo(r, 1);
                    yield return r;
                }
            }
        }
    }
}
