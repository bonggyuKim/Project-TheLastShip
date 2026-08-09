using System.Collections.Generic;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 자유면 계산(<see cref="LastShiftFreeFaces"/>)을 잰다 — 선체 도면 개편에서 유일하게
    /// 새로 드는 계산이다(<c>docs/core-four-rooms-and-hull-schematic-v1.md</c> §4.6).
    ///
    /// <b>여기서 지키는 것은 넷이다.</b>
    /// <list type="number">
    /// <item><b>맞닿은 면은 자유면이 아니다.</b> 이게 안 지켜지면 도면이 이미 방이 붙은 벽을
    /// 굵게 그리고, 플레이어는 판정기가 물리는 자리를 계속 고른다.</item>
    /// <item><b><c>1.6m</c> 미만 자투리는 안 그린다.</b> 문 구멍이 그 폭이라 그보다 좁은 면에는
    /// 어떤 방도 못 붙는다 — <see cref="LastShiftModuleAttachment"/> 가 확정 순간 같은 값으로
    /// 물린다.</item>
    /// <item><b>선체 안쪽을 향한 면은 안 나온다.</b> 고정 구획의 안쪽 벽을 자유면으로 내면
    /// 그 자리는 <see cref="LastShiftPlacementRejection.OverlapsHullInterior"/> 로 전부 물린다.</item>
    /// <item><b>자유면이 실제로 배치를 통과시킨다.</b> 굵은 선 위에 방을 대면 판정이 통과해야
    /// 한다 — 이 하나가 도면과 판정기가 같은 배를 보고 있다는 유일한 증거다.</item>
    /// </list>
    ///
    /// 정적 표를 만지므로 <see cref="LastShiftCompartments.ClearModules"/> 가 앞뒤에 붙는다.
    /// </summary>
    public sealed class LastShiftFreeFacesTests
    {
        private const float Tolerance = 0.01f;

        private readonly List<LastShiftFreeFace> faces = new();

        [SetUp]
        public void ClearBefore()
        {
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
        }

        [TearDown]
        public void ClearAfter()
        {
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
        }

        // ── 기본 규약 ───────────────────────────────────────────────────────

        /// <summary>
        /// 시작 배에서 자유면이 하나도 없으면 도면을 켤 이유가 없다. <b>가장 먼저 깨질 자리가
        /// 여기다</b> — 막는 조건을 하나 잘못 넣으면 전부 사라지고, 화면에는 아무 일도 안
        /// 일어난 것으로 보인다.
        /// </summary>
        [Test]
        public void CanonicalShipExposesFreeFaces()
        {
            LastShiftFreeFaces.Collect(LastShiftCompartments.Specs, faces);

            Assert.That(faces.Count, Is.GreaterThan(0), "시작 배에 붙일 면이 하나도 없다");
            Assert.That(faces.All(face => face.Length >= LastShiftFreeFaces.MinimumRunMeters - Tolerance),
                "문 구멍보다 좁은 구간이 자유면으로 나왔다");
        }

        /// <summary>
        /// <b>창이 있는 좌현 긴 벽은 통째로 비어 있다.</b> 정본 구획표가 좌현에 아무것도 안
        /// 붙였으므로(<c>ServerRoom</c>·<c>Hydroponics</c>·<c>MedBay</c> 전부 우현) 선체
        /// <c>z = -3</c> 면은 전장 <c>38m</c> 가 한 구간이어야 한다.
        /// </summary>
        [Test]
        public void PortSideHullFaceIsOneUnbrokenRun()
        {
            LastShiftFreeFaces.Collect(LastShiftCompartments.Specs, faces);

            var portSide = faces.Where(face =>
                face.OwnerIndex == LastShiftFreeFaces.HullOwner &&
                face.Face == LastShiftModuleFace.MinZ).ToArray();

            Assert.That(portSide.Length, Is.EqualTo(1), "좌현 선체 면이 쪼개졌다");
            Assert.That(portSide[0].Length, Is.EqualTo(LastShiftShipDimensions.InteriorLength).Within(Tolerance));
        }

        /// <summary>
        /// 우현은 정본 구획 둘(서버/통신실 <c>x -17~-13</c> · 수경재배 <c>x +10~+16</c>)이
        /// 물고 있으므로 셋으로 갈리고, 그 자리는 <b>정확히 그 둘의 발자국</b>이다.
        /// 맞닿음만 보는 계산이면 이 시험이 통과하지만, 띠를 안 보면 <c>0.5m</c> 떨어져 선 방
        /// 뒤에 못 쓰는 굵은 선이 남는다 — 다음 시험이 그것을 건다.
        /// </summary>
        [Test]
        public void StarboardHullFaceIsOneUnbrokenRunNow()
        {
            // <b>M-2 가 이 검사의 부호를 뒤집었다.</b> 예전에는 서버실·수경재배가 우현 벽에
            // 구워져 있어 그 면이 셋으로 갈렸고, 이 테스트는 "갈린다" 를 지켰다. 둘 다
            // 카탈로그로 이관되면서 우현 벽은 통짜 한 구간이 됐다.
            //
            // 그게 맵 개편 §4.1-3 이 적은 "이관이 자유면을 몇 배로 늘린다" 의 실체다 —
            // 선체 바깥 둘레가 거의 전부 붙일 수 있는 면이 된다.
            LastShiftFreeFaces.Collect(LastShiftCompartments.Specs, faces);

            var starboard = faces
                .Where(face => face.OwnerIndex == LastShiftFreeFaces.HullOwner &&
                               face.Face == LastShiftModuleFace.MaxZ)
                .OrderBy(face => face.SpanMin)
                .ToArray();

            Assert.That(starboard.Length, Is.EqualTo(1), "우현 선체 면이 아직 갈려 있다");
            Assert.That(starboard[0].SpanMin,
                Is.EqualTo(-LastShiftShipDimensions.HalfLength).Within(Tolerance));
            Assert.That(starboard[0].SpanMax,
                Is.EqualTo(LastShiftShipDimensions.HalfLength).Within(Tolerance));
        }

        /// <summary>
        /// <b>맞닿음만 빼면 안 된다.</b> 벽에서 <c>0.5m</c> 띄워 선 방 뒤에는 아무것도 못
        /// 들어가는데, 그 면은 어느 것과도 맞닿아 있지 않다. 자유면이 <c>1.6m</c> 깊이 띠를
        /// 보는 이유가 이것이고, 안 보면 화면이 "붙일 수 있다" 고 적은 자리에서 확정이 겹침으로
        /// 물린다.
        /// </summary>
        [Test]
        public void GapTooThinToBuildInIsNotAFreeFace()
        {
            var hull = LastShiftShipDimensions.HalfWidth;
            var table = new[]
            {
                Box(0, -4f, 4f, hull, hull + 4f),
                // 좌우로 넉넉히 덮어 앞 방의 우현 면 전체를 0.5m 앞에서 막는다.
                Box(1, -8f, 8f, hull + 4.5f, hull + 8f)
            };

            LastShiftFreeFaces.Collect(table, faces);

            var behind = faces.Any(face =>
                face.OwnerIndex == 0 && face.Face == LastShiftModuleFace.MaxZ);

            Assert.That(behind, Is.False, "1.6m 도 안 남은 틈이 자유면으로 나왔다");
        }

        /// <summary>
        /// 선체 안쪽을 향한 면은 자유면이 아니다. 숙소의 선수 면(<c>x = +19</c>)은 선체
        /// 선미 끝벽과 통째로 맞닿아 있다 — 폭이 선체 내폭과 같은 <c>6m</c> 라 자투리조차
        /// 안 남는다(조항 S-2).
        /// </summary>
        [Test]
        public void FaceLookingIntoTheHullIsNotFree()
        {
            LastShiftFreeFaces.Collect(LastShiftCompartments.Specs, faces);

            var inward = faces.Any(face =>
                face.OwnerIndex == (int)LastShiftCompartment.Quarters &&
                face.Face == LastShiftModuleFace.MinX);

            Assert.That(inward, Is.False, "선체를 파고드는 면이 자유면으로 나왔다");
        }

        // ── 판정기와의 대조 ─────────────────────────────────────────────────

        /// <summary>
        /// <b>이 하나가 도면과 판정기가 같은 배를 보고 있다는 증거다.</b> 자유면 하나를 골라
        /// 그 위에 실제 카탈로그 모듈을 대면, 커서가 그 자리를 <see cref="LastShiftPlacementCursor.CanCommit"/>
        /// 로 받아야 한다. 자유면이 판정을 못 통과하는 자리를 굵게 그리면 플레이어는 화면이
        /// 시키는 대로 눌러 놓고 거부만 본다.
        ///
        /// 선체 좌현 면을 쓴다 — 자유면 중 가장 길고, 이탈·사슬 깊이가 선체 직결이라 <c>1</c> 이다.
        /// </summary>
        [Test]
        public void ModulePlacedOnAFreeFacePassesTheVerdict()
        {
            LastShiftFreeFaces.Collect(LastShiftCompartments.Specs, faces);

            var face = faces.Single(item =>
                item.OwnerIndex == LastShiftFreeFaces.HullOwner &&
                item.Face == LastShiftModuleFace.MinZ);

            var kind = LastShiftModuleCatalog.At(LastShiftModuleCatalog.Observatory);
            var cursor = new LastShiftPlacementCursor();
            cursor.Select(LastShiftModuleCatalog.Observatory);
            // 문이 MinX 면에 있는 발자국을 z 면에 대려면 90° 돌린다. 문 면이 MinZ 가 되는
            // 회전을 고르는 것이 아니라, 붙일 벽의 바깥쪽(z 가 작아지는 쪽)으로 방을 내는 것이다.
            cursor.Rotate(1);
            cursor.MoveAnchorTo(new Vector3(
                face.SpanMin + 1f,
                0f,
                face.PlaneCoordinate - cursor.Kind.Footprint.Rotated(1).WidthZ));

            Assert.That(cursor.Candidate.DoorPlaneCoordinate, Is.EqualTo(face.PlaneCoordinate).Within(Tolerance),
                "문이 자유면 평면에 안 얹혔다");
            Assert.That(face.Accepts(cursor.Candidate.DoorCenter), Is.True, "문 중심이 자유면 밖이다");
            Assert.That(cursor.CanCommit, Is.True,
                $"{kind.Name} — {LastShiftPlacementUi.Reason(cursor.Verdict, cursor.Faults)}");
        }

        /// <summary>
        /// 모듈을 하나 확정하면 그 자리를 덮던 자유면이 줄고 새 모듈의 바깥 면이 생긴다.
        /// <b>구간 수가 아니라 "그 자리가 사라졌는가" 를 건다</b> — 구간 수는 방이 면 한가운데
        /// 붙으면 오히려 는다.
        /// </summary>
        [Test]
        public void PlacingAModuleConsumesTheFaceItAteAndOpensItsOwn()
        {
            LastShiftFreeFaces.Collect(LastShiftCompartments.Specs, faces);
            var before = faces.Single(item =>
                item.OwnerIndex == LastShiftFreeFaces.HullOwner &&
                item.Face == LastShiftModuleFace.MinZ);

            var footprint = LastShiftModuleCatalog.At(LastShiftModuleCatalog.Observatory).Footprint.Rotated(1);
            var cursor = new LastShiftPlacementCursor();
            cursor.Select(LastShiftModuleCatalog.Observatory);
            cursor.Rotate(1);
            cursor.MoveAnchorTo(new Vector3(
                before.SpanMin + 1f, 0f, before.PlaneCoordinate - footprint.WidthZ));

            Assert.That(cursor.TryCommit(out var index, out var verdict), Is.True,
                LastShiftPlacementUi.Reason(verdict, cursor.Faults));

            LastShiftFreeFaces.Collect(LastShiftCompartments.Specs, faces);

            var covered = cursor.Candidate;
            var stillFree = faces.Any(item =>
                item.OwnerIndex == LastShiftFreeFaces.HullOwner &&
                item.Face == LastShiftModuleFace.MinZ &&
                item.SpanMin < covered.MaxX - Tolerance &&
                item.SpanMax > covered.MinX + Tolerance);

            Assert.That(stillFree, Is.False, "모듈이 덮은 구간이 아직 자유면이다");
            Assert.That(faces.Any(item => item.OwnerIndex == index), Is.True,
                "새로 붙인 모듈에 자유면이 하나도 안 생겼다");
        }

        // ── 도면 투영 ───────────────────────────────────────────────────────

        /// <summary>
        /// 월드 → 화면 → 월드 왕복이 제자리로 온다. <b>도면 위 클릭이 곧 배치 좌표이므로</b>
        /// 이 왕복이 어긋나면 손가락이 짚은 자리와 방이 서는 자리가 갈린다.
        /// </summary>
        [Test]
        public void SchematicRoundTripsWorldCoordinates()
        {
            var schematic = new LastShiftHullSchematic(new Rect(120f, 40f, 900f, 500f));

            foreach (var world in new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(-19f, 0f, 3f),
                new Vector3(27f, 0f, -8f),
                new Vector3(-41f, 0f, 19f)
            })
            {
                var back = schematic.ToWorld(schematic.ToScreen(world));
                Assert.That(back.x, Is.EqualTo(world.x).Within(Tolerance));
                Assert.That(back.z, Is.EqualTo(world.z).Within(Tolerance));
            }
        }

        /// <summary>
        /// 배율이 하나다. <c>5×5</c> 정비창이 도면에서 정사각형으로 안 보이면 "두 자리를 나란히
        /// 대 본다"(§4.1-1)가 눈으로 성립하지 않는다.
        /// </summary>
        [Test]
        public void SchematicKeepsSquareFootprintsSquare()
        {
            var schematic = new LastShiftHullSchematic(new Rect(0f, 0f, 1280f, 400f));
            var rect = schematic.ToScreenRect(-2.5f, 2.5f, -2.5f, 2.5f);

            Assert.That(rect.width, Is.EqualTo(rect.height).Within(Tolerance));
        }

        /// <summary>
        /// 선수가 왼쪽, 선미가 오른쪽, 우현이 위다(§4.2 그림). 축이 뒤집히면 도면과 씬을 눈으로
        /// 대조할 수 없다.
        /// </summary>
        [Test]
        public void SchematicPutsBowLeftAndStarboardUp()
        {
            var schematic = new LastShiftHullSchematic(new Rect(0f, 0f, 800f, 400f));

            var bow = schematic.ToScreen(-LastShiftShipDimensions.HalfLength, 0f);
            var stern = schematic.ToScreen(LastShiftShipDimensions.HalfLength, 0f);
            var starboard = schematic.ToScreen(0f, LastShiftShipDimensions.HalfWidth);
            var port = schematic.ToScreen(0f, -LastShiftShipDimensions.HalfWidth);

            Assert.That(bow.x, Is.LessThan(stern.x), "선수가 왼쪽이 아니다");
            Assert.That(starboard.y, Is.LessThan(port.y), "우현이 위가 아니다");
        }

        // ── 도구 ────────────────────────────────────────────────────────────

        /// <summary>문 규약을 안 보는 판 하나. 자유면 계산은 발자국만 보므로 문 값은 아무래도 좋다.</summary>
        private static LastShiftCompartmentSpec Box(
            int index, float minX, float maxX, float minZ, float maxZ) => new(
            index, minX, maxX, minZ, maxZ,
            LastShiftDoorPlane.AlongX, minX, (minZ + maxZ) * 0.5f,
            -1, LastShiftCompartmentAccess.Open);
    }
}
