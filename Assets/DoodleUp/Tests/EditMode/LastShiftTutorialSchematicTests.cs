using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 튜토리얼 <c>7</c>~<c>10</c>단계 — 도면·거점 구간과 완료 처리.
    /// 정본은 <c>docs/tutorial-o3-free-placement-farming-deposit-v1.md</c> 다.
    ///
    /// <b>여기서 재는 것은 넷이다.</b>
    /// <list type="number">
    /// <item><b>회전을 가르칠 실패가 실제로 일어난다</b>(조항 <c>T-3</c>). 화면이 골조를 어긋난
    /// 자세로 세워 두고, <c>R</c> <b>한 번</b>이면 초록이 된다 — 두 번 이상이면 §7 수용 문장의
    /// "한 번 돌려" 가 깨지고, 안 어긋나면 가르칠 장면이 통째로 없다.</item>
    /// <item><b>잠금이 단계로 열린다</b>(조항 <c>T-4</c>). 선체 탭은 <c>9</c>단계가, 되돌리기는
    /// <c>10</c>단계가 연다. 잠금은 화면에서 숨는 것으로 끝나면 안 되고 <b>동사 자체가 안 먹어야</b>
    /// 한다 — 숨기기만 하면 예전 키 하나가 그대로 통과한다.</item>
    /// <item><b>골조가 서면 선체 탭이 열리고 화면이 거기로 넘어간다</b>(§2-1). 자재가
    /// <c>4 → 0</c> 이 된 바로 다음 화면에 여력 잔액 하나만 떠야 조항 <c>O-2</c> 가 설명 없이
    /// 읽힌다.</item>
    /// <item><b><c>10</c>단계 진입이 곧 완료다</b>(조항 <c>T-6</c>). 플래그가 서고 잠금과
    /// <c>T-5</c>·<c>T-8</c> 예외가 한꺼번에 끝난다. <b><c>9</c>단계를 지난 판만 받는다.</b></item>
    /// </list>
    ///
    /// 정적 표·원장을 만지므로 앞뒤로 다 비운다.
    /// </summary>
    public sealed class LastShiftTutorialSchematicTests
    {
        [SetUp]
        public void ClearBefore() => ClearAll();

        [TearDown]
        public void ClearAfter()
        {
            ClearAll();

            var root = GameObject.Find(LastShiftOutpostAssembler.RootName);
            if (root != null) Object.DestroyImmediate(root);
        }

        private static void ClearAll()
        {
            LastShiftVoyage.Clear();
            LastShiftOutpost.ClearPieces();
            LastShiftMaterials.Clear();
            LastShiftMaintenance.Clear();
        }

        // ── 도구 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 도면이 열린 자리(<c>7</c>단계)에 세운다. 파밍 구간은
        /// <see cref="LastShiftTutorialTests"/> 가 이미 재므로 여기서는 단계만 민다.
        /// </summary>
        private static void ArriveAtSchematic()
        {
            LastShiftVoyage.BeginVoyage();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 1);
            LastShiftTutorial.AdvanceTo(LastShiftTutorialStep.Schematic);

            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.Schematic));
        }

        /// <summary>골조 하나를 살 수 있는 자재. 조항 <c>T-5</c> 로 필드 전량과 같은 값이다.</summary>
        private static void GrantOneFrameOfMaterials() =>
            LastShiftMaterials.Deposit(LastShiftOutpostCatalog.At(LastShiftOutpostCatalog.MooringFrame).MaterialCost);

        private static LastShiftPlacementUi NewScreen() =>
            new GameObject(nameof(LastShiftTutorialSchematicTests)).AddComponent<LastShiftPlacementUi>();

        private static void Destroy(LastShiftPlacementUi screen)
        {
            if (screen != null) Object.DestroyImmediate(screen.gameObject);
        }

        // ── 회전 (조항 T-3) ─────────────────────────────────────────────────

        /// <summary>
        /// 어긋난 첫 자세가 실제로 안 들어가고, <b><c>R</c> 한 번</b>이면 들어간다.
        /// <c>LastShiftPlacementUi.ArmTutorialRotation</c> 이 커서에 거는 것이 이 <c>-1</c>단이고,
        /// 여기서 재는 것은 그 한 단이 <c>8</c>단계를 성립시키는가다.
        /// </summary>
        [Test]
        public void TheArmedPoseFailsAndASingleRotationFixesIt()
        {
            var cursor = new LastShiftOutpostCursor();
            Assert.That(cursor.CanCommit, Is.True, "기준 자세는 계류가 성립한다 (§5.1)");

            cursor.Rotate(-1);
            Assert.That(cursor.CanCommit, Is.False,
                "안 맞는 자세가 없으면 8단계가 가르칠 실패 자체가 안 일어난다");

            cursor.Rotate(1);
            Assert.That(cursor.CanCommit, Is.True, "§7 수용 문장 — 한 번 돌려 세운다");
        }

        // ── 잠금 (조항 T-4) ─────────────────────────────────────────────────

        /// <summary>선체 탭은 <c>9</c>단계가 연다. 그 전에는 눌러도 안 넘어간다.</summary>
        [Test]
        public void TheHullTabIsInertUntilStepNine()
        {
            ArriveAtSchematic();

            var screen = NewScreen();
            try
            {
                screen.SelectTab(LastShiftPlacementTab.Outpost);

                Assert.That(LastShiftTutorial.HullTabLocked, Is.True);
                screen.SelectTab(LastShiftPlacementTab.Hull);
                Assert.That(screen.Tab, Is.EqualTo(LastShiftPlacementTab.Outpost),
                    "숨기기만 하면 T 키와 예전 클릭이 그대로 통과한다");

                LastShiftTutorial.AdvanceTo(LastShiftTutorialStep.HullUnlocked);
                Assert.That(LastShiftTutorial.HullTabLocked, Is.False);

                screen.SelectTab(LastShiftPlacementTab.Hull);
                Assert.That(screen.Tab, Is.EqualTo(LastShiftPlacementTab.Hull));
            }
            finally
            {
                Destroy(screen);
            }
        }

        /// <summary>
        /// 되돌리기는 <c>9</c>단계에서도 안 열린다 — 골조를 지우면 자재가 <c>0</c> 인 채로
        /// 다시 살 수 없어 판이 막힌다. 손을 떼는 <c>10</c>단계가 같이 연다.
        /// </summary>
        [Test]
        public void UndoStaysInertUntilTheTutorialLetsGo()
        {
            ArriveAtSchematic();
            GrantOneFrameOfMaterials();

            var screen = NewScreen();
            try
            {
                screen.SelectTab(LastShiftPlacementTab.Outpost);
                Assert.That(screen.Confirm(), Is.True, "기준 자세라 그대로 들어간다");
                Assert.That(LastShiftOutpost.PieceCount, Is.EqualTo(1));

                // 9단계로 올라와도 환수는 여전히 없다.
                Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.HullUnlocked));
                Assert.That(LastShiftTutorial.UndoLocked, Is.True);

                screen.SelectTab(LastShiftPlacementTab.Outpost);
                Assert.That(screen.UndoLast(), Is.False);
                Assert.That(LastShiftOutpost.PieceCount, Is.EqualTo(1), "튜토리얼 중에는 뜯을 수 없다");
                Assert.That(LastShiftMaterials.Balance, Is.Zero, "환수가 자재를 되돌리면 안 된다");

                LastShiftTutorial.HandOff();
                Assert.That(LastShiftTutorial.UndoLocked, Is.False);
                Assert.That(screen.UndoLast(), Is.True, "손을 뗀 뒤에는 평시와 같다");
            }
            finally
            {
                Destroy(screen);
            }
        }

        // ── 확정 (§2-1) ─────────────────────────────────────────────────────

        /// <summary>
        /// 골조가 서면 <c>9</c>단계가 열리고 화면이 <b>선체 탭으로 넘어간다</b>. 머리줄이 지금
        /// 탭의 잔액만 띄우므로, 넘어가는 것이 곧 "여력 잔액이 처음 뜬다" 이고 그 화면에서 자재는
        /// <c>0</c> 이다 — 두 잔액이 나란히 뜨는 장면 없이 조항 <c>O-2</c> 가 읽히는 순서다.
        /// </summary>
        [Test]
        public void StandingTheFrameOpensTheHullTabWithMaterialsAtZero()
        {
            ArriveAtSchematic();
            GrantOneFrameOfMaterials();
            Assert.That(LastShiftMaterials.Balance, Is.EqualTo(LastShiftSalvage.ChunksPerField));

            var screen = NewScreen();
            try
            {
                screen.SelectTab(LastShiftPlacementTab.Outpost);
                Assert.That(screen.Confirm(), Is.True);

                Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.HullUnlocked));
                Assert.That(screen.Tab, Is.EqualTo(LastShiftPlacementTab.Hull));
                Assert.That(LastShiftMaterials.Balance, Is.Zero, "가격이 필드 전량과 같다 (조항 T-5)");
                Assert.That(LastShiftMaintenance.Balance, Is.GreaterThan(0),
                    "자재가 0 인데 지을 수 있다 — 조항 O-2 를 여기서 배운다");
            }
            finally
            {
                Destroy(screen);
            }
        }

        /// <summary>
        /// 자재가 모자라면 단계가 안 올라간다. 확정이 물린 자리에서 탭이 열리면 <c>9</c>단계가
        /// 가르치는 것("<c>4 → 0</c> 을 보고 나서 여력을 만난다")이 통째로 빈다.
        /// </summary>
        [Test]
        public void ARejectedFrameLeavesTheTutorialWhereItWas()
        {
            ArriveAtSchematic();

            var screen = NewScreen();
            try
            {
                screen.SelectTab(LastShiftPlacementTab.Outpost);
                Assert.That(screen.Confirm(), Is.False, "자재가 없다");

                Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.Schematic));
                Assert.That(LastShiftTutorial.HullTabLocked, Is.True);
            }
            finally
            {
                Destroy(screen);
            }
        }

        // ── 완료 (조항 T-6) ─────────────────────────────────────────────────

        /// <summary>
        /// <c>10</c>단계 진입이 곧 완료다. 플래그가 서고, 잠금도 안내 띠도 <c>T-5</c>·<c>T-8</c>
        /// 예외도 한꺼번에 끝난다.
        /// </summary>
        [Test]
        public void HandingOffCompletesTheTutorialAndReleasesEveryLock()
        {
            ArriveAtSchematic();
            LastShiftTutorial.AdvanceTo(LastShiftTutorialStep.HullUnlocked);

            LastShiftTutorial.HandOff();

            Assert.That(LastShiftTutorial.HasCompleted, Is.True);
            Assert.That(LastShiftTutorial.IsRunning, Is.False, "띠가 사라지는 것이 '손을 뗀다' 다");
            Assert.That(LastShiftTutorial.IsTutorialPort, Is.False, "T-5·T-8 예외도 같이 끝난다");
            Assert.That(LastShiftTutorial.HullTabLocked, Is.False);
            Assert.That(LastShiftTutorial.UndoLocked, Is.False);
        }

        /// <summary>
        /// <b><c>9</c>단계를 지난 판만 완료를 받는다.</b> 중간에서 받을 수 있으면 화면 어딘가의
        /// 실수 하나가 튜토리얼을 통째로 건너뛴 세이브를 만든다.
        /// </summary>
        [Test]
        public void HandingOffBeforeStepNineDoesNothing()
        {
            ArriveAtSchematic();

            LastShiftTutorial.HandOff();

            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.Schematic));
            Assert.That(LastShiftTutorial.HasCompleted, Is.False);
        }

        /// <summary>
        /// 도면 구간을 다 지나고 나온 판은 <b>둘째 항해에서 튜토리얼이 안 뜬다</b>(조항 <c>T-6</c>).
        /// 잔해도 평시 총량으로 돌아온다 — <c>T-5</c> 의 인원 배수가 첫 기항 하나에만 걸린다.
        /// </summary>
        [Test]
        public void TheNextVoyageRunsWithoutTheTutorial()
        {
            ArriveAtSchematic();
            LastShiftTutorial.AdvanceTo(LastShiftTutorialStep.HullUnlocked);
            LastShiftTutorial.HandOff();

            LastShiftVoyage.BeginVoyage();
            LastShiftTutorial.SetCrewCount(4);
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 1);

            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.None));
            Assert.That(LastShiftTutorial.IsArmed, Is.False);
            Assert.That(LastShiftSalvage.FieldChunks, Is.EqualTo(LastShiftSalvage.ChunksPerField),
                "인원 배수는 튜토리얼 기항 하나에만 걸린다");
        }

        /// <summary>
        /// 도면 구간 <b>도중에 출항하면</b> 완료가 아니다 — 다음 판에서 다시 뜬다. 조항
        /// <c>T-6</c> 이 "<c>10</c>단계에 도달한 판만" 으로 잡은 경계가 이것이다.
        /// </summary>
        [Test]
        public void LeavingPortMidSchematicDoesNotCount()
        {
            ArriveAtSchematic();
            LastShiftTutorial.AdvanceTo(LastShiftTutorialStep.HullUnlocked);

            LastShiftTutorial.LeavePort();

            Assert.That(LastShiftTutorial.HasCompleted, Is.False);
            Assert.That(LastShiftTutorial.Step, Is.EqualTo(LastShiftTutorialStep.None));
        }
    }
}
