using System;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 그레이박스 구획 배치(<c>docs/corridor-4p-redesign-v1.md</c> §17.4)의 기하 조건을 고정한다.
    ///
    /// §17.8 미결`3`("분기 방 간 지오메트리 겹침 정밀 검증 — game-tech-director")이 이 파일이
    /// 답하는 자리다. 기획이 표에서 손으로 맞춰 둔 x/z 범위는 문 폭까지 반영한 검증이 아니라고
    /// 스스로 적어 두었고(§17.7-2), 실제로 겹치면 씬에서는 두 방이 한 벽을 공유한 것처럼 보이다가
    /// 승무원이 벽을 통과한다.
    ///
    /// 씬이 아니라 좌표표를 검사한다. 씬 검증기(Editor 어셈블리)는 빌드된 씬을 봐야 돌지만,
    /// 구획 좌표는 선체 전장에서 파생하므로 §2.2 의 `36 → 38` 개정이 들어오는 순간 전부 움직인다 —
    /// 그때 무엇이 깨졌는지는 씬을 다시 굽기 전에 알아야 한다.
    /// </summary>
    public sealed class LastShiftCompartmentLayoutTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void EveryCompartmentValueHasASpec()
        {
            var values = Enum.GetValues(typeof(LastShiftCompartment)).Cast<LastShiftCompartment>().ToArray();
            Assert.That(values.Length, Is.EqualTo(LastShiftCompartments.FixedCount),
                "중앙 광장 허브 이후 고정 표는 부속 둘이다. 개수가 바뀌면 표와 enum 중 하나가 뒤처진 것이다.");
            foreach (var value in values)
                Assert.That(LastShiftCompartments.Of(value).Compartment, Is.EqualTo(value),
                    $"{value} 의 spec 이 자기 자신을 안 가리킨다 — 표 index 가 어긋났다.");
        }

        [Test]
        public void EveryCompartmentHasPositiveExtentAndTheUniformHeight()
        {
            // §17.4 "전 항목 높이 3m 균일". 높이는 spec 이 아니라 상수라 여기서 한 번만 본다.
            Assert.That(LastShiftCompartments.InteriorHeight, Is.EqualTo(3f).Within(Tolerance));
            Assert.That(LastShiftCompartments.InteriorHeight,
                Is.GreaterThan(LastShiftZoneDoor.OpeningHeight),
                "문 구멍이 구획 천장보다 높으면 인방 두께가 음수가 되어 벽이 뒤집힌다.");

            foreach (var spec in LastShiftCompartments.FixedSpecs)
            {
                Assert.That(spec.LengthX, Is.GreaterThan(0f), $"{spec.Compartment} x 범위가 뒤집혔다.");
                Assert.That(spec.WidthZ, Is.GreaterThan(0f), $"{spec.Compartment} z 범위가 뒤집혔다.");
                Assert.That(Mathf.Min(spec.LengthX, spec.WidthZ),
                    Is.GreaterThanOrEqualTo(LastShiftZoneDoor.OpeningWidth),
                    $"{spec.Compartment} 가 문 폭보다 좁다 — 문을 달면 방이 문에 먹힌다.");
            }
        }

        [Test]
        public void NoTwoCompartmentsOverlap()
        {
            // 맞닿는 면은 겹침이 아니다. 사슬로 이어 붙인 방은 언제나 한 면을 공유하므로
            // 닫힌 구간 비교를 쓰면 전부 FAIL 한다. 고정 표가 하나로 줄어 지금은 쌍이 없지만,
            // 이 검사가 실제로 도는 자리는 여기가 아니라 배치된 모듈 쪽 판정기다.
            var specs = LastShiftCompartments.FixedSpecs;
            for (var a = 0; a < specs.Length; a++)
            for (var b = a + 1; b < specs.Length; b++)
                Assert.That(LastShiftCompartments.VolumesOverlap(specs[a], specs[b]), Is.False,
                    $"{specs[a].Compartment} 와 {specs[b].Compartment} 의 볼륨이 겹친다 — §17.8 미결 3 이 걱정한 자리다.");
        }

        [Test]
        public void NoCompartmentEatsIntoTheHullInterior()
        {
            // 선체 안쪽은 방·통로가 이미 빈틈없이 타일링한 영역이다(LastShiftBulkheadCoverageTests).
            // 구획이 거기 파고들면 승무원이 서는 자리에 벽이 생기거나, 압력 구역 안에 압력 없는
            // 공간이 들어앉아 §17.6 이 미결로 남긴 편입 문제를 코드가 먼저 결정해 버린다.
            foreach (var spec in LastShiftCompartments.FixedSpecs)
                Assert.That(LastShiftCompartments.OverlapsHullInterior(spec), Is.False,
                    $"{spec.Compartment} 가 선체 내부를 침범한다.");
        }

        [Test]
        public void EveryDoorSitsOnItsOwnBoundaryFace()
        {
            foreach (var spec in LastShiftCompartments.FixedSpecs)
                Assert.That(LastShiftCompartments.DoorSitsOnOwnBoundary(spec), Is.True,
                    $"{spec.Compartment} 의 문이 자기 경계면 위에 없거나 폭이 면 밖으로 넘친다.");
        }

        [Test]
        public void EveryDoorAlsoSitsOnTheFaceItConnectsTo()
        {
            // 문이 자기 면 위에 있는 것만으로는 부족하다. 상대 쪽 면과 같은 평면이 아니면
            // 씬에서는 두 방 사이에 0.x m 짜리 솔리드가 남아 "문은 보이는데 안 통하는" 상태가 된다.
            foreach (var spec in LastShiftCompartments.FixedSpecs)
            {
                if (LastShiftCompartments.ConnectsToHull(spec))
                {
                    AssertDoorMeetsThePlaza(spec);
                    continue;
                }

                var parent = LastShiftCompartments.FixedSpecs[spec.ParentIndex];
                var (parentMin, parentMax) = spec.DoorPlane == LastShiftDoorPlane.AlongX
                    ? (parent.MinX, parent.MaxX)
                    : (parent.MinZ, parent.MaxZ);
                Assert.That(
                    Mathf.Abs(spec.DoorPlaneCoordinate - parentMin) < Tolerance ||
                    Mathf.Abs(spec.DoorPlaneCoordinate - parentMax) < Tolerance, Is.True,
                    $"{spec.Compartment} 의 문 평면이 부모 {parent.Compartment} 의 경계면과 다르다.");

                // 그리고 문 구멍이 부모 면 안에 다 들어가야 한다. 부모가 자기 면을 세우고 거기에
                // 구멍을 뚫으므로(씬 빌더의 면 소유 규칙), 넘치면 모서리에 틈이 남는다.
                var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
                var (freeMin, freeMax) = spec.DoorPlane == LastShiftDoorPlane.AlongX
                    ? (parent.MinZ, parent.MaxZ)
                    : (parent.MinX, parent.MaxX);
                Assert.That(spec.DoorCenter - half, Is.GreaterThanOrEqualTo(freeMin - Tolerance),
                    $"{spec.Compartment} 의 문이 부모 {parent.Compartment} 면 밖으로 넘친다.");
                Assert.That(spec.DoorCenter + half, Is.LessThanOrEqualTo(freeMax + Tolerance),
                    $"{spec.Compartment} 의 문이 부모 {parent.Compartment} 면 밖으로 넘친다.");
            }
        }

        /// <summary>
        /// <b>"부모가 없다" 가 이제 "광장 변에 직결" 이다</b>(중앙 광장 허브 §2.3). 일자
        /// 스파인에서는 그 자리가 선체 내면(<c>HalfLength</c>·<c>HalfWidth</c>)이었는데,
        /// 방사형에서 그 두 값은 배를 덮는 사각형이 아니라 <b>고정 발자국 경계 상자</b>라
        /// 벽이 서 있는 자리가 아니다 — 숙소 문은 <c>z = +6</c>(광장 우현 변)이고 경계 상자
        /// 가장자리 <c>z = +12</c> 와 아무 관계가 없다.
        /// </summary>
        private static void AssertDoorMeetsThePlaza(LastShiftCompartmentSpec spec)
        {
            var (near, far) = spec.DoorPlane == LastShiftDoorPlane.AlongX
                ? (LastShiftPlazaLayout.PlazaMinX, LastShiftPlazaLayout.PlazaMaxX)
                : (LastShiftPlazaLayout.PlazaMinZ, LastShiftPlazaLayout.PlazaMaxZ);

            Assert.That(
                Mathf.Abs(spec.DoorPlaneCoordinate - near) < Tolerance ||
                Mathf.Abs(spec.DoorPlaneCoordinate - far) < Tolerance, Is.True,
                $"{spec.Compartment} 는 광장에 직결인데 문 평면이 광장 변과 다르다.");

            // 광장 변에 나는 문은 그 변 안에서 열려야 한다. 변 밖으로 나가면 씬 빌더가 뚫을
            // 판이 없고, 뚫어도 광장이 아니라 팔 사이 빈 사분면 쪽으로 열린다.
            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
            var (freeMin, freeMax) = spec.DoorPlane == LastShiftDoorPlane.AlongX
                ? (LastShiftPlazaLayout.PlazaMinZ, LastShiftPlazaLayout.PlazaMaxZ)
                : (LastShiftPlazaLayout.PlazaMinX, LastShiftPlazaLayout.PlazaMaxX);
            Assert.That(spec.DoorCenter - half, Is.GreaterThanOrEqualTo(freeMin - Tolerance));
            Assert.That(spec.DoorCenter + half, Is.LessThanOrEqualTo(freeMax + Tolerance));
        }

        [Test]
        public void CompartmentGraphIsATreeRootedAtTheHull()
        {
            // §9.4·§9.5 의 "막다른 방" 전제. 순환이 하나라도 생기면 두 지점을 잇는 경로가 둘이
            // 되고, 그러면 §9.5 가 "기여하지 않는다"고 명시적으로 답한 4인 게이트 대안 경로가
            // 실수로 만들어진다 — 그 순간 RG-1 면제 근거(§9.3)도 같이 사라진다.
            foreach (var spec in LastShiftCompartments.FixedSpecs)
            {
                var depth = LastShiftCompartments.DoorDepth(spec.Compartment);
                Assert.That(depth, Is.GreaterThan(0),
                    $"{spec.Compartment} 의 부모 사슬이 선체에 안 닿는다 — 순환이거나 고아다.");
                Assert.That(depth, Is.LessThanOrEqualTo(LastShiftCompartments.FixedCount));
            }

            // 선체에 직접 붙는 것이 정확히 하나여야 한다 — 숙소(선미 끝벽, 조항 S-2).
            // 예전에는 넷이었고 그 셋(화물칸·서버실·수경재배)이 카탈로그로 이관됐다.
            //
            // <b>이 수가 곧 시작 배의 사슬 깊이 상한이다.</b> 뿌리가 하나뿐이므로 배치 전
            // 배에서 가장 깊은 방이 깊이 1 이고, 그것이 §5.2 의 최악 이탈 재계산을 성립시킨다.
            Assert.That(LastShiftCompartments.FixedSpecs.Count(LastShiftCompartments.ConnectsToHull),
                Is.EqualTo(LastShiftCompartments.FixedCount),
                "부속 중 하나가 다른 구획을 부모로 물었다 — 경유 방이 생겼다.");
        }

        [Test]
        public void NoCompartmentBlocksTheForwardWindows()
        {
            // 이 선체의 좌현(-z)은 벽이 아니라 전장 전체에 걸친 창이다. 거기에 구획을 붙이면
            // 조종석에서 보이는 별이 회색 상자로 막힌다 — §17.4 표가 서버/통신실을 좌현으로
            // 적어 둔 것을 우현으로 뒤집은 이유이고, 다음에 구획이 추가될 때 같은 실수가
            // 조용히 들어오는 것을 막는 자리다.
            //
            // 조건은 "좌현으로 나가지 마라" 가 아니라 <b>창이 실제로 보고 있는 x 구간에서</b>
            // 좌현으로 나가지 마라다. 선수 끝벽 너머(화물칸)는 창 앞이 아니라 창 옆이고,
            // 거기까지 금지하면 §17.4 의 화물칸 폭 `8m` 를 못 세운다.
            var hullMinX = -LastShiftShipDimensions.HalfLength;
            var hullMaxX = LastShiftShipDimensions.HalfLength;
            foreach (var spec in LastShiftCompartments.FixedSpecs)
            {
                var facesTheWindows = spec.MinX < hullMaxX - Tolerance && hullMinX < spec.MaxX - Tolerance;
                if (!facesTheWindows) continue;
                Assert.That(spec.MinZ, Is.GreaterThanOrEqualTo(-LastShiftShipDimensions.HalfWidth - Tolerance),
                    $"{spec.Compartment} 가 창이 보고 있는 x 구간에서 좌현으로 나가 별을 막는다.");
            }
        }

        [Test]
        public void TheOpeningLineIsGoneAndEveryFixedRoomIsOpen()
        {
            // 조항 K-2. 개방 계열이 폐지됐다 — Locked 였던 셋(서버/통신실·수경재배·의무실)이
            // 전부 자유 배치 카탈로그로 갔고, 배치된 모듈은 언제나 Open 으로 선다.
            // 기항 화면의 계열은 이제 복구·보급·배치 셋이다(맵 개편 §3.5).
            var open = LastShiftCompartments.FixedSpecs
                .Where(spec => spec.Access == LastShiftCompartmentAccess.Open)
                .Select(spec => spec.Compartment)
                .ToArray();
            Assert.That(open, Is.EquivalentTo(new[]
                {
                    LastShiftCompartment.Quarters,
                    LastShiftCompartment.AirlockHall
                }),
                "배와 함께 태어나는 부속은 숙소·에어록 홀 둘이고 둘 다 언제나 열려 있다.");

            Assert.That(
                LastShiftCompartments.FixedSpecs.Any(
                    spec => spec.Access == LastShiftCompartmentAccess.Locked),
                Is.False,
                "잠긴 고정 구획이 남았다 — 개방 계열이 폐지됐으므로 그 방을 열 수단이 배에 없다.");

            // Access 값 자체가 둘로 줄었다. 셋째 값(SpaceOpenFunctionLocked)은 구명정 하나만을
            // 위해 있었고 구명정이 제거되면서 enum 에서 빠졌다(맵 개편 §6.2-6) — 값이 다시
            // 늘면 그 방이 무엇인지부터 물어야 한다.
            Assert.That(Enum.GetValues(typeof(LastShiftCompartmentAccess)).Length, Is.EqualTo(2),
                "Access 값이 둘이 아니다 — 구명정 전용 셋째 값이 되살아났는지 본다.");

            // 지나갈 수 있는 구획은 부모도 지나갈 수 있어야 한다. 잠긴 방 너머에 열린 방이
            // 있으면 그 방은 영영 못 들어가는 방이고, 씬에는 도달 불가능한 지오메트리가 남는다.
            foreach (var spec in LastShiftCompartments.FixedSpecs)
            {
                if (!spec.IsPassable || LastShiftCompartments.ConnectsToHull(spec)) continue;
                Assert.That(LastShiftCompartments.FixedSpecs[spec.ParentIndex].IsPassable, Is.True,
                    $"{spec.Compartment} 는 열려 있는데 부모가 잠겨 있다 — 도달 불가능한 공간이다.");
            }
        }

        [Test]
        public void CompartmentCoordinatesFollowThePlazaInsteadOfLiterals()
        {
            // 표 숫자를 박아 두면 배치 개정이 들어오는 순간 방이 통째로 어긋난다.
            // 여기서는 "광장에 붙어 있는가" 만 본다 — 붙어 있으면 발자국이 움직여도 따라온다.
            //
            // <b>부속 둘 다 광장에 직결이다</b>(§2.3). 예전 숭소는 선미 끝벽에 붙었고
            // 에어록 홀은 언더덱 통로에 있었는데, 둘 다 광장 변으로 올라오면서 사슬 깊이가
            // 전부 <c>1</c> 이 됐다 — 그것이 최악 이탈 <c>6.05 → 4.26초</c> 의 실체다.
            foreach (var compartment in new[]
                     { LastShiftCompartment.AirlockHall, LastShiftCompartment.Quarters })
            {
                var spec = LastShiftCompartments.Of(compartment);
                var space = compartment == LastShiftCompartment.AirlockHall
                    ? LastShiftPlazaSpace.AirlockHall
                    : LastShiftPlazaSpace.Quarters;
                var footprint = LastShiftPlazaLayout.Of(space);
                var door = LastShiftPlazaLayout.DoorOf(space);

                Assert.That(spec.MinX, Is.EqualTo(footprint.MinX).Within(Tolerance), $"{compartment} MinX");
                Assert.That(spec.MaxX, Is.EqualTo(footprint.MaxX).Within(Tolerance), $"{compartment} MaxX");
                Assert.That(spec.MinZ, Is.EqualTo(footprint.MinZ).Within(Tolerance), $"{compartment} MinZ");
                Assert.That(spec.MaxZ, Is.EqualTo(footprint.MaxZ).Within(Tolerance), $"{compartment} MaxZ");

                Assert.That(spec.ParentIndex, Is.EqualTo(-1),
                    $"{compartment} 가 다른 구획을 부모로 물고 있다 — 광장 직결이 아니다.");
                Assert.That(spec.DoorPlaneCoordinate, Is.EqualTo(door.Plane).Within(Tolerance));
                Assert.That(spec.DoorCenter, Is.EqualTo(door.Center).Within(Tolerance));
                Assert.That(LastShiftCompartments.DoorDepth(compartment), Is.EqualTo(1),
                    $"{compartment} 사슬 깊이가 1 이 아니다 — 경유 방이 생겼다.");

                // 문이 자기 경계와 광장 변에 동시에 얇혀 있어야 직결이다.
                Assert.That(LastShiftCompartments.DoorSitsOnOwnBoundary(spec), Is.True,
                    $"{compartment} 문이 자기 발자국 경계 위가 아니다.");
            }

            // 발자국은 확정표 그대로다(§2.2). 에어록 홀 8x6, 숭소 6x4.
            AssertFootprint(LastShiftCompartment.AirlockHall, 8f, 6f);
            AssertFootprint(LastShiftCompartment.Quarters, 6f, 4f);
        }

        private static void AssertFootprint(LastShiftCompartment compartment, float lengthX, float widthZ)
        {
            var spec = LastShiftCompartments.Of(compartment);
            Assert.That(spec.LengthX, Is.EqualTo(lengthX).Within(Tolerance), $"{compartment} L");
            Assert.That(spec.WidthZ, Is.EqualTo(widthZ).Within(Tolerance), $"{compartment} W");
        }
    }
}
