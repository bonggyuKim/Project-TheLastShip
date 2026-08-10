using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 거점 배치 탭(<c>docs/outboard-outpost-and-map-final-v1.md</c> §4.4, 단계 <c>O-2</c>)을 잰다.
    ///
    /// <b>여기서 지키는 것은 넷이다.</b>
    /// <list type="number">
    /// <item><b>조항 <c>O-2</c>.</b> 거점은 자재만 쓴다 — 골조를 세워도 정비 여력이 한 푼도
    /// 안 움직인다. 이게 깨지면 두 자원을 같이 요구하는 항목이 생긴 것이고, §3.4 가 명시적으로
    /// 금지한 상태다.</item>
    /// <item><b>계류가 실제로 계류다.</b> 골조는 잔해(뿌리) 또는 이미 세운 것의 면에 붙어야
    /// 들어간다 — 허공에 뜬 골조가 통과하면 자유면 하이라이트가 아무것도 안 뜻하게 된다.</item>
    /// <item><b>판정 층이 둘뿐이다</b>(§4.4) — 겹침과 사슬. 선체 침범·이탈은 거점에 없다.
    /// 원반 바깥에서 짓는 것이 <see cref="LastShiftPlacementRejection.OverlapsHullInterior"/> 로
    /// 물리면 거점은 어디에도 못 선다.</item>
    /// <item><b>값을 낸 만큼만 나간다.</b> 자재가 모자라면 표가 안 움직이고, 뜯으면 그 값이
    /// 그대로 돌아온다.</item>
    /// </list>
    ///
    /// 정적 표·원장을 만지므로 앞뒤로 비운다.
    /// </summary>
    public sealed class LastShiftOutpostPlacementTests
    {
        private readonly List<LastShiftFreeFace> faces = new();

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
            LastShiftOutpost.ClearPieces();
            LastShiftMaterials.Clear();
            LastShiftMaintenance.Clear();
        }

        // ── 카탈로그 ────────────────────────────────────────────────────────

        [Test]
        public void MooringFrameCostsExactlyOneSalvageField()
        {
            var frame = LastShiftOutpostCatalog.At(LastShiftOutpostCatalog.MooringFrame);

            // 조항 T-5 — 가격이 필드 전량과 같아야 자재가 4 → 0 이 되고, 튜토리얼 9단계에서
            // 조항 O-2 가 저절로 읽힌다(튜토리얼 §2-1).
            Assert.AreEqual(LastShiftSalvage.ChunksPerField, frame.MaterialCost);
        }

        [Test]
        public void MooringFrameFootprintIsNotSquare()
        {
            var frame = LastShiftOutpostCatalog.At(LastShiftOutpostCatalog.MooringFrame);

            // 조항 T-3 — 정사각이면 90° 를 돌려도 발자국이 그대로라 회전을 가르칠 실패가
            // 아예 안 일어난다.
            Assert.AreNotEqual(frame.LengthX, frame.WidthZ);
            Assert.IsTrue(frame.Footprint.DoorFits, "계류면에 구멍 폭이 안 들어간다");
        }

        // ── 계류 ────────────────────────────────────────────────────────────

        [Test]
        public void FreshCursorAlreadyMoorsToTheSalvageAnchor()
        {
            var cursor = new LastShiftOutpostCursor();

            // §5.1 "실패할 수 없다" — 화면이 열리는 첫 프레임이 빨간색이면 안 된다.
            Assert.AreEqual(LastShiftOutpost.AnchorIndex, cursor.ParentIndex);
            Assert.AreEqual(1, cursor.ChainDepth);
            Assert.IsTrue(cursor.CanCommit, LastShiftPlacementCommands.Reason(cursor.Rejection, cursor.Faults));
        }

        [Test]
        public void FrameFloatingInVacuumIsRejected()
        {
            LastShiftMaterials.Deposit(LastShiftSalvage.ChunksPerField);

            var anchor = LastShiftOutpost.Anchor;
            var cursor = LastShiftOutpostCommands.CursorFor(
                LastShiftOutpostCatalog.MooringFrame, 0, anchor.MinX - 20f, anchor.MinZ);

            Assert.IsFalse(cursor.CanCommit);
            Assert.IsFalse(LastShiftOutpostCommands.TryPlace(cursor, out var outcome));
            Assert.AreEqual(LastShiftPlacementCommandResult.Rejected, outcome.Result);
            Assert.AreEqual(0, LastShiftOutpost.PieceCount);
            Assert.AreEqual(LastShiftSalvage.ChunksPerField, LastShiftMaterials.Balance, "물린 배치가 자재를 태웠다");
        }

        [Test]
        public void OverlappingFrameIsRejected()
        {
            LastShiftMaterials.Deposit(LastShiftSalvage.ChunksPerField * 2);
            Assert.IsTrue(LastShiftOutpostCommands.TryPlace(new LastShiftOutpostCursor(), out _));

            var again = new LastShiftOutpostCursor();

            Assert.IsFalse(again.CanCommit);
            Assert.AreNotEqual(
                LastShiftPlacementRejection.None,
                again.Rejection & LastShiftPlacementRejection.OverlapsPlacement);
            Assert.AreEqual(1, LastShiftOutpost.PieceCount);
        }

        [Test]
        public void OutpostIsBuiltOutsideTheDiscAndTheHullRuleNeverFires()
        {
            var cursor = new LastShiftOutpostCursor();
            var candidate = cursor.Candidate;

            // 원반 바깥에 서는 것이 조항 O-5 다. 선체 판정을 그대로 물려 놓았다면 여기서
            // 통과할 수 없다 — 거점 판정에 그 층이 없다는 것을 좌표로 확인한다.
            Assert.IsFalse(LastShiftHullShell.Contains(candidate.CenterX, candidate.CenterZ));
            Assert.AreEqual(LastShiftPlacementRejection.None, cursor.Rejection);
        }

        // ── 값 ──────────────────────────────────────────────────────────────

        [Test]
        public void PlacingSpendsMaterialsDownToZero()
        {
            LastShiftMaterials.Deposit(LastShiftSalvage.ChunksPerField);

            Assert.IsTrue(LastShiftOutpostCommands.TryPlace(new LastShiftOutpostCursor(), out var outcome));
            Assert.AreEqual(LastShiftSalvage.ChunksPerField, outcome.Cost);
            Assert.AreEqual(0, LastShiftMaterials.Balance);
            Assert.AreEqual(1, LastShiftOutpost.PieceCount);
        }

        [Test]
        public void PlacingNeverTouchesTheMaintenanceLedger()
        {
            LastShiftMaterials.Deposit(LastShiftSalvage.ChunksPerField);
            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);
            var before = LastShiftMaintenance.Balance;

            Assert.IsTrue(LastShiftOutpostCommands.TryPlace(new LastShiftOutpostCursor(), out _));

            // 조항 O-2 — 한 항목이 두 자원을 같이 요구하지 않는다.
            Assert.AreEqual(before, LastShiftMaintenance.Balance);
        }

        [Test]
        public void ShortMaterialsLeaveTheTableUntouched()
        {
            LastShiftMaterials.Deposit(LastShiftSalvage.ChunksPerField - 1);

            Assert.IsFalse(LastShiftOutpostCommands.TryPlace(new LastShiftOutpostCursor(), out var outcome));
            Assert.AreEqual(LastShiftPlacementCommandResult.Unaffordable, outcome.Result);
            Assert.AreEqual(0, LastShiftOutpost.PieceCount);
            Assert.AreEqual(LastShiftSalvage.ChunksPerField - 1, LastShiftMaterials.Balance);
        }

        [Test]
        public void RemovingRefundsWithoutInflatingLifetimeSalvage()
        {
            LastShiftMaterials.Deposit(LastShiftSalvage.ChunksPerField);
            var salvaged = LastShiftMaterials.LifetimeSalvaged;

            Assert.IsTrue(LastShiftOutpostCommands.TryPlace(new LastShiftOutpostCursor(), out _));
            Assert.IsTrue(LastShiftOutpostCommands.TryRemoveLast(out var outcome));

            Assert.AreEqual(LastShiftSalvage.ChunksPerField, outcome.Refunded);
            Assert.AreEqual(LastShiftSalvage.ChunksPerField, LastShiftMaterials.Balance);
            Assert.AreEqual(0, LastShiftOutpost.PieceCount);

            // 환수는 밖에서 들여온 것이 아니다 — 세웠다 뜯기를 반복해도 이 수는 안 오른다.
            Assert.AreEqual(salvaged, LastShiftMaterials.LifetimeSalvaged);
        }

        [Test]
        public void TheSalvageAnchorCannotBeTornDown()
        {
            Assert.IsFalse(LastShiftOutpostCommands.TryRemoveLast(out var outcome));
            Assert.AreEqual(LastShiftPlacementCommandResult.NothingToRemove, outcome.Result);
            Assert.AreEqual(LastShiftOutpost.FixedCount, LastShiftOutpost.Count);
        }

        // ── 자유면 ──────────────────────────────────────────────────────────

        [Test]
        public void OutpostFreeFacesComeFromTheOutpostTableOnly()
        {
            LastShiftFreeFaces.Collect(
                LastShiftOutpost.Specs, faces,
                LastShiftFreeFaces.ClearanceMeters, LastShiftFreeFaces.MinimumRunMeters, false);

            // 잔해 네 면. 선체를 세면 광장 둘레가 원반 바깥 도면에 섞여 들어온다.
            Assert.AreEqual(4, faces.Count);
            foreach (var face in faces)
            {
                Assert.AreNotEqual(LastShiftFreeFaces.HullOwner, face.OwnerIndex);
                Assert.AreEqual(LastShiftOutpost.AnchorIndex, face.OwnerIndex);
            }
        }

        [Test]
        public void MooringFrameEatsTheFaceItMooredTo()
        {
            LastShiftMaterials.Deposit(LastShiftSalvage.ChunksPerField);
            var cursor = new LastShiftOutpostCursor();
            var mooredPlane = cursor.Candidate.DoorPlaneCoordinate;
            Assert.IsTrue(LastShiftOutpostCommands.TryPlace(cursor, out _));

            LastShiftFreeFaces.Collect(
                LastShiftOutpost.Specs, faces,
                LastShiftFreeFaces.ClearanceMeters, LastShiftFreeFaces.MinimumRunMeters, false);

            // 계류한 면은 더 이상 통짜 자유면이 아니다 — 도면이 이미 붙은 자리를 굵게 그리면
            // 플레이어는 판정기가 물리는 자리를 계속 고른다.
            foreach (var face in faces)
            {
                if (face.OwnerIndex != LastShiftOutpost.AnchorIndex) continue;
                if (!face.OnXFace) continue;
                if (!Mathf.Approximately(face.PlaneCoordinate, mooredPlane)) continue;

                Assert.Less(face.Length, LastShiftOutpost.AnchorSpan - 0.01f, "계류한 면이 통째로 남았다");
            }

            // 세운 골조가 자기 자유면을 새로 낸다 — §4.3 표 1행("자유면을 처음 만든다")이
            // 실제로 성립하는지가 이 한 줄이다.
            var fromFrame = 0;
            foreach (var face in faces)
                if (face.OwnerIndex > LastShiftOutpost.AnchorIndex)
                    fromFrame++;

            Assert.Greater(fromFrame, 0, "골조가 자유면을 하나도 안 만들었다");
        }

        // ── 씬 ──────────────────────────────────────────────────────────────

        [Test]
        public void ConfirmedFrameActuallyStandsInTheScene()
        {
            LastShiftMaterials.Deposit(LastShiftSalvage.ChunksPerField);
            var cursor = new LastShiftOutpostCursor();
            var candidate = cursor.Candidate;
            Assert.IsTrue(LastShiftOutpostCommands.TryPlace(cursor, out _));

            // 카드의 완료 기준 — <b>고른 것이 실제로 서는가</b>. 표만 늘고 씬이 안 서면
            // 도면에서 확정을 눌러도 밖에 나갔을 때 아무것도 없다.
            Assert.AreEqual(1, LastShiftOutpostAssembler.Rebuild());

            var root = GameObject.Find(LastShiftOutpostAssembler.RootName);
            Assert.IsNotNull(root, "거점 칸이 씬에 없다");
            Assert.AreEqual(1, root.transform.childCount);
            Assert.AreEqual(LastShiftOutpost.DeckY, root.transform.position.y, 0.001f);

            var piece = root.transform.GetChild(0);
            Assert.AreEqual(candidate.CenterX, piece.localPosition.x, 0.001f);
            Assert.AreEqual(candidate.CenterZ, piece.localPosition.z, 0.001f);

            // 갑판 판 하나 + 기둥 넷. 벽·천장이 붙으면 진공에 기밀 방이 선 것이다(§4.4).
            Assert.AreEqual(5, piece.childCount);
            Assert.IsNotNull(piece.Find("Deck"));

            Assert.IsTrue(LastShiftOutpostCommands.TryRemoveLast(out _));
            Assert.AreEqual(0, LastShiftOutpostAssembler.Rebuild());
            Assert.AreEqual(0, root.transform.childCount);
        }

        [Test]
        public void ScreenConfirmsFromTheOutpostTabAndSpendsMaterials()
        {
            LastShiftMaterials.Deposit(LastShiftSalvage.ChunksPerField);
            var host = new GameObject("PlacementUiHost");

            try
            {
                var ui = host.AddComponent<LastShiftPlacementUi>();

                // 기본 탭은 선체다 — 거점이 기본이면 기존 화면의 첫 인상이 바뀐다.
                Assert.AreEqual(LastShiftPlacementTab.Hull, ui.Tab);

                ui.SelectTab(LastShiftPlacementTab.Outpost);
                Assert.AreEqual(LastShiftPlacementTab.Outpost, ui.Tab);
                Assert.AreEqual(LastShiftOutpostCatalog.MooringFrame, ui.OutpostCursor.CatalogIndex);

                // 카드의 완료 기준을 <b>화면 쪽 문</b>으로 한 번 더 잰다 — 명령 층만 재면
                // 탭 배선이 빠져도 초록이다.
                Assert.IsTrue(ui.Confirm());
                Assert.AreEqual(1, LastShiftOutpost.PieceCount);
                Assert.AreEqual(0, LastShiftMaterials.Balance);

                var root = GameObject.Find(LastShiftOutpostAssembler.RootName);
                Assert.IsNotNull(root);
                Assert.AreEqual(1, root.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // ── 항해 단위 ───────────────────────────────────────────────────────

        [Test]
        public void BeginningAVoyageTearsTheOutpostDown()
        {
            LastShiftMaterials.Deposit(LastShiftSalvage.ChunksPerField);
            Assert.IsTrue(LastShiftOutpostCommands.TryPlace(new LastShiftOutpostCursor(), out _));

            LastShiftOutpost.BeginVoyage();

            // 조항 O-6 — 골조는 항해 시작 지급이다. 남겨 두면 다음 항해가 공짜로 시작한다.
            Assert.AreEqual(0, LastShiftOutpost.PieceCount);
            Assert.AreEqual(LastShiftOutpost.FixedCount, LastShiftOutpost.Count);
        }
    }
}
