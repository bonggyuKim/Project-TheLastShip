using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 관측실에서 밖이 보이는가. 아트 정본
    /// <c>docs/art/last-shift-bow-chain-dressing-v1.md</c> §7-6 이 씬 빌더 몫으로 넘긴 자리다.
    ///
    /// <b>창 하나가 아니라 셋이 한 벌이다</b> — 방 끝벽 개구부, 그 앞 골조 금지, 원반 테두리
    /// 유리와 배경막. 하나만 있으면 전부 헛것이라, 검사도 셋을 따로 재지 않고 <b>시선이
    /// 방에서 배경막까지 실제로 닿는가</b>로 잰다.
    /// </summary>
    public sealed class LastShiftObservatoryWindowTests
    {
        private const float Tolerance = 0.0001f;

        /// <summary>
        /// 개구부가 문이 아니라 창인가. 문턱이 없으면 바닥이 그대로 밖으로 이어지고, 폭이
        /// 방 폭과 같으면 끝벽이 사라진다 — 둘 다 "창을 냈다" 가 아니라 "벽을 없앴다" 다.
        /// </summary>
        [Test]
        public void TheOpeningIsAWindowAndNotAHoleInTheWall()
        {
            var spec = LastShiftCompartments.Of(LastShiftObservatoryWindow.Compartment);

            Assert.That(LastShiftObservatoryWindow.WallX, Is.EqualTo(spec.MinX).Within(Tolerance),
                "창이 관측실 선수 끝벽에 없다.");
            Assert.That(LastShiftObservatoryWindow.SillHeight, Is.GreaterThan(0f),
                "문턱이 없다 — 창이 아니라 바닥까지 뚫린 구멍이다.");
            Assert.That(LastShiftObservatoryWindow.HeadHeight,
                Is.LessThan(LastShiftCompartments.InteriorHeight),
                "윗단이 천장이다 — 인방이 안 남는다.");
            Assert.That(LastShiftObservatoryWindow.HeadHeight,
                Is.GreaterThan(LastShiftObservatoryWindow.SillHeight + 1.2f),
                "창이 눈높이를 못 덮는다.");
            Assert.That(LastShiftObservatoryWindow.OpeningWidth, Is.LessThan(spec.WidthZ - 1f),
                "창 폭이 방 폭에 붙었다 — 끝벽 양쪽에 벽이 안 남는다.");
        }

        /// <summary>
        /// 방에서 창을 지나 테두리까지, 시선이 지나는 자리에 <b>아무것도 없다.</b>
        /// 구획·회랑·골조를 전부 같은 부채꼴로 잰다 — 셋 중 하나만 놓치면 씬에서
        /// "창은 뚫렸는데 회색 판이 보인다" 가 된다.
        /// </summary>
        [Test]
        public void NothingStandsInTheSightCone()
        {
            foreach (var spec in LastShiftCompartments.FixedSpecs)
            {
                if (spec.Compartment == LastShiftObservatoryWindow.Compartment) continue;
                AssertBoxIsOutOfTheCone($"{spec.Compartment}", spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ);
            }

            foreach (var leg in LastShiftUpperGallery.Legs)
                AssertBoxIsOutOfTheCone("UpperGallery", leg.MinX, leg.MaxX, leg.MinZ, leg.MaxZ);

            foreach (var leg in LastShiftObservationGallery.Legs)
                AssertBoxIsOutOfTheCone("ObservationGallery", leg.MinX, leg.MaxX, leg.MinZ, leg.MaxZ);

            for (var rib = 0; rib < LastShiftHullFrames.RibCount; rib++)
            {
                if (!LastShiftHullFrames.RibIsBuildable(rib)) continue;
                AssertSegmentIsOutOfTheCone($"Rib_{rib:00}",
                    LastShiftHullFrames.RibInner(rib), LastShiftHullFrames.RibOuter(rib));
            }

            for (var segment = 0; segment < LastShiftHullShell.SegmentCount; segment++)
            {
                if (!LastShiftHullFrames.RingSegmentIsBuildable(segment)) continue;
                AssertSegmentIsOutOfTheCone($"Girth_{segment:00}",
                    LastShiftHullFrames.RingSegmentStart(segment),
                    LastShiftHullFrames.RingSegmentStart((segment + 1) % LastShiftHullShell.SegmentCount));
            }
        }

        /// <summary>
        /// 테두리가 창이 훑는 만큼 유리인가. <b>방 뒤벽에 선 사람</b>이 개구부 양 끝을 통해
        /// 보는 두 시선이 테두리에 닿는 자리가 기준이다 — 그 자리가 불투명 판이면 창 구석에서
        /// 회색이 보이고, 그건 창을 낸 것이 아니라 창틀만 낸 것이다.
        /// </summary>
        [Test]
        public void TheBowRimIsGlassWhereTheWindowLooks()
        {
            Assert.That(LastShiftObservatoryWindow.BowBaySegmentCount, Is.GreaterThan(0),
                "선수 테두리에 유리가 한 장도 없다 — 관측실은 여전히 껍질 속을 본다.");

            var glassHalfZ = LastShiftObservatoryWindow.GlassHalfZ;
            foreach (var edge in new[] { 1f, -1f })
            {
                var hit = RimHitFromTheBackWall(edge * LastShiftObservatoryWindow.OpeningWidth * 0.5f);
                Assert.That(Mathf.Abs(hit.y), Is.LessThanOrEqualTo(glassHalfZ),
                    $"개구부 끝을 지나는 시선이 유리 밖({hit.y:0.##})에 닿는다 — 창 구석이 회색이다.");
            }

            // 유리가 끊기지 않은 호 하나이고 이음매마다 멀리언이 선다. 좌현과 같은 규칙이다.
            var count = LastShiftHullShell.SegmentCount;
            var runs = 0;
            for (var segment = 0; segment < count; segment++)
            {
                var previous = LastShiftObservatoryWindow.SegmentIsBowBay((segment + count - 1) % count);
                if (LastShiftObservatoryWindow.SegmentIsBowBay(segment) && !previous) runs++;
            }

            Assert.That(runs, Is.EqualTo(1), "선수 유리가 여러 덩어리로 갈렸다.");
            Assert.That(LastShiftObservatoryWindow.BowMullionSeams().Length,
                Is.EqualTo(LastShiftObservatoryWindow.BowBaySegmentCount + 1),
                "멀리언 수가 이음매 수와 다르다.");
        }

        /// <summary>
        /// 선수 유리와 좌현 창은 <b>겹치지 않는 별개의 호</b>다. 한 판정으로 합치면
        /// "좌현 창 판은 끊기지 않은 호 하나" 라는 불변식이 깨지고, 그 불변식은 조종석에서
        /// 보는 별 띠가 중간에 안 잘린다는 뜻이라 유지해야 한다.
        /// </summary>
        [Test]
        public void BowGlassIsSeparateFromThePortWindowArc()
        {
            for (var segment = 0; segment < LastShiftHullShell.SegmentCount; segment++)
                Assert.That(
                    LastShiftObservatoryWindow.SegmentIsBowBay(segment) &&
                    LastShiftHullFrames.SegmentIsWindowBay(segment), Is.False,
                    $"세그먼트 {segment:00} 이 좌현 창이면서 동시에 선수 창이다.");

            Assert.That(
                LastShiftObservatoryWindow.BowBaySegmentCount +
                LastShiftHullFrames.WindowBaySegmentCount,
                Is.LessThan(LastShiftHullShell.SegmentCount / 2),
                "유리가 테두리 절반을 먹었다 — 원반이 실루엣이 아니라 유리 띠로 읽힌다.");
        }

        /// <summary>
        /// 배경막이 원반 <b>밖</b>이고 별 판이 그 앞이되 유리 안쪽으로는 안 넘어오는가.
        /// 좌현이 <c>z</c> 축에서 같은 조건을 걸고 있고, 예전 좌현 배경막이 껍질 속에 갇혀
        /// 있던 것이 이 검사가 생긴 이유다.
        /// </summary>
        [Test]
        public void TheBowBackdropSitsOutsideTheDiscAndBehindTheGlass()
        {
            Assert.That(LastShiftObservatoryWindow.BackdropX,
                Is.LessThan(-LastShiftHullShell.SemiMajorX),
                "선수 배경막이 원반 안이다 — 외피에 가려 창에서 안 보인다.");
            Assert.That(LastShiftObservatoryWindow.StarNearestX,
                Is.LessThan(-LastShiftHullShell.SemiMajorX),
                "별 판 상한이 원반 안쪽이다 — 유리 앞에 별이 떠 있는 것으로 보인다.");
            Assert.That(LastShiftObservatoryWindow.StarNearestX,
                Is.GreaterThan(LastShiftObservatoryWindow.BackdropX),
                "별 판 상한이 배경막보다 뒤다 — 별이 배경막에 가려 하나도 안 보인다.");
            Assert.That(LastShiftObservatoryWindow.BackdropHalfZ,
                Is.GreaterThan(LastShiftObservatoryWindow.GlassHalfZ),
                "배경막이 유리보다 좁다 — 창 구석에서 배경막 가장자리가 보인다.");
        }

        // ── 도구 ─────────────────────────────────────────────────────────────

        /// <summary>방 뒤벽 중앙에서 개구부의 <paramref name="edgeZ"/> 를 지나는 시선이 테두리에 닿는 점.</summary>
        private static Vector2 RimHitFromTheBackWall(float edgeZ)
        {
            var spec = LastShiftCompartments.Of(LastShiftObservatoryWindow.Compartment);
            var eye = new Vector2(spec.MaxX, spec.CenterZ);
            var through = new Vector2(LastShiftObservatoryWindow.WallX, edgeZ);
            var step = (through - eye).normalized * 0.01f;

            var point = through;
            for (var index = 0; index < 4000; index++)
            {
                if (LastShiftHullShell.NormalizedRadiusSquared(point.x, point.y) >= 1f) break;
                point += step;
            }

            return point;
        }

        private static void AssertBoxIsOutOfTheCone(string name, float minX, float maxX, float minZ, float maxZ)
        {
            // 부채꼴은 x 가 작아질수록 넓어지므로 상자에서 가장 깊은 x 만 보면 된다.
            if (minX >= LastShiftObservatoryWindow.WallX) return;
            var halfZ = LastShiftObservatoryWindow.SightHalfZAt(minX);
            Assert.That(minZ > halfZ || maxZ < -halfZ, Is.True,
                $"{name}({minX:0.##}~{maxX:0.##}, {minZ:0.##}~{maxZ:0.##}) 이 관측실 창 앞을 막는다.");
        }

        private static void AssertSegmentIsOutOfTheCone(string name, Vector2 from, Vector2 to)
        {
            var count = Mathf.Max(8, Mathf.CeilToInt(Vector2.Distance(from, to) / 0.25f));
            for (var index = 0; index <= count; index++)
            {
                var point = Vector2.Lerp(from, to, (float)index / count);
                Assert.That(LastShiftObservatoryWindow.IsSightKeepOut(point.x, point.y), Is.False,
                    $"{name} 이 관측실 창 앞을 지난다.");
            }
        }
    }
}
