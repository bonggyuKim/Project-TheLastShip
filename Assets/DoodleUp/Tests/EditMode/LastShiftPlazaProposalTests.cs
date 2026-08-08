using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 선수 조종석 + 중앙 광장 도안(<c>docs/bow-cockpit-central-plaza-layout-v1.md</c>)의 미결
    /// §7-2(원반 파라미터 확정 + 내접·겹침 정밀 검증)와 §7-4(<c>T3</c>/<c>T4</c>/<c>T5</c> 재실행)를
    /// 여기서 닫는다. 대상 좌표는 <see cref="LastShiftPlazaProposal"/> 이고 정본은 안 건드린다.
    ///
    /// <b>재실행이 지금 성립하는 이유.</b> <c>T4</c>/<c>T5</c> 가 쓰는 것은 개구부·배플·게이지
    /// 좌표뿐이고 도안이 그 넷을 하나도 안 옮긴다(도안 §2.3). 그래서 이 둘은 <b>표본 공간만
    /// 통로 A(<c>6 x 3.6</c>)에서 광장(<c>6 x 18</c>)으로 넓힌</b> 같은 검사이며, 정본
    /// <see cref="LastShiftSightlineProbe"/> 를 그대로 태운다. <c>T3</c> 는 거리 상수만 보므로
    /// 광장 치수와 직접 견준다.
    /// </summary>
    public sealed class LastShiftPlazaProposalTests
    {
        private const float Tolerance = 0.0001f;

        // ── §7-2 원반 파라미터 ───────────────────────────────────────────────

        [Test]
        public void ConfirmedDiscKeepsTheDraftLengthAndOnlyWidensTheBeam()
        {
            // 사용자에게 설명된 값(전장 48m, 선수 -17, 선미 +31)이 안 흔들리는 것이 조건이다 —
            // 도안 §7-7(c) 가 그 셋을 확인 항목으로 올려 두었다. 움직인 것은 폭 하나다.
            Assert.That(LastShiftPlazaProposal.SemiMajorX, Is.EqualTo(24f).Within(Tolerance));
            Assert.That(LastShiftPlazaProposal.CenterX, Is.EqualTo(7f).Within(Tolerance));
            Assert.That(LastShiftPlazaProposal.BowX, Is.EqualTo(-17f).Within(Tolerance));
            Assert.That(LastShiftPlazaProposal.SternX, Is.EqualTo(31f).Within(Tolerance));
            Assert.That(LastShiftPlazaProposal.OverallLength, Is.EqualTo(48f).Within(Tolerance));

            // 폭은 도안 §6-5 가 지정한 보상 구간(20 → 22~24)의 하한이다.
            Assert.That(LastShiftPlazaProposal.SemiMinorZ, Is.EqualTo(22f).Within(Tolerance));
            Assert.That(LastShiftPlazaProposal.OverallWidth, Is.EqualTo(44f).Within(Tolerance));

            // 종횡비가 1 을 향해 내려간 것이 요점이다. 현재 선체는 2.1:1 이고 §26.4 가 정원을
            // 기각한 근거가 그 값이었는데, 도안은 그 진단 자체를 뒤집는 쪽으로 간다.
            Assert.That(LastShiftPlazaProposal.AspectRatio, Is.EqualTo(1.0909f).Within(0.001f));
            Assert.That(LastShiftPlazaProposal.AspectRatio,
                Is.LessThan(LastShiftHullShell.AspectRatio));
        }

        [Test]
        public void EveryFootprintClearsTheRimPanelByBothPanelThicknesses()
        {
            // 통과 여부만 보면 파라미터를 못 정한다. 어느 구획이 몇 미터 남겼는지를 같이 찍는다.
            var rows = LastShiftPlazaProposal.Footprints
                .Where(footprint => !footprint.Protrudes)
                .OrderBy(footprint => LastShiftPlazaProposal.InscribedMargin(footprint))
                .ToArray();

            foreach (var footprint in rows)
            {
                var radius = LastShiftPlazaProposal.WorstCornerRadiusSquared(footprint);
                var margin = LastShiftPlazaProposal.InscribedMargin(footprint);
                Assert.That(radius, Is.LessThanOrEqualTo(1f),
                    $"{footprint.Name} 의 모서리가 타원 밖이다(r^2 {radius:F4}).");
                Assert.That(margin, Is.GreaterThanOrEqualTo(LastShiftPlazaProposal.MinInscribedClearance),
                    $"{footprint.Name} 의 내접 여유가 {margin:F3}m 다 — 구획 판과 테두리 판이 서로를 파고든다.");
            }

            var worst = rows[0];
            Debug.Log($"[LAST_SHIFT_PLAZA_INSCRIBE] a={LastShiftPlazaProposal.SemiMajorX} " +
                      $"b={LastShiftPlazaProposal.SemiMinorZ} cx={LastShiftPlazaProposal.CenterX} " +
                      $"tightest={worst.Name} margin={LastShiftPlazaProposal.InscribedMargin(worst):F3}m " +
                      $"r2={LastShiftPlazaProposal.WorstCornerRadiusSquared(worst):F4} result=PASS");
        }

        [Test]
        public void TheDraftBeamWouldHavePushedTheMedBayOutsideTheDisc()
        {
            // b 를 왜 옮겼는지가 여기 남는다. 초안 b=20 에서 의무실은 타원 <b>밖</b>이었고,
            // 정비창·격납고는 안이긴 해도 여유가 판 두께보다 얇았다 — 셋 다 씬에서 깨진다.
            var medBay = Find("의무실");
            var draftRadius = DraftRadiusSquared(medBay);
            Assert.That(draftRadius, Is.GreaterThan(1f),
                "초안 b=20 에서 의무실이 타원 안이면 b 를 옮긴 근거가 사라진 것이다.");

            foreach (var name in new[] { "정비창", "격납고" })
            {
                var draftMargin = LastShiftPlazaProposal.InscribedMargin(
                    Find(name), LastShiftPlazaProposal.DraftSemiMinorZ);
                Assert.That(draftMargin, Is.LessThan(LastShiftCompartments.PanelThickness),
                    $"초안 b=20 에서 {name} 여유가 판 두께를 넘었다면 그 구획을 근거로 못 쓴다.");
            }

            // 그리고 확정값에서는 셋 다 요구선을 넘는다.
            foreach (var name in new[] { "의무실", "정비창", "격납고" })
                Assert.That(LastShiftPlazaProposal.InscribedMargin(Find(name)),
                    Is.GreaterThanOrEqualTo(LastShiftPlazaProposal.MinInscribedClearance));

            Debug.Log($"[LAST_SHIFT_PLAZA_DRAFT] draft b={LastShiftPlazaProposal.DraftSemiMinorZ} " +
                      $"medBay r2={draftRadius:F4} (>1 = 밖) result=REJECTED");
        }

        [Test]
        public void NoTwoFootprintsOverlap()
        {
            // §21.1 이 구획 55 쌍에 한 것과 같은 검사다. 광장이 통로 A 를 흡수했으므로 스파인도
            // 같은 표에 넣고 함께 센다 — 광장이 전력실 쪽으로 1m 만 새도 여기서 걸린다.
            var specs = LastShiftPlazaProposal.Footprints;
            var pairs = 0;
            for (var a = 0; a < specs.Length; a++)
            for (var b = a + 1; b < specs.Length; b++)
            {
                pairs++;
                Assert.That(LastShiftPlazaProposal.Overlap(specs[a], specs[b]), Is.False,
                    $"{specs[a].Name} 와 {specs[b].Name} 가 겹친다.");
            }

            Assert.That(pairs, Is.EqualTo(specs.Length * (specs.Length - 1) / 2));
            Debug.Log($"[LAST_SHIFT_PLAZA_OVERLAP] footprints={specs.Length} pairs={pairs} result=PASS");
        }

        [Test]
        public void ThePlazaStaysInsideTheCockpitZoneSpan()
        {
            // 도안 §4 불변식 1. 광장이 x > -5 로 넘어가면 압력문을 안 거치는 구역 간 경로가
            // 생기고, 그 순간 §25.3 이 기각한 허브가 된다. 경계는 구역 정본에서 뽑는다.
            Assert.That(LastShiftPlazaProposal.PlazaMaxX,
                Is.LessThanOrEqualTo(LastShiftZoneAtlas.CockpitMaxX + Tolerance),
                "광장이 조종석 구역 밖으로 나갔다 — 불변식 1 위반이다.");
            Assert.That(LastShiftPlazaProposal.PlazaMinX,
                Is.EqualTo(LastShiftShipDimensions.RoomMaxX(LastShiftZone.Cockpit)).Within(Tolerance),
                "광장 선수벽이 조종석 방 선미벽과 안 맞는다 — 개구부 0 이 벽 한가운데에 온다.");
            Assert.That(LastShiftPlazaProposal.PlazaMaxX,
                Is.EqualTo(LastShiftShipDimensions.PassageMaxX(0)).Within(Tolerance),
                "광장이 통로 A 의 x 구간을 그대로 흡수하지 않았다 — 개구부 1 이 어긋난다.");

            // 개구부 0·1 과 배플 A 가 전부 광장 안(또는 그 경계면)에 든다. 하나라도 밖이면
            // "좌표를 안 옮긴다" 는 도안의 전제가 거짓이다.
            foreach (var opening in new[] { 0, 1 })
            {
                Assert.That(LastShiftShipDimensions.OpeningX(opening),
                    Is.InRange(LastShiftPlazaProposal.PlazaMinX - Tolerance,
                        LastShiftPlazaProposal.PlazaMaxX + Tolerance));
                Assert.That(LastShiftShipDimensions.OpeningMinZ(opening),
                    Is.GreaterThan(LastShiftPlazaProposal.PlazaMinZ));
                Assert.That(LastShiftShipDimensions.OpeningMaxZ(opening),
                    Is.LessThan(LastShiftPlazaProposal.PlazaMaxZ));
            }

            Assert.That(LastShiftShipDimensions.BaffleCenterX(0),
                Is.InRange(LastShiftPlazaProposal.PlazaMinX, LastShiftPlazaProposal.PlazaMaxX));
        }

        [Test]
        public void OnlyTheCockpitPokesOutOfTheBow()
        {
            // 팰콘 콕핏+목은 조형이 아니라 이 수치의 결과다(도안 §2.1). 돌출이 사라지면 조종석이
            // 원반 안에 묻혀 "선수 최선단" 이 화면에서 안 읽히고, 너무 길면 목이 관이 된다.
            var cockpit = Find("조종석 방");
            var entry = LastShiftPlazaProposal.BowEntryX(cockpit.MaxZ);
            var protrusion = entry - cockpit.MinX;

            Assert.That(protrusion, Is.InRange(1.5f, 3f),
                $"조종석 돌출이 {protrusion:F2}m 다 — 도안 §2.1 이 잡은 2.27m 대역에서 벗어났다.");
            Assert.That(LastShiftPlazaProposal.BowX,
                Is.InRange(cockpit.MinX, cockpit.MaxX),
                "원반 선수가 조종석 방 x 범위 밖이다 — 돌출이 아니라 방 하나가 통째로 떨어져 나갔다.");

            // 돌출은 조종석 하나뿐이다. 다른 것이 같이 나가면 그건 도안이 아니라 사고다.
            foreach (var footprint in LastShiftPlazaProposal.Footprints)
                Assert.That(footprint.Protrudes,
                    Is.EqualTo(footprint.Name == "조종석 방"));

            Debug.Log($"[LAST_SHIFT_PLAZA_BOW] entryX={entry:F2} protrusion={protrusion:F2}m result=PASS");
        }

        [Test]
        public void ChordApproximationStillReadsAsACurve()
        {
            // 판 하나의 새그가 판 두께를 넘으면 테두리가 곡선이 아니라 다각형으로 보인다.
            // 원반이 짧아지면서 새그는 줄어드는 쪽이지만, 세그먼트 수를 줄이려는 다음 사람이
            // 근사식이 아니라 실제 수치를 보게 둔다.
            var sag = LastShiftPlazaProposal.MaxChordSag();
            Assert.That(sag, Is.LessThan(LastShiftHullShell.PanelThickness),
                $"세그먼트 {LastShiftPlazaProposal.SegmentCount} 장으로는 테두리가 각져 보인다({sag:F4}).");
            Assert.That(sag, Is.LessThan(LastShiftHullShell.MaxChordSag),
                "원반이 작아졌는데 새그가 늘었다 — 세그먼트 수가 같은지 확인해야 한다.");
        }

        // ── §5 판정 기준 재측정 ──────────────────────────────────────────────

        [Test]
        public void TheDiscIsActuallyFilledNow()
        {
            // 도안 §5 는 전부 "초안 좌표 기준 추정" 이라고 적고 확정 좌표에서 다시 재라고 넘겼다.
            // 여기가 그 자리다. 추정과 실측이 갈리는 항목이 있으면 그 숫자를 문서에 되돌린다.
            var discArea = Mathf.PI * LastShiftPlazaProposal.SemiMajorX * LastShiftPlazaProposal.SemiMinorZ;
            var total = LastShiftPlazaProposal.Footprints.Sum(footprint => footprint.Area);
            var port = LastShiftPlazaProposal.Footprints.Sum(footprint => footprint.PortArea);
            var portShare = port / (discArea * 0.5f);

            Assert.That(portShare, Is.GreaterThanOrEqualTo(0.30f),
                $"§29.6-2 좌현 점유율이 {portShare:P1} 다 — 기준 30% 미달이라 원반 좌현이 다시 빈다.");

            var open = LastShiftPlazaProposal.Footprints.Where(footprint => footprint.OpenInP0).ToArray();
            var openArea = open.Sum(footprint => footprint.Area);
            var wideArea = open.Where(footprint => footprint.WidthZ >= 4f).Sum(footprint => footprint.Area);
            var wideShare = wideArea / openArea;
            var widest = open.Max(footprint => footprint.WidthZ);

            Assert.That(wideShare, Is.GreaterThanOrEqualTo(0.60f),
                $"확장 검토 §1.4-3a 가 {wideShare:P1} 다 — 열린 발자국의 절반이 아직 관이다.");
            Assert.That(widest, Is.GreaterThanOrEqualTo(8f),
                $"확장 검토 §1.4-3b 가 {widest:F0}m 다 — 가장 넓은 방이 여전히 좁다.");

            var minX = LastShiftPlazaProposal.Footprints.Min(footprint => footprint.MinX);
            var maxX = LastShiftPlazaProposal.Footprints.Max(footprint => footprint.MaxX);
            var minZ = LastShiftPlazaProposal.Footprints.Min(footprint => footprint.MinZ);
            var maxZ = LastShiftPlazaProposal.Footprints.Max(footprint => footprint.MaxZ);
            var aspect = (maxX - minX) / (maxZ - minZ);
            var centreZ = (minZ + maxZ) * 0.5f;

            Assert.That(aspect, Is.LessThanOrEqualTo(2.5f),
                $"§29.6-3 띠 종횡비가 {aspect:F2}:1 이다.");
            Assert.That(Mathf.Abs(centreZ), Is.LessThanOrEqualTo(2.5f),
                $"§29.6-3 z 중심이 {centreZ:+0.0;-0.0} 다 — 발자국이 한쪽 현으로 쏠렸다.");

            Debug.Log($"[LAST_SHIFT_PLAZA_METRICS] disc={discArea:F0}m2 total={total:F0}m2 " +
                      $"occupancy={total / discArea:P1} port={portShare:P1} " +
                      $"aabb=x[{minX},{maxX}] z[{minZ},{maxZ}] aspect={aspect:F2}:1 centreZ={centreZ:+0.0;-0.0} " +
                      $"wide4m={wideShare:P1} widest={widest:F0}m result=PASS");
        }

        // ── §7-4 T4 재실행 ───────────────────────────────────────────────────

        [Test]
        public void NoPointInThePlazaReadsMoreThanTwoZones()
        {
            // T4. 표본 공간이 통로 A(6 x 3.6)에서 광장(6 x 18)으로 넓어진다. 재는 자는 정본
            // 그대로이므로 바뀐 것은 어디에 서 보는가뿐이다.
            var worst = 0;
            var worstAt = Vector2.zero;
            var samples = 0;
            foreach (var eye in PlazaGrid())
            {
                samples++;
                var count = LastShiftSightlineProbe.SimultaneousZones(eye, out _);
                if (count <= worst) continue;
                worst = count;
                worstAt = eye;
            }

            Assert.That(samples, Is.GreaterThan(0), "광장 격자가 비었다.");
            Assert.That(worst, Is.LessThanOrEqualTo(2),
                $"광장 ({worstAt.x:F1}, {worstAt.y:F1}) 에서 {worst} 구역이 동시에 읽힌다 — SIMUL_ZONES 위반이다.");

            // 그리고 <b>2 여야 한다</b>. 1 이면 광장에서 전력실 게이지가 아예 안 읽히는 것이고,
            // 그러면 §3.1 이 지킨다고 한 판독 구조가 통로 벽과 함께 사라진 것이다.
            Assert.That(worst, Is.EqualTo(2),
                "광장 어디에서도 2 구역이 안 읽힌다 — 개구부 1 게이지가 광장에서 죽었다.");

            Debug.Log($"[LAST_SHIFT_PLAZA_SIMUL] samples={samples} worst={worst} " +
                      $"worstAt=({worstAt.x:F1},{worstAt.y:F1}) result=PASS");
        }

        // ── §7-4 T5 재실행 ───────────────────────────────────────────────────

        [Test]
        public void ThePlazaWidensTheReadableBandWithoutLosingTheBaffleShadow()
        {
            // T5. 통로 A 시절 판독 띠는 통행 차선 1.04m 였다. 광장에서 그 띠가 얼마나 되는지,
            // 그리고 <b>배플 그림자가 남는지</b>를 같이 잰다 — 띠만 재면 배플을 지워도 통과한다.
            const int gauge = 1;
            var readable = 0;
            var samples = 0;
            var bandMin = float.MaxValue;
            var bandMax = float.MinValue;
            var farthest = 0f;

            foreach (var eye in PlazaGrid())
            {
                samples++;
                if (!LastShiftSightlineProbe.GaugeReadableFrom(eye, gauge)) continue;
                readable++;
                bandMin = Mathf.Min(bandMin, eye.y);
                bandMax = Mathf.Max(bandMax, eye.y);
                farthest = Mathf.Max(farthest, Vector2.Distance(eye,
                    new Vector2(LastShiftShipDimensions.OpeningX(gauge),
                        LastShiftShipDimensions.OpeningCenterZ(gauge))));
            }

            Assert.That(readable, Is.GreaterThan(0), "광장 어디에서도 게이지가 전폭으로 안 읽힌다.");

            // 통로 A 의 통행 차선은 광장 안에도 그대로 있다. 거기서 안 읽히면 회귀다.
            var laneZ = LastShiftShipDimensions.BaffleFreeStripCenterZ(0);
            Assert.That(LastShiftSightlineProbe.GaugeReadableFrom(
                    new Vector2(LastShiftPlazaProposal.PlazaMinX + LastShiftShipPhysics.CrewRadius, laneZ), gauge),
                Is.True, "통행 차선에서 게이지가 안 읽힌다 — 통로 A 시절보다 나빠졌다.");

            // 배플 그림자. 배플 z 구간 정후방에서는 여전히 안 읽혀야 A3 격리가 관측 가능하다.
            var behind = new Vector2(
                LastShiftPlazaProposal.PlazaMinX + LastShiftShipPhysics.CrewRadius,
                LastShiftShipDimensions.BaffleCenterZ(0));
            Assert.That(LastShiftSightlineProbe.GaugeReadableFrom(behind, gauge), Is.False,
                "광장에서 배플 뒤가 뚫렸다 — 벽이 사라지면서 차단까지 같이 사라졌다.");
            Assert.That(readable, Is.LessThan(samples),
                "광장 전 지점에서 읽힌다 — 배플 그림자가 표본에 하나도 안 잡혔다.");

            // 판독 3단은 거리 규칙이므로 띠가 넓어져도 등급/수치 구분은 유지된다. 다만 이 도안이
            // 실제로 넓히는 폭을 숫자로 남긴다 — §6-1(판독 상황 이원화)의 근거 자료다.
            Debug.Log($"[LAST_SHIFT_PLAZA_GAUGE] opening={gauge} samples={samples} readable={readable} " +
                      $"({(float)readable / samples:P1}) band=z[{bandMin:F2},{bandMax:F2}] " +
                      $"bandWidth={bandMax - bandMin:F2} laneWidth={LastShiftShipDimensions.BaffleFreeStrip:F2} " +
                      $"farthest={farthest:F2}m result=PASS");
        }

        // ── §7-4 T3 재실행 ───────────────────────────────────────────────────

        [Test]
        public void BreathStillStopsAtThePlazaAftWallButNoLongerCrossesThePlazaItself()
        {
            // T3 는 차폐가 아니라 거리 상수만 본다(LastShiftZoneAudio 주석). 광장이 벽을 없앤 것은
            // 감쇠에 영향이 없고, 바뀌는 것은 <b>같은 공간의 크기</b>다.
            var breath = LastShiftZoneAudio.BreathMaxDistance;
            var powerCentre = new Vector2(LastShiftShipDimensions.PowerCenterX, 0f);

            // (1) 지켜지는 것 — 광장 선미벽(x = -11) 어느 z 에 서도 그 호흡이 전력실 방 중앙까지
            //     안 닿는다. 통로 A 시절 이 성질은 한 점(반대쪽 끝)에서만 쟀는데, 광장에서는
            //     벽 전체가 대상이므로 최단 거리로 재야 한다.
            var nearestOnAftWall = Mathf.Abs(powerCentre.x - LastShiftPlazaProposal.PlazaMinX);
            Assert.That(nearestOnAftWall, Is.GreaterThan(breath),
                "광장 선미벽의 호흡이 전력실 방에 닿는다 — 통로가 주던 방향 판단이 사라진다.");

            // (2) 새로 생기는 것 — 광장은 자기 안에서도 끝에서 끝이 안 들린다. 광장이 배에서
            //     가장 넓은 단일 공간(z 18m)인데 호흡 반경은 끝방 길이(8m)에 묶여 있다.
            var diagonal = Mathf.Sqrt(
                LastShiftPlazaProposal.PlazaLengthX * LastShiftPlazaProposal.PlazaLengthX +
                LastShiftPlazaProposal.PlazaWidthZ * LastShiftPlazaProposal.PlazaWidthZ);
            Assert.That(diagonal, Is.GreaterThan(breath),
                "광장 대각이 호흡 반경 안이면 아래 상충이 없는 것이므로 이 검사를 지워야 한다.");

            // (3) 그리고 이건 상수로 못 푼다. 광장을 덮으려면 z 폭(18)을 넘겨야 하는데, 그 값은
            //     (1) 의 한계(8.5)를 이미 넘는다 — 덮는 순간 전력실로 새어 들어간다.
            //     BreathMaxDistance 를 키우는 것은 답이 아니고, 답은 기획/밸런스 쪽에 있다.
            Assert.That(LastShiftPlazaProposal.PlazaWidthZ, Is.GreaterThan(nearestOnAftWall),
                "광장 폭이 선미벽 거리보다 좁으면 상충이 없다 — 그러면 §7-4 의 T3 항목이 닫힌다.");

            Debug.Log($"[LAST_SHIFT_PLAZA_AUDIO] breath={breath:F1}m plazaDiagonal={diagonal:F2}m " +
                      $"shortfall={diagonal - breath:F2}m aftWallToPowerCentre={nearestOnAftWall:F2}m " +
                      $"impact={LastShiftZoneAudio.ImpactMaxDistance:F1}m result=FINDING");
        }

        /// <summary>
        /// 광장 안에서 사람이 실제로 설 수 있는 격자. 벽에서 몸 반지름만큼 물러선다 —
        /// <see cref="LastShiftGaugeReadabilityTests"/> 의 <c>IsStandable</c> 과 같은 규약이다.
        /// </summary>
        private static System.Collections.Generic.IEnumerable<Vector2> PlazaGrid()
        {
            var r = LastShiftShipPhysics.CrewRadius;
            for (var x = LastShiftPlazaProposal.PlazaMinX + r;
                 x <= LastShiftPlazaProposal.PlazaMaxX - r + Tolerance; x += 0.1f)
            for (var z = LastShiftPlazaProposal.PlazaMinZ + r;
                 z <= LastShiftPlazaProposal.PlazaMaxZ - r + Tolerance; z += 0.1f)
                yield return new Vector2(x, z);
        }

        private static LastShiftPlazaProposal.Footprint Find(string name) =>
            LastShiftPlazaProposal.Footprints.First(footprint => footprint.Name == name);

        private static float DraftRadiusSquared(in LastShiftPlazaProposal.Footprint footprint)
        {
            var worst = 0f;
            foreach (var x in new[] { footprint.MinX, footprint.MaxX })
            foreach (var z in new[] { footprint.MinZ, footprint.MaxZ })
            {
                var nx = (x - LastShiftPlazaProposal.CenterX) / LastShiftPlazaProposal.SemiMajorX;
                var nz = z / LastShiftPlazaProposal.DraftSemiMinorZ;
                worst = Mathf.Max(worst, nx * nx + nz * nz);
            }
            return worst;
        }
    }
}
