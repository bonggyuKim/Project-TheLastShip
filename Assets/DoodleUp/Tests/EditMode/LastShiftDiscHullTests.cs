using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 원반 외피(§26·§27.2)와 상부 회랑(§25.4(B)·§27.4)의 기하 조건을 고정한다.
    /// §27.7-2("상부 회랑 정밀 곡선·분기문 좌표, 겹침 재검증 — <c>game-tech-director</c>")가
    /// 이 파일이 답하는 자리다.
    ///
    /// 씬이 아니라 좌표표를 검사하는 이유는 <see cref="LastShiftCompartmentLayoutTests"/> 와
    /// 같다 — 회랑 좌표는 구획 표에서, 구획 표는 선체 전장에서 파생하므로 전장이 움직이면
    /// 전부 따라 움직인다. 그때 무엇이 깨졌는지는 씬을 다시 굽기 전에 알아야 한다.
    /// </summary>
    public sealed class LastShiftDiscHullTests
    {
        private const float Tolerance = 0.0001f;

        // ── 외피 타원 ────────────────────────────────────────────────────────

        [Test]
        public void ShellMatchesTheApprovedDisc()
        {
            // 중앙 광장 허브 §9.2 가 확정한 값이다. 반지름은 취향이 아니라 네 항의 합이라
            // 리터럴이 아니라 정본을 본다 — 발자국이 한 번만 움직여도 이 값이 따라와야 한다.
            Assert.That(LastShiftHullShell.Radius, Is.EqualTo(LastShiftPlazaLayout.HullRadius).Within(Tolerance));
            Assert.That(LastShiftHullShell.Radius, Is.EqualTo(19f).Within(Tolerance));
            Assert.That(LastShiftHullShell.OverallLength, Is.EqualTo(38f).Within(Tolerance));
            Assert.That(LastShiftHullShell.OverallWidth, Is.EqualTo(38f).Within(Tolerance));

            // §26.4 가 정원을 기각했던 근거는 스파인의 종횡비 6.33:1 이었고, 허브가
            // 그것을 1.22:1 로 뒤집으면서 장축을 따로 둘 이유가 사라졌다(§0-8).
            Assert.That(LastShiftHullShell.AspectRatio, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void EveryCompartmentFitsInsideTheShell()
        {
            foreach (var spec in LastShiftCompartments.FixedSpecs)
            {
                Assert.That(LastShiftHullShell.ContainsFootprint(spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ),
                    Is.True, $"{spec.Compartment} 의 모서리가 타원 밖이다 — 방이 껍질을 뚫는다.");

                // 이상적인 타원만으로는 부족하다. 씬에 서는 것은 내접 다각형이라 경계에
                // 아슬아슬하게 붙은 발자국은 이상 검사를 통과하고도 실제 판에 잘린다.
                Assert.That(LastShiftHullShell.InscribedContainsFootprint(
                        spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ), Is.True,
                    $"{spec.Compartment} 가 내접 다각형 밖이다 — 타원은 통과해도 판이 파고든다.");
            }
        }

        [Test]
        public void TheDiscIsNowMostlyEmptyAndTheTightestCornerIsTheAirlockHall()
        {
            // §27.2 는 타원 치수를 격납고 모서리(-27, +14)에서 뽑았고 여유가 10% 였다.
            // <b>그 방이 배에서 나갔다.</b> 남은 고정 부속 둘 중 원반에 가장 가까운 것은
            // 에어록 홀이고 그 모서리가 (-11, -12) — 중앙 광장 허브 §9.2 가 반지름 19m 를
            // 뽑을 때 쓴 바로 그 최원 모서리다. 실측 여유 <c>0.266</c>(반지름 제곱비 기준,
            // 원점에서 16.28m / 19m).
            //
            // <b>타원을 다시 잡지 않는다.</b> 원반 크기는 자유 배치가 쓸 자리이고(맵 개편
            // §3.4 "원반 밖" 판정), 배가 비었다고 껍질을 줄이면 플레이어가 지을 자리를
            // 같이 줄인다. 이 검사가 지키는 것은 "빈 것이 의도다" 하나다.
            var worst = LastShiftCompartments.FixedSpecs
                .OrderBy(spec => LastShiftHullShell.FootprintMargin(spec))
                .First();
            Assert.That(worst.Compartment, Is.EqualTo(LastShiftCompartment.AirlockHall));

            Assert.That(LastShiftHullShell.FootprintMargin(worst), Is.EqualTo(0.266f).Within(0.005f),
                "에어록 홀 모서리 여유가 움직였다 — 발자국이나 원반 반지름이 바뀌었다.");
        }

        [Test]
        public void ChordApproximationReadsAsACurve()
        {
            // 판 하나의 새그가 판 두께보다 크면 테두리가 곡선이 아니라 다각형으로 보인다.
            Assert.That(LastShiftHullShell.MaxChordSag,
                Is.LessThan(LastShiftHullShell.PanelThickness),
                $"세그먼트 {LastShiftHullShell.SegmentCount} 장으로는 테두리가 각져 보인다.");
        }

        [Test]
        public void ShellDoesNotTouchThePressureZones()
        {
            // §26.5: 원반은 껍질이고 압력 구역 넷을 안 바꾼다.
            //
            // <b>내부 치수를 리터럴로 안 잰다.</b> 예전에는 CT-09 의 <c>38 x 6.0</c> 을 그대로
            // 박아 두었는데, 방사형에서 그 두 수는 배를 덮는 사각형이 아니라 <b>경계 상자</b>가
            // 됐다. 껍질이 안 건드린다는 것을 재려면 발자국이 그대로인지를 물어야 하고,
            // 그건 발자국표가 답한다.
            Assert.That(LastShiftShipDimensions.InteriorLength,
                Is.EqualTo(LastShiftPlazaLayout.MaxX - LastShiftPlazaLayout.MinX).Within(Tolerance));
            Assert.That(LastShiftShipDimensions.InteriorWidth,
                Is.GreaterThanOrEqualTo(LastShiftPlazaLayout.MaxZ - LastShiftPlazaLayout.MinZ),
                "경계 상자가 발자국보다 좁다 — 에어록 홀이 배 밖으로 판정된다.");
            Assert.That(LastShiftZoneAtlas.ZoneCount, Is.EqualTo(4));

            // 압력 경계는 여전히 셋이다. 일자 스파인에서는 사슬이라 셋이었고 방사형에서는
            // 광장 변의 압력문이 셋이라 같은 값이 나온다 — 위상은 바뀌었지만 문 개수가
            // 안 움직여서 LastShiftZoneDoor 인스턴스 수도 그대로다.
            Assert.That(LastShiftZoneAtlas.BoundaryCount, Is.EqualTo(3));
        }

        // ── 자투리 구조체(격벽 프레임) ───────────────────────────────────────

        [Test]
        public void FramesActuallyFillTheGap()
        {
            // §27.3 이 요구한 "자투리를 비-게임플레이 구조체로 채운다". 하나도 안 서면
            // 여유(Clearance)나 최소 길이가 너무 커서 조건이 조용히 전부 걸러진 것이다.
            Assert.That(LastShiftHullFrames.BuildableRibCount, Is.GreaterThanOrEqualTo(8),
                "격벽 프레임이 거의 안 선다 — 자투리가 안 채워진 채로 통과한다.");
            Assert.That(LastShiftHullFrames.BuildableRingSegmentCount, Is.GreaterThanOrEqualTo(8),
                "거들 링이 거의 안 선다.");

            // 전부 서는 것도 이상하다. 방이 외피에 가까운 각(격납고 어깨)과 창 앞(좌현)에서는
            // 안 서는 것이 정상이라, 24/24 면 걸러내는 조건 자체가 안 도는 것이다.
            Assert.That(LastShiftHullFrames.BuildableRibCount,
                Is.LessThan(LastShiftHullFrames.RibCount),
                "모든 각에 프레임이 선다 — 방·창 회피가 안 돌고 있다.");
        }

        [Test]
        public void NoFrameTouchesARoomACorridorOrTheHull()
        {
            for (var rib = 0; rib < LastShiftHullFrames.RibCount; rib++)
            {
                if (!LastShiftHullFrames.RibIsBuildable(rib)) continue;
                AssertMemberIsFree($"Rib_{rib:00}",
                    LastShiftHullFrames.RibInner(rib), LastShiftHullFrames.RibOuter(rib));
            }

            for (var segment = 0; segment < LastShiftHullShell.SegmentCount; segment++)
            {
                if (!LastShiftHullFrames.RingSegmentIsBuildable(segment)) continue;
                AssertMemberIsFree($"Girth_{segment:00}",
                    LastShiftHullFrames.RingSegmentStart(segment),
                    LastShiftHullFrames.RingSegmentStart((segment + 1) % LastShiftHullShell.SegmentCount));
            }
        }

        [Test]
        public void NoFrameStandsInFrontOfThePortWindows()
        {
            // 이 배의 창 너머는 진짜 우주가 아니라 배경막이다. 그 앞에 부재를 세우면
            // 창에서 회색 보가 우주에 떠 있는 것으로 보인다.
            //
            // 배경막은 <b>원반 외피 바깥</b>에 서야 한다(§28.6 art 결정 (a)). 안쪽에 두면
            // 껍질에 갇혀서 창에 우주가 아니라 테두리 판이 보인다 — 예전 -9.1 이 그 상태였다.
            Assert.That(LastShiftHullFrames.WindowBackdropZ,
                Is.LessThan(-LastShiftHullShell.SemiMinorZ),
                "배경막이 원반 단축 반지름 안쪽에 있다 — 외피에 가려 창에서 안 보인다.");
            Assert.That(LastShiftHullFrames.WindowBackdropZ,
                Is.EqualTo(-LastShiftHullShell.Radius - 2f).Within(0.001f),
                "배경막 z 가 씬 빌더의 SpaceVoid 와 어긋났다 — 두 값이 갈리면 회피가 헛돈다.");

            // 별 판도 원반 밖이어야 한다. 배경막과 달리 별은 배경막 <b>앞</b>으로 흩뿌려지므로
            // 상한을 따로 건다 — 이게 없으면 좌현 테두리 유리(§29.4-(1)) 앞에 별이 뜬다.
            Assert.That(LastShiftHullFrames.WindowStarNearestZ,
                Is.LessThan(-LastShiftHullShell.SemiMinorZ),
                "별 판 상한이 원반 안쪽이다 — 테두리 창 앞에 별이 떠 있는 것으로 보인다.");
            Assert.That(LastShiftHullFrames.WindowStarNearestZ,
                Is.GreaterThan(LastShiftHullFrames.WindowBackdropZ),
                "별 판 상한이 배경막보다 뒤다 — 별이 배경막에 가려 하나도 안 보인다.");

            for (var rib = 0; rib < LastShiftHullFrames.RibCount; rib++)
            {
                if (!LastShiftHullFrames.RibIsBuildable(rib)) continue;
                foreach (var point in Samples(LastShiftHullFrames.RibInner(rib), LastShiftHullFrames.RibOuter(rib)))
                    Assert.That(LastShiftHullFrames.IsWindowKeepOut(point.x, point.y), Is.False,
                        $"Rib_{rib:00} 이 좌현 창 앞을 지난다.");
            }
        }

        /// <summary>
        /// §29.6 판정기준 1 — <b>테두리 판이 전부 선다.</b> 예전에는 좌현 창 구간
        /// <c>10</c>장을 통째로 비웠고(그래서 §29.3 이 잰 실루엣은 <c>38/48</c>), 그 자리가
        /// 원반 전장 <c>84m</c> 중 <c>50m</c> 짜리 노치였다.
        ///
        /// 이 검사가 세그먼트 번호를 안 박는 이유는 <c>SegmentCount</c> 가 바뀌면 번호가
        /// 통째로 밀리기 때문이다 — 세는 것은 "판이 서는가/창 판인가" 둘뿐이다.
        /// </summary>
        [Test]
        public void DiscRimStandsAllTheWayAround()
        {
            var bays = LastShiftHullFrames.WindowBaySegmentCount;

            Assert.That(bays, Is.GreaterThan(0),
                "창 판이 하나도 없다 — 테두리가 닫히기만 하고 창이 사라졌다.");
            Assert.That(bays, Is.LessThan(LastShiftHullShell.SegmentCount / 2),
                "창 판이 좌현 절반을 넘게 먹었다 — 테두리가 유리 띠로 읽힌다.");

            // 나머지는 불투명 판이다. 둘을 더해 SegmentCount 가 되어야 "48장 전부 선다".
            //
            // <b>선수 창이 이 셈에서 빠졌다.</b> 관측실이 카탈로그로 이관되면서(맵 개편 §3.2)
            // 그 창을 보는 자리가 배에서 없어졌고, 선수 테두리는 다시 통짜 불투명 판이다.
            var opaque = 0;
            for (var segment = 0; segment < LastShiftHullShell.SegmentCount; segment++)
                if (!LastShiftHullFrames.SegmentIsWindowBay(segment)) opaque++;

            Assert.That(opaque + bays,
                Is.EqualTo(LastShiftHullShell.SegmentCount),
                "테두리 판 수가 세그먼트 수와 다르다 — 실루엣에 구멍이 남았다.");
        }

        /// <summary>
        /// 창 판이 <b>끊기지 않은 호 하나</b>이고, 멀리언이 그 호의 이음매마다 선다.
        /// 창 판이 두 덩어리로 갈리면 그 사이에 불투명 판이 한 장 끼어 조종석에서 보는
        /// 별 띠가 중간에 잘린다. 멀리언은 세그먼트가 아니라 이음매라 <c>n+1</c> 개다 —
        /// 양 끝 멀리언이 유리와 불투명 판의 경계를 마감한다(아트 정본 §3.3).
        /// </summary>
        [Test]
        public void WindowBaysFormOneArcWithAMullionAtEverySeam()
        {
            var count = LastShiftHullShell.SegmentCount;
            var runs = 0;
            for (var segment = 0; segment < count; segment++)
            {
                var previous = LastShiftHullFrames.SegmentIsWindowBay((segment + count - 1) % count);
                if (LastShiftHullFrames.SegmentIsWindowBay(segment) && !previous) runs++;
            }

            Assert.That(runs, Is.EqualTo(1), "창 판이 여러 덩어리로 갈렸다 — 별 띠가 중간에 잘린다.");

            var seams = LastShiftHullFrames.WindowMullionSeams();
            Assert.That(seams.Length, Is.EqualTo(LastShiftHullFrames.WindowBaySegmentCount + 1),
                "멀리언 수가 이음매 수와 다르다 — 유리와 불투명 판의 경계가 안 마감된다.");
            Assert.That(seams.Distinct().Count(), Is.EqualTo(seams.Length),
                "같은 이음매에 멀리언이 두 번 선다.");

            foreach (var seam in seams)
            {
                var point = LastShiftHullShell.SegmentStart(seam);
                Assert.That(LastShiftHullShell.NormalizedRadiusSquared(point.x, point.y),
                    Is.EqualTo(1f).Within(0.001f),
                    $"Mullion_{seam:00} 이 테두리 타원 위가 아니다 — 이음매에서 벗어났다.");
            }
        }

        /// <summary>
        /// §29.6 판정기준 2 의 전제 — 창을 테두리로 옮겨도 <b>발자국은 안 변한다.</b>
        /// 창 판·멀리언은 테두리 위에 서고 방 발자국에 관여하지 않는다. 좌현 점유율을 실제로
        /// 올리기로 했던 관측 회랑은 폐지됐고 그 역할은 중앙 광장이 승계한다
        /// (docs/bow-cockpit-central-plaza-layout-v1.md §166) — 여기서 점유율이 흔들리면
        /// 테두리 쪽이 발자국을 건드린 것이다.
        /// </summary>
        [Test]
        public void MovingTheWindowsToTheRimDoesNotChangeAnyFootprint()
        {
            foreach (var segment in Enumerable.Range(0, LastShiftHullShell.SegmentCount))
            {
                if (!LastShiftHullFrames.SegmentIsWindowBay(segment)) continue;
                var start = LastShiftHullShell.SegmentStart(segment);
                var end = LastShiftHullShell.SegmentStart((segment + 1) % LastShiftHullShell.SegmentCount);
                var middle = (start + end) * 0.5f;

                foreach (var spec in LastShiftCompartments.FixedSpecs)
                    Assert.That(
                        middle.x >= spec.MinX && middle.x <= spec.MaxX &&
                        middle.y >= spec.MinZ && middle.y <= spec.MaxZ, Is.False,
                        $"창 판 {segment:00} 이 구획 {spec.Compartment} 발자국 안에 있다.");

            }
        }

        private static void AssertMemberIsFree(string name, Vector2 from, Vector2 to)
        {
            foreach (var point in Samples(from, to))
                Assert.That(LastShiftHullFrames.IsFree(point.x, point.y), Is.True,
                    $"{name} 이 ({point.x:0.##}, {point.y:0.##}) 에서 방·선체와 겹친다.");
        }

        /// <summary>부재 위를 촘촘히 훑는다. 양 끝만 보면 방을 가로지르는 부재가 통과한다.</summary>
        private static Vector2[] Samples(Vector2 from, Vector2 to)
        {
            var count = Mathf.Max(8, Mathf.CeilToInt(Vector2.Distance(from, to) / 0.25f));
            var result = new Vector2[count + 1];
            for (var index = 0; index <= count; index++)
                result[index] = Vector2.Lerp(from, to, (float)index / count);
            return result;
        }

    }
}
