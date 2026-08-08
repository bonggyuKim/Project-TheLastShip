using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 배치 확정 흐름(축 E)을 잰다 — 커서 → 판정 → 표 → 오버레이.
    ///
    /// <b>여기서 지키는 것은 셋이다.</b>
    /// <list type="number">
    /// <item><b>스냅과 회전이 규약을 안 깬다.</b> 경계가 <c>1m</c> 격자에 얹히고, 커서 회전이
    /// 조립기 회전(<see cref="LastShiftModuleAssembler.Rotate"/>)과 같은 물건이다. 두 회전이
    /// 갈리면 커서가 만든 칸에 조립기가 프리팹을 못 맞추고, 그건 씬에서만 드러난다.</item>
    /// <item><b>판정기가 안 보는 결함을 커서가 잡는다.</b> 문이 남의 벽에 안 닿는 배치는 판정을
    /// 통과하고 씬에 서고 <b>문이 없다</b> — 정본 구획 열하나가 그 규약을 이미 지키고 있으므로
    /// 그 열하나로 규칙 자체를 대조한다.</item>
    /// <item><b>확정이 표와 오버레이를 같이 움직인다.</b> 둘이 갈리면 발자국은 있는데 압력이
    /// 선체 밴드에서 나오는 방이 생긴다(타당성 검토 §11-1).</item>
    /// </list>
    ///
    /// 정적 표를 만지므로 <see cref="LastShiftCompartments.ClearModules"/> 가 앞뒤에 붙는다.
    /// </summary>
    public sealed class LastShiftPlacementCursorTests
    {
        private const float Tolerance = 0.001f;

        [SetUp]
        public void ClearBefore()
        {
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
            LastShiftPlacementAuthority.Revoke();
        }

        [TearDown]
        public void ClearAfter()
        {
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
            LastShiftPlacementAuthority.Revoke();
        }

        // ── 회전 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 커서 회전과 조립기 회전이 <b>같은 물건</b>이어야 한다. 커서는 모듈을 돌려 표에 넣을
        /// 칸을 만들고 조립기는 프리팹을 돌려 그 칸에 맞추는데, 둘이 다른 식을 쓰면 커서가 놓은
        /// 자리에 프리팹이 <see cref="LastShiftModuleFit.DoorMismatch"/> 로 안 선다.
        /// </summary>
        [Test]
        public void RotatedFootprintAgreesWithAssemblerRotation()
        {
            for (var index = 0; index < LastShiftModuleCatalog.Count; index++)
            {
                var footprint = LastShiftModuleCatalog.At(index).Footprint;
                for (var turns = 0; turns < 4; turns++)
                {
                    var rotated = footprint.Rotated(turns);
                    var swapped = (turns & 1) == 1;

                    Assert.That(rotated.LengthX,
                        Is.EqualTo(swapped ? footprint.WidthZ : footprint.LengthX).Within(Tolerance),
                        $"kind {index} turns {turns} 의 x 치수");
                    Assert.That(rotated.WidthZ,
                        Is.EqualTo(swapped ? footprint.LengthX : footprint.WidthZ).Within(Tolerance),
                        $"kind {index} turns {turns} 의 z 치수");

                    var expected = LastShiftModuleAssembler.Rotate(footprint.DoorPoint, turns);
                    Assert.That((rotated.DoorPoint - expected).magnitude, Is.LessThan(Tolerance),
                        $"kind {index} turns {turns} 의 문점이 조립기 회전과 갈렸다");
                }
            }
        }

        /// <summary>네 회전이 네 면을 다 쓴다. 하나라도 겹치면 그 방향으로는 문을 못 낸다.</summary>
        [Test]
        public void FourQuarterTurnsCoverFourDoorFaces()
        {
            var footprint = LastShiftModuleCatalog.At(0).Footprint;
            var faces = new bool[4];

            for (var turns = 0; turns < 4; turns++) faces[(int)footprint.Rotated(turns).DoorFace] = true;

            Assert.That(faces, Is.All.True, "네 회전이 네 면을 다 덮어야 한다");
        }

        /// <summary>돌린 발자국도 문이 자기 면 안에 들어와야 한다.</summary>
        [Test]
        public void RotationKeepsTheDoorInsideItsFace()
        {
            for (var index = 0; index < LastShiftModuleCatalog.Count; index++)
            for (var turns = 0; turns < 4; turns++)
                Assert.That(LastShiftModuleCatalog.At(index).Footprint.Rotated(turns).DoorFits, Is.True,
                    $"kind {index} turns {turns}");
        }

        // ── 스냅 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 격자에 얹히는 것은 <b>중심이 아니라 경계</b>다. 벽은 경계에 서므로 중심을 얹으면
        /// 홀수 치수 모듈의 벽이 격자에서 <c>0.5m</c> 씩 빠져나간다.
        /// </summary>
        [Test]
        public void SnapPutsFootprintBoundariesOnTheGrid()
        {
            var cursor = new LastShiftPlacementCursor();

            for (var index = 0; index < LastShiftModuleCatalog.Count; index++)
            {
                cursor.Select(index);
                for (var turns = 0; turns < 4; turns++)
                {
                    cursor.Rotate(1);
                    cursor.MoveTo(new Vector3(7.37f, 0f, -11.62f));

                    var candidate = cursor.Candidate;
                    foreach (var edge in new[] { candidate.MinX, candidate.MaxX, candidate.MinZ, candidate.MaxZ })
                        Assert.That(edge - Mathf.Round(edge), Is.EqualTo(0f).Within(Tolerance),
                            $"kind {index} turns {turns} 의 경계 {edge} 가 격자에서 벗어났다");
                }
            }
        }

        /// <summary>방향키 한 번이 정확히 한 칸이다.</summary>
        [Test]
        public void NudgeMovesExactlyOneGridStep()
        {
            var cursor = new LastShiftPlacementCursor();
            var before = cursor.Candidate.MinX;

            cursor.Nudge(3, -2);

            Assert.That(cursor.Candidate.MinX - before,
                Is.EqualTo(3f * LastShiftPlacementCursor.GridMeters).Within(Tolerance));
            Assert.That(cursor.Anchor.z,
                Is.EqualTo(-2f * LastShiftPlacementCursor.GridMeters).Within(Tolerance));
        }

        // ── 붙임 규칙 ───────────────────────────────────────────────────────

        /// <summary>
        /// <b>규칙 자체를 정본으로 대조한다.</b> 정본 구획 열하나는 전부 남의 벽(부모 또는
        /// 선체 외곽)에 문을 얹고 있다 — 하나라도 여기서 걸리면 규칙이 틀린 것이지 그 구획이
        /// 틀린 것이 아니다.
        /// </summary>
        [Test]
        public void EveryFixedCompartmentPassesTheAttachCheck()
        {
            var table = LastShiftCompartments.FixedSpecs;

            foreach (var spec in table)
                Assert.That(LastShiftModuleAttachment.Check(spec, table),
                    Is.EqualTo(LastShiftPlacementFault.None),
                    $"{LastShiftCompartments.NameOf(spec)} 가 붙임 검사에 걸렸다");
        }

        /// <summary>정본 구획의 부모를 좌표만 보고 되찾을 수 있어야 한다 — 커서 자동 부모의 근거다.</summary>
        [Test]
        public void ResolvingParentFromGeometryReproducesTheCanonicalChain()
        {
            var table = LastShiftCompartments.FixedSpecs;

            foreach (var spec in table)
            {
                Assert.That(LastShiftModuleAttachment.TryResolveParent(spec, table, out var parent), Is.True,
                    $"{LastShiftCompartments.NameOf(spec)} 의 벽 주인을 못 찾았다");
                Assert.That(parent, Is.EqualTo(spec.ParentIndex),
                    $"{LastShiftCompartments.NameOf(spec)} 의 부모가 좌표와 갈렸다");
            }
        }

        /// <summary>
        /// 허공에 뜬 배치는 <b>판정을 통과한다</b> — 그래서 커서가 잡아야 한다. 안 잡으면
        /// 표에 들어가고 씬에 서고 <see cref="LastShiftBakedDoorways"/> 가 자를 판을 못 찾아
        /// 문 없는 방이 된다.
        /// </summary>
        [Test]
        public void ModuleFloatingInSpacePassesTheVerdictButFailsTheCursor()
        {
            var cursor = new LastShiftPlacementCursor();
            cursor.Rotate(1);
            cursor.MoveAnchorTo(new Vector3(0f, 0f, -LastShiftShipDimensions.HalfWidth - 8f));

            Assert.That(cursor.Verdict.Rejection, Is.EqualTo(LastShiftPlacementRejection.None),
                "판정기는 붙임을 안 본다 — 이 전제가 깨지면 이 커서 검사가 필요 없다");
            Assert.That(cursor.Faults & LastShiftPlacementFault.DoorOffParentFace,
                Is.EqualTo(LastShiftPlacementFault.DoorOffParentFace));
            Assert.That(cursor.CanCommit, Is.False);
        }

        /// <summary>붙임 결함이 있으면 표를 한 칸도 안 건드린다.</summary>
        [Test]
        public void CommitIsRefusedWhileTheDoorTouchesNothing()
        {
            var cursor = new LastShiftPlacementCursor();
            cursor.Rotate(1);
            cursor.MoveAnchorTo(new Vector3(0f, 0f, -LastShiftShipDimensions.HalfWidth - 8f));

            Assert.That(cursor.TryCommit(out _, out _), Is.False);
            Assert.That(LastShiftCompartments.ModuleCount, Is.Zero);
            Assert.That(LastShiftPlacedModules.ActiveCount, Is.Zero);
        }

        // ── 확정 ────────────────────────────────────────────────────────────

        /// <summary>선체 좌현 면에 딱 붙인 모듈. 부모가 자동으로 선체(<c>-1</c>)로 잡힌다.</summary>
        [Test]
        public void ModuleOnTheHullFaceAttachesToTheHull()
        {
            var cursor = HullAttachedCursor();

            Assert.That(cursor.ParentIndex, Is.EqualTo(-1));
            Assert.That(cursor.Faults, Is.EqualTo(LastShiftPlacementFault.None));
            Assert.That(cursor.Verdict.DoorDepth, Is.EqualTo(1));
            Assert.That(cursor.CanCommit, Is.True, LastShiftPlacementUi.Reason(cursor.Verdict, cursor.Faults));
        }

        /// <summary>
        /// 확정 한 번이 표와 구역 오버레이를 <b>같이</b> 움직인다. 그리고 오버레이가 답하는 구역은
        /// 후보 자기 좌표가 아니라 판정이 정한 값이다(조항 F-1).
        /// </summary>
        [Test]
        public void CommitLandsInBothTheTableAndTheZoneOverlay()
        {
            var cursor = HullAttachedCursor();
            var revision = LastShiftCompartments.Revision;

            Assert.That(cursor.TryCommit(out var index, out var verdict), Is.True);

            Assert.That(index, Is.EqualTo(LastShiftCompartments.FixedCount));
            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(1));
            Assert.That(LastShiftCompartments.Revision, Is.GreaterThan(revision));
            Assert.That(LastShiftPlacedModules.ActiveCount, Is.EqualTo(1));

            var placed = LastShiftCompartments.At(index);
            var inside = new Vector3(placed.CenterX, 0f, placed.CenterZ);
            Assert.That(LastShiftPlacedModules.TryResolve(inside, out var zone), Is.True);
            Assert.That(zone, Is.EqualTo(verdict.Zone));
        }

        /// <summary>
        /// 확정한 자리에 커서를 그대로 두면 겹침으로 물린다. <b>커서를 안 옮기는 것이 의도다</b> —
        /// "여긴 이미 찼다" 가 가장 읽기 쉬운 화면이다.
        /// </summary>
        [Test]
        public void CommittedSpotRejectsTheNextPlacement()
        {
            var cursor = HullAttachedCursor();
            Assert.That(cursor.TryCommit(out _, out _), Is.True);

            Assert.That(cursor.Verdict.Rejection & LastShiftPlacementRejection.OverlapsPlacement,
                Is.EqualTo(LastShiftPlacementRejection.OverlapsPlacement));
            Assert.That(cursor.CanCommit, Is.False);
        }

        /// <summary>
        /// <b>남이 확정하면 내 화면의 "배치 가능" 이 그 자리에서 죽어야 한다.</b> 커서가 판정을
        /// 캐시하므로 표 <see cref="LastShiftCompartments.Revision"/> 을 같이 안 보면, 옆
        /// 승무원이 방금 채운 자리에 내 화면은 계속 초록을 띄운다 — 2인 이상에서 이 커서가
        /// 틀리는 가장 조용한 자리다(§12-9).
        /// </summary>
        [Test]
        public void AnotherCursorsCommitInvalidatesMyCachedVerdict()
        {
            var mine = HullAttachedCursor();
            var theirs = HullAttachedCursor();

            Assert.That(mine.CanCommit, Is.True);
            Assert.That(theirs.TryCommit(out _, out _), Is.True);

            Assert.That(mine.CanCommit, Is.False, "낡은 표를 근거로 초록이 남았다");
            Assert.That(mine.TryCommit(out _, out _), Is.False);
            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(1));
        }

        /// <summary>모듈에 모듈을 잇는다 — 사슬이 한 칸 깊어지고 구역은 뿌리가 그대로 정한다.</summary>
        [Test]
        public void ModuleChainsOntoAnotherModule()
        {
            var first = HullAttachedCursor();
            Assert.That(first.TryCommit(out var firstIndex, out var firstVerdict), Is.True);

            var kind = LastShiftModuleCatalog.At(0);
            var depth = kind.Footprint.Rotated(1).WidthZ;
            var second = new LastShiftPlacementCursor();
            second.Rotate(1);
            second.MoveAnchorTo(new Vector3(0f, 0f, -LastShiftShipDimensions.HalfWidth - 2f * depth));

            Assert.That(second.ParentIndex, Is.EqualTo(firstIndex));
            Assert.That(second.Faults, Is.EqualTo(LastShiftPlacementFault.None));
            Assert.That(second.Verdict.DoorDepth, Is.EqualTo(2));
            Assert.That(second.Verdict.Zone, Is.EqualTo(firstVerdict.Zone), "구역은 사슬 뿌리가 정한다");
            Assert.That(second.TryCommit(out _, out _), Is.True);
            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(2));
        }

        /// <summary>손으로 정한 부모는 자동 해석을 이긴다.</summary>
        [Test]
        public void ManualParentOverridesAutoResolution()
        {
            var cursor = HullAttachedCursor();
            Assert.That(cursor.ParentIndex, Is.EqualTo(-1));

            cursor.AttachTo((int)LastShiftCompartment.Lavatory);

            Assert.That(cursor.AutoParent, Is.False);
            Assert.That(cursor.ParentIndex, Is.EqualTo((int)LastShiftCompartment.Lavatory));
            Assert.That(cursor.Faults & LastShiftPlacementFault.DoorOutsideParentSpan,
                Is.EqualTo(LastShiftPlacementFault.DoorOutsideParentSpan),
                "화장실 벽에는 이 문이 안 닿는다");

            cursor.AttachAutomatically();
            Assert.That(cursor.ParentIndex, Is.EqualTo(-1));
        }

        /// <summary>목록의 다섯이 전부 실제로 놓이는 치수인가 — 못 놓을 것을 목록에 두면 안 된다.</summary>
        [Test]
        public void EveryCatalogEntryCanActuallyBePlaced()
        {
            for (var index = 0; index < LastShiftModuleCatalog.Count; index++)
            {
                LastShiftCompartments.ClearModules();

                var cursor = HullAttachedCursor(index);
                Assert.That(cursor.CanCommit, Is.True,
                    $"{LastShiftModuleCatalog.At(index).Name} — {LastShiftPlacementUi.Reason(cursor.Verdict, cursor.Faults)}");
            }
        }

        // ── 커서 소유권 (§12-9) ─────────────────────────────────────────────

        [Test]
        public void OnlyOneCrewHoldsTheCursor()
        {
            Assert.That(LastShiftPlacementAuthority.TryClaim(1), Is.True);
            Assert.That(LastShiftPlacementAuthority.TryClaim(2), Is.False, "뺏기는 없다");
            Assert.That(LastShiftPlacementAuthority.TryClaim(1), Is.True, "자기가 다시 잡는 것은 실패가 아니다");
            Assert.That(LastShiftPlacementAuthority.HolderId, Is.EqualTo(1));
        }

        [Test]
        public void ReleasingSomeoneElsesCursorDoesNothing()
        {
            LastShiftPlacementAuthority.TryClaim(1);

            Assert.That(LastShiftPlacementAuthority.Release(2), Is.False);
            Assert.That(LastShiftPlacementAuthority.IsHeldBy(1), Is.True);

            Assert.That(LastShiftPlacementAuthority.Release(1), Is.True);
            Assert.That(LastShiftPlacementAuthority.IsHeld, Is.False);
            Assert.That(LastShiftPlacementAuthority.TryClaim(2), Is.True);
        }

        /// <summary>잡은 사람이 나가면 호스트가 푼다 — 안 그러면 기항을 벗어날 방법이 없다.</summary>
        [Test]
        public void HostCanRevokeAnAbandonedCursor()
        {
            LastShiftPlacementAuthority.TryClaim(3);

            LastShiftPlacementAuthority.Revoke();

            Assert.That(LastShiftPlacementAuthority.IsHeld, Is.False);
            Assert.That(LastShiftPlacementAuthority.TryClaim(4), Is.True);
        }

        // ── 표본 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 선체 좌현(<c>z = -HalfWidth</c>) 면에 문을 얹은 커서. 좌현을 고른 것은 정본 구획이
        /// 하나도 안 붙어 있는 유일한 긴 면이라 표본이 기존 구획과 안 부딪히기 때문이다.
        /// </summary>
        private static LastShiftPlacementCursor HullAttachedCursor(int catalogIndex = 0)
        {
            var cursor = new LastShiftPlacementCursor();
            cursor.Select(catalogIndex);

            // 회전 1 이 기준 자세의 MinX 문을 MaxZ 면으로 보낸다 — 그 면이 선체를 향한다.
            cursor.Rotate(1);
            var depth = LastShiftModuleCatalog.At(catalogIndex).Footprint.Rotated(1).WidthZ;
            cursor.MoveAnchorTo(new Vector3(0f, 0f, -LastShiftShipDimensions.HalfWidth - depth));
            return cursor;
        }
    }
}
