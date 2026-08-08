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
                "에어록을 뺀 11 개다(§17.5). 개수가 바뀌면 표와 enum 중 하나가 뒤처진 것이다.");
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
            // 맞닿는 면은 겹침이 아니다. 사슬(화물칸-정비창-관측실, 화장실-숙소-휴게실-구명정)은
            // 언제나 한 면을 공유하므로 닫힌 구간 비교를 쓰면 전부 FAIL 한다.
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
                    AssertDoorMeetsHull(spec);
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

        private static void AssertDoorMeetsHull(LastShiftCompartmentSpec spec)
        {
            var plane = spec.DoorPlane == LastShiftDoorPlane.AlongX
                ? LastShiftShipDimensions.HalfLength
                : LastShiftShipDimensions.HalfWidth;
            Assert.That(Mathf.Abs(Mathf.Abs(spec.DoorPlaneCoordinate) - plane), Is.LessThan(Tolerance),
                $"{spec.Compartment} 는 선체에 직접 붙는데 문 평면이 선체 내면과 다르다.");

            // 선체에 붙는 문은 선체 내부 범위 안에서 열려야 한다. 끝벽 밖이나 긴 벽 밖으로
            // 나가면 씬 빌더가 판을 못 뚫고, 뚫어도 방이 아니라 솔리드 쪽으로 열린다.
            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
            var (freeMin, freeMax) = spec.DoorPlane == LastShiftDoorPlane.AlongX
                ? (-LastShiftShipDimensions.HalfWidth, LastShiftShipDimensions.HalfWidth)
                : (-LastShiftShipDimensions.HalfLength, LastShiftShipDimensions.HalfLength);
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

            // 선체에 직접 붙는 것이 정확히 넷이어야 한다 — 화물칸(선수 끝벽), 서버실·수경재배
            // (우현 긴 벽), 생활공간 진입로(선미 끝벽). §17.3 도해가 그리는 그림이 이것이다.
            Assert.That(LastShiftCompartments.FixedSpecs.Count(LastShiftCompartments.ConnectsToHull),
                Is.EqualTo(4));
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
        public void LivingBlockAndTheBowChainAreOpenWhileTheOtherThreeStayLocked()
        {
            // §15.2 는 아홉 개를 언락 대상으로 두었고, 그 중 구명정만 "공간은 열려 있고 기능만
            // 잠긴다"(§15.4). 생활공간 셋(§9)은 애초에 언락 목록에 없다.
            //
            // 선수 사슬 넷은 확장 검토 §2 로 P0 초기값이 Open 이다 — 언락 순서(§15.2)를
            // 지우는 것이 아니라 "P0 씬 = 언락이 끝난 뒤의 배" 로 정의하는 것이라, 이 넷이
            // 다시 Locked 로 돌아가는 것은 메타 진행 백본이 붙을 때다(§2.3).
            var open = LastShiftCompartments.FixedSpecs
                .Where(spec => spec.Access == LastShiftCompartmentAccess.Open)
                .Select(spec => spec.Compartment)
                .ToArray();
            Assert.That(open, Is.EquivalentTo(new[]
            {
                LastShiftCompartment.Lavatory,
                LastShiftCompartment.Quarters,
                LastShiftCompartment.Lounge,
                LastShiftCompartment.CargoBay,
                LastShiftCompartment.Hangar,
                LastShiftCompartment.Workshop,
                LastShiftCompartment.Observatory
            }), "생활공간 셋(§9) + 선수 사슬 넷(확장 검토 §2)이다.");

            // 안 여는 셋. 확장 검토 §2.2 가 각각 "정보 우위 접근 비용"·"새 시간 축"·
            // "두 번째 개인 상태 축" 이 전제라 P0 밖이라고 판정했다 — 이 셋이 같이 열리면
            // 그 판정이 코드에서 조용히 뒤집힌다.
            var locked = LastShiftCompartments.FixedSpecs
                .Where(spec => spec.Access == LastShiftCompartmentAccess.Locked)
                .Select(spec => spec.Compartment)
                .ToArray();
            Assert.That(locked, Is.EquivalentTo(new[]
            {
                LastShiftCompartment.ServerRoom,
                LastShiftCompartment.Hydroponics,
                LastShiftCompartment.MedBay
            }), "P0 에서 안 여는 것은 서버/통신실·수경재배·의무실 셋이다(확장 검토 §2.2).");

            Assert.That(LastShiftCompartments.Of(LastShiftCompartment.EscapePod).Access,
                Is.EqualTo(LastShiftCompartmentAccess.SpaceOpenFunctionLocked),
                "구명정을 Locked 로 묶으면 §15.4 가 기각한 '그동안 탈출 수단도 없이 다녔다'가 된다.");

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
        public void CompartmentCoordinatesFollowTheHullInsteadOfLiterals()
        {
            // §17.4 표는 전장 38m(반전장 19) 기준인데 선체는 아직 36m 다(§2.2 개정 미반영).
            // 표 숫자를 박아 두면 개정이 들어오는 순간 열한 개가 통째로 1m 어긋난다.
            // 여기서는 "선체에 붙어 있는가" 만 본다 — 붙어 있으면 전장이 바뀌어도 따라온다.
            var bow = -LastShiftShipDimensions.HalfLength;
            var stern = LastShiftShipDimensions.HalfLength;

            Assert.That(LastShiftCompartments.Of(LastShiftCompartment.CargoBay).MaxX,
                Is.EqualTo(bow).Within(Tolerance), "화물칸은 조종석 선수 끝벽에 붙는다.");
            Assert.That(LastShiftCompartments.Of(LastShiftCompartment.Lavatory).MinX,
                Is.EqualTo(stern).Within(Tolerance), "생활공간은 산소실 선미 끝벽에 붙는다.");
            Assert.That(LastShiftCompartments.Of(LastShiftCompartment.ServerRoom).DoorCenter,
                Is.EqualTo(LastShiftShipDimensions.CockpitCenterX).Within(Tolerance),
                "서버/통신실 문은 조종석 방 중심이다(§17.4 의 x=-15).");

            // 표가 확정한 치수도 같이 고정한다 — 붙는 자리만 맞고 크기가 어긋나면
            // §17.7-1 이 허용한 "art/tech 실측 조정" 과 구분이 안 된다.
            AssertFootprint(LastShiftCompartment.Observatory, 3f, 4f);
            AssertFootprint(LastShiftCompartment.Workshop, 5f, 5f);
            AssertFootprint(LastShiftCompartment.CargoBay, 8f, 8f);
            AssertFootprint(LastShiftCompartment.Hangar, 8f, 10f);
            AssertFootprint(LastShiftCompartment.ServerRoom, 4f, 6f);
            AssertFootprint(LastShiftCompartment.MedBay, 5f, 5f);
            AssertFootprint(LastShiftCompartment.EscapePod, 4f, 4f);
            AssertFootprint(LastShiftCompartment.Lavatory, 2f, LastShiftShipDimensions.InteriorWidth);
            AssertFootprint(LastShiftCompartment.Quarters, 4f, LastShiftShipDimensions.InteriorWidth);
            AssertFootprint(LastShiftCompartment.Lounge, 4f, LastShiftShipDimensions.InteriorWidth);

            // 수경재배만 §17.4 표의 치수(`6×5`)와 범위(`+10~+16` / `+3~+9` = `6×6`)가 서로 다르다.
            // 범위 쪽을 따른다 — 표 두 칸 중 문·인접 관계를 실제로 정하는 것은 범위다.
            AssertFootprint(LastShiftCompartment.Hydroponics, 6f, 6f);
        }

        private static void AssertFootprint(LastShiftCompartment compartment, float lengthX, float widthZ)
        {
            var spec = LastShiftCompartments.Of(compartment);
            Assert.That(spec.LengthX, Is.EqualTo(lengthX).Within(Tolerance), $"{compartment} L");
            Assert.That(spec.WidthZ, Is.EqualTo(widthZ).Within(Tolerance), $"{compartment} W");
        }
    }
}
