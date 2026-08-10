using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 중앙 광장 허브 배치(<c>docs/central-plaza-hub-layout-v1.md</c>)의 부록 검산을 코드로 옮긴 것.
    /// 기획이 <c>docs/tools/plaza_hub_check.py</c> 로 낸 값을 <see cref="LastShiftPlazaLayout"/> 위에서
    /// 다시 뽑아 고정한다.
    ///
    /// <b>이 파일이 폐기된 셋을 대체한다</b> — <c>LastShiftPlazaProposal</c>·
    /// <c>LastShiftPlazaProposalTests</c>·<c>LastShiftPlazaRg1Tests</c>. 그 셋은
    /// <c>docs/bow-cockpit-central-plaza-layout-v1.md</c> 의 좌표(광장 <c>x [-11,-5]</c>, 통로 B 존치,
    /// 부속 열둘) 위에 서 있었고, 확정안 §3.4 가 그 체계를 통째로 폐지했다.
    ///
    /// <b>부록이 대신하지 않는다고 적어 둔 넷 중 둘을 여기서 닫는다</b> — 원반 내접(§7 미결 1)과
    /// 정식 <c>RG-1</c> 판정 시간(문 통과 페널티 포함). 나머지 둘(프로브 각크기·씬 실좌표 겹침)은
    /// 씬이 서야 잴 수 있으므로 재빌드 카드에 남는다.
    /// </summary>
    public sealed class LastShiftPlazaLayoutTests
    {
        private const float Tolerance = 0.01f;

        /// <summary>부록 검산의 표본 격자. 기획 산출값을 재현하려면 같은 격자여야 한다.</summary>
        private const float SampleStep = 0.05f;

        // ── 부록 1. 겹침 ─────────────────────────────────────────────────────

        [Test]
        public void SevenStructuresDoNotOverlapInAnyPair()
        {
            var footprints = LastShiftPlazaLayout.Footprints;
            Assert.That(footprints.Length, Is.EqualTo(6),
                "고정 구조물이 일곱이 아니다 — 광장 + 고정 방 여섯이 §2.2 좌표표 전부다.");

            var pairs = 0;
            for (var a = 0; a < footprints.Length; a++)
            for (var b = a + 1; b < footprints.Length; b++)
            {
                pairs++;
                Assert.That(LastShiftPlazaLayout.Overlap(footprints[a], footprints[b]), Is.False,
                    $"{footprints[a].Space} 와 {footprints[b].Space} 가 겹친다. 맞닿는 면은 겹침이 " +
                    "아니므로(열린 구간 비교) 이건 실제로 부피가 겹친 것이다.");
            }

            Assert.That(pairs, Is.EqualTo(15));
        }

        // ── 부록 2·3. 문 여섯 ────────────────────────────────────────────────

        [Test]
        public void EverySixDoorSitsOnItsOwnRoomBoundaryAndOnAPlazaSide()
        {
            // 이 검사가 "경유 방이 하나도 없다"(§0-2)의 좌표 형태다. 문 평면이 자기 방 경계이면서
            // 동시에 광장 변이면 그 방은 광장에 직결한다 — 두 조건 중 하나만 성립하면 사이에
            // 무언가가 낀 것이고, 그러면 "각 방으로 바로" 가 깨진다.
            var plaza = LastShiftPlazaLayout.Of(LastShiftPlazaSpace.Plaza);
            Assert.That(LastShiftPlazaLayout.Doors.Length, Is.EqualTo(5));

            foreach (var door in LastShiftPlazaLayout.Doors)
            {
                var room = LastShiftPlazaLayout.Of(door.Space);
                var onOwn = door.PlaneIsX
                    ? Near(door.Plane, room.MinX) || Near(door.Plane, room.MaxX)
                    : Near(door.Plane, room.MinZ) || Near(door.Plane, room.MaxZ);
                var onPlaza = door.PlaneIsX
                    ? Near(door.Plane, plaza.MinX) || Near(door.Plane, plaza.MaxX)
                    : Near(door.Plane, plaza.MinZ) || Near(door.Plane, plaza.MaxZ);

                Assert.That(onOwn, Is.True, $"{door.Space} 문 평면이 자기 방 경계가 아니다.");
                Assert.That(onPlaza, Is.True, $"{door.Space} 문 평면이 광장 변이 아니다 — 경유 방이 생겼다.");
            }
        }

        [Test]
        public void EveryDoorOpeningFitsInsideBothFaces()
        {
            var plaza = LastShiftPlazaLayout.Of(LastShiftPlazaSpace.Plaza);

            foreach (var door in LastShiftPlazaLayout.Doors)
            {
                var room = LastShiftPlazaLayout.Of(door.Space);
                var (roomLo, roomHi) = door.PlaneIsX ? (room.MinZ, room.MaxZ) : (room.MinX, room.MaxX);
                var (plazaLo, plazaHi) = door.PlaneIsX ? (plaza.MinZ, plaza.MaxZ) : (plaza.MinX, plaza.MaxX);

                Assert.That(door.MinSpan, Is.GreaterThanOrEqualTo(roomLo - Tolerance),
                    $"{door.Space} 문 구멍이 자기 방 면 밖으로 나간다.");
                Assert.That(door.MaxSpan, Is.LessThanOrEqualTo(roomHi + Tolerance));
                Assert.That(door.MinSpan, Is.GreaterThanOrEqualTo(plazaLo - Tolerance),
                    $"{door.Space} 문 구멍이 광장 변 밖으로 나간다.");
                Assert.That(door.MaxSpan, Is.LessThanOrEqualTo(plazaHi + Tolerance));
            }
        }

        [Test]
        public void PressureDoorCountDoesNotMoveFromThree()
        {
            // 조항 S-1. 압력 구역 넷·압력문 셋이 안 바뀌는 것이 이 개편의 전제다 — 여기 걸린
            // 다섯(ZonePressures 배열·SIMUL_ZONES·RG-4 조합·HUD 칸 수·네트워크 스냅샷)이
            // 전부 안 열린다. 통로 A/B 와 개구부 다섯 체계가 폐지돼도 문 수는 그대로다.
            var pressure = 0;
            var openings = 0;
            var plain = 0;
            foreach (var door in LastShiftPlazaLayout.Doors)
                switch (door.Kind)
                {
                    case LastShiftPlazaDoorKind.PressureDoor: pressure++; break;
                    case LastShiftPlazaDoorKind.Opening: openings++; break;
                    default: plain++; break;
                }

            Assert.That(pressure, Is.EqualTo(3), "압력문이 셋이 아니다 — 조항 S-1 이 깨졌다.");
            Assert.That(openings, Is.EqualTo(1), "문 없는 개구부는 조종석↔광장 하나뿐이다(§3.2).");
            Assert.That(plain, Is.EqualTo(1), "비압력 일반문은 숙소 하나다(에어록 홀 폐지).");
            Assert.That(pressure, Is.EqualTo(LastShiftZoneAtlas.BoundaryCount),
                "압력문 수가 구역 경계 수와 다르다 — LastShiftZoneDoor 인스턴스 수가 움직인다.");
        }

        // ── 부록 4. 광장 둘레 자유면 ─────────────────────────────────────────

        [Test]
        public void PlazaPerimeterLeavesSixFreeSpansTotallingEighteenMeters()
        {
            // §5.1 의 산수. 둘레 48m 중 고정 구조물 여섯이 30m 를 먹고 18m 가 남는다.
            // 이 값이 "광장이 확장의 유일한 기점이면 여섯 번에 끝난다" 의 근거다.
            var spans = 0;
            var meters = 0f;
            foreach (LastShiftPlazaLayout.PlazaSide side in
                     System.Enum.GetValues(typeof(LastShiftPlazaLayout.PlazaSide)))
                foreach (var (min, max) in LastShiftPlazaLayout.FreeSpansOn(side))
                {
                    spans++;
                    meters += max - min;
                }

            Assert.That(spans, Is.EqualTo(7), "광장 변 유효 자유면이 7구간이 아니다(에어록 홀 폐지).");
            Assert.That(meters, Is.EqualTo(14f).Within(Tolerance),
                "자유면 합이 18.0m 에서 움직였다 — 고정 구조물이 광장 변을 먹는 길이가 30m 가 아니다.");
            Assert.That(LastShiftPlazaLayout.PlazaPerimeter - meters, Is.EqualTo(34f).Within(Tolerance));
        }

        // ── 부록 5·6. SIMUL_ZONES ────────────────────────────────────────────

        [Test]
        public void TheCoreDrivesSimultaneousReadingsToZeroAndItIsTheSmallestThatDoes()
        {
            // §4.2 의 표를 통째로 재현한다. <b>코어 치수를 아트 판단으로 못 줄이는 이유가
            // 이 다섯 줄이다</b> — 3.2 x 3.2 로 한 눈금만 줄여도 위반이 128점 남는다.
            var expected = new (float Half, int Three)[]
            {
                (0f, 2738), (1.0f, 1138), (1.4f, 0), (1.6f, 0),
                (LastShiftPlazaLayout.CoreHalfExtent, 0)
            };

            foreach (var (half, three) in expected)
            {
                var counts = SampleReadings(half);
                Assert.That(counts[3], Is.EqualTo(three),
                    $"코어 반폭 {half:F1}m 에서 3구역 동시 판독이 {counts[3]}점이다 — §4.2 표의 " +
                    $"{three}점에서 움직였다. 게이지 위치나 문 구멍 폭이 바뀌었다는 뜻이다.");
            }
        }

        [Test]
        public void TheWorstPlazaPointStillReadsTwoZones()
        {
            // 최악이 2 라는 것 자체가 요건이다. 최악이 1 이면 광장에서 게이지가 아예 죽은 것이고,
            // 그러면 "좌현으로 가면 전력실만, 우현으로 가면 냉각실만" 이라는 §8 판정 기준이
            // 성립할 자리가 없다.
            var counts = SampleReadings(LastShiftPlazaLayout.CoreHalfExtent);

            Assert.That(counts[4], Is.EqualTo(51200), "코어 제외 유효 표본이 51,200점이 아니다.");
            Assert.That(counts[3], Is.Zero, "SIMUL_ZONES 위반 — 3구역이 동시에 읽히는 자리가 남았다.");
            Assert.That(counts[2], Is.EqualTo(6400));
            Assert.That(counts[1], Is.EqualTo(13310));
            Assert.That(counts[0], Is.EqualTo(31490));
        }

        [Test]
        public void ThePlazaCentreReadsNothing()
        {
            // §4.3. 광장 한가운데가 정보 특권 지점이 되면 판독 3단의 의도가 죽는다. 코어가
            // 서 있는 자리라 실제로는 설 수도 없지만, 코어를 치워도 안 읽혀야 한다.
            Assert.That(LastShiftPlazaLayout.SimultaneousZoneReadings(0f, 0f), Is.EqualTo(3),
                "광장 정중앙은 세 게이지의 정면이다 — 코어가 그 자리를 점유하는 이유다.");
            Assert.That(LastShiftPlazaLayout.SimultaneousZoneReadings(-5.5f, -5.5f), Is.Zero,
                "광장 좌현 선수 구석에서 게이지가 읽힌다 — 게이지 이설(§4.1)이 풀렸다.");
        }

        // ── 부록 7. RG-1 재래칫 ──────────────────────────────────────────────

        [Test]
        public void WorstEgressIsTheCockpitBowCornerAndItRatchetsDown()
        {
            // <b>이 값이 M-2 후 6.05초에서 또 내려간 것이다</b>(§6.1). 원인은 하나 — 시작 배
            // 전체의 사슬 깊이가 1 이 됐다. 조종석 방 선수 구석 -> 개구부 -> 전력실 문이
            // 유일하게 두 다리를 쓰는 경로이고 그래서 최악이다.
            var worst = LastShiftPlazaLayout.WorstEgressMeters();

            Assert.That(worst, Is.EqualTo(19.26f).Within(Tolerance),
                "배 전체 최악 이탈이 17.03m 에서 움직였다.");
            Assert.That(LastShiftPlazaLayout.EgressWalkSeconds(worst), Is.EqualTo(4.81f).Within(Tolerance),
                "RG-1(1) 개산이 4.26초에서 움직였다 — 한도 10초 대비 여유 2.35배다.");

            // 정식 판정은 압력문 통과 0.8초를 더한다. 개산과 판정을 같은 자리에서 고정해 두지
            // 않으면 §6.1 표(개산)와 가드레일(판정)이 어느 쪽 정의인지가 다음 사람에게 안 보인다.
            Assert.That(LastShiftPlacementRules.EgressSeconds(worst),
                Is.EqualTo(5.61f).Within(Tolerance),
                "문 페널티를 포함한 RG-1(1) 판정값이 5.61초에서 움직였다.");
            Assert.That(LastShiftPlacementRules.EgressSeconds(worst),
                Is.LessThan(LastShiftPlacementRules.TraverseLimitSeconds));
        }

        [Test]
        public void EveryZoneEgressMatchesTheConfirmedTable()
        {
            // §6.1 표를 구역별로 고정한다. 전력실·냉각실이 5.83m 인 것은 표에 없는 값인데,
            // 단칸 구역은 자기 문이 곧 이탈구라 광장을 안 지나기 때문이다 — 그 성질이 깨지면
            // (문이 광장 변에서 떨어지면) 여기가 먼저 걸린다.
            var expected = new (LastShiftZone Zone, float Meters)[]
            {
                (LastShiftZone.Cockpit, 19.26f),
                (LastShiftZone.Power, 8.94f),
                (LastShiftZone.Cooling, 8.94f),
                (LastShiftZone.LifeSupport, 10.77f)
            };

            foreach (var (zone, meters) in expected)
                Assert.That(LastShiftPlazaLayout.WorstEgressMeters(zone), Is.EqualTo(meters).Within(Tolerance),
                    $"{LastShiftZoneAtlas.ShortLabelOf(zone)} 구역 최악 이탈이 {meters:F2}m 에서 움직였다.");
        }

        // ── 부록 8·9. 면적과 AABB ────────────────────────────────────────────

        [Test]
        public void FootprintSumAndOverallAabbMatchTheConfirmedTable()
        {
            var area = 0f;
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            foreach (var footprint in LastShiftPlazaLayout.Footprints)
            {
                area += footprint.Area;
                minX = Mathf.Min(minX, footprint.MinX);
                maxX = Mathf.Max(maxX, footprint.MaxX);
                minZ = Mathf.Min(minZ, footprint.MinZ);
                maxZ = Mathf.Max(maxZ, footprint.MaxZ);
            }

            Assert.That(area, Is.EqualTo(480f).Within(Tolerance), "발자국 합이 372m2 가 아니다.");
            Assert.That(area - LastShiftPlazaLayout.CoreArea, Is.EqualTo(464f).Within(Tolerance));

            Assert.That(maxX - minX, Is.EqualTo(32f).Within(Tolerance));
            Assert.That(maxZ - minZ, Is.EqualTo(28f).Within(Tolerance));
            Assert.That((maxX - minX) / (maxZ - minZ), Is.EqualTo(1.143f).Within(0.01f),
                "종횡비가 1.14:1 에서 움직였다 — 현행 6.33:1 을 뒤집은 것이 원형 껍질의 근거다(§0-8).");
        }

        // ── 원형 껍질 (§7 미결 1) ────────────────────────────────────────────

        [Test]
        public void TheDiscRadiusClearsEveryFootprintWithRoomForOneExpansionRing()
        {
            // 반지름 19m 의 근거를 값이 아니라 부등식으로 고정한다. 발자국이 한 번만 움직여도
            // 이 검사가 먼저 걸려야 하고, 그때 고칠 것은 상수 하나다.
            var required = LastShiftPlazaLayout.MinInscribedClearance +
                           LastShiftPlazaLayout.ExpansionAllowance;

            var thinnest = float.MaxValue;
            var thinnestSpace = LastShiftPlazaSpace.Plaza;
            foreach (var footprint in LastShiftPlazaLayout.Footprints)
            {
                var margin = LastShiftPlazaLayout.InscribedMargin(footprint);
                if (margin >= thinnest) continue;
                thinnest = margin;
                thinnestSpace = footprint.Space;
            }

            // 에어록 홀 폐지 + 기능실 확장으로 최원 모서리가 숙소로 옮겨 갔다(2026-08-10).
            Assert.That(thinnestSpace, Is.EqualTo(LastShiftPlazaSpace.Quarters),
                "최원 모서리가 숙소가 아니다 — 반지름을 정한 구조물이 바뀌었다.");
            Assert.That(thinnest, Is.GreaterThanOrEqualTo(required),
                $"최빡빡 구획 여유가 {thinnest:F3}m 로 요구 {required:F3}m 아래다. 원반을 키우거나 " +
                "확장 한 겹 가정을 다시 봐야 한다.");
            Assert.That(thinnest, Is.EqualTo(2.933f).Within(0.001f),
                "실측 여유가 2.680m 에서 움직였다.");

            // 한 눈금 아래(18m)로는 요구를 못 맞춘다는 것이 19m 를 고른 이유다.
            var atEighteen = 18f * Mathf.Cos(Mathf.PI / LastShiftPlazaLayout.SegmentCount) -
                             Mathf.Sqrt(11f * 11f + 12f * 12f);
            Assert.That(atEighteen, Is.LessThan(required),
                "반지름 18m 로도 요구를 맞춘다 — 19m 는 그만큼 과하다.");
        }

        [Test]
        public void ChordApproximationStillReadsAsACurve()
        {
            // 판 48장으로 두른 다각형의 최대 새그가 판 두께보다 작아야 테두리가 다각형이 아니라
            // 곡선으로 읽힌다. 정원이라 새그가 닫힌 식으로 나온다 — 타원에서 세그먼트를 실제로
            // 돌아야 했던 이유(매개변수 t 가 기하 각이 아니다)가 종횡비 1:1 에서 사라진다.
            var sag = LastShiftPlazaLayout.HullRadius *
                      (1f - Mathf.Cos(Mathf.PI / LastShiftPlazaLayout.SegmentCount));

            Assert.That(sag, Is.EqualTo(0.0963f).Within(0.0005f));
            Assert.That(sag, Is.LessThan(LastShiftCompartments.PanelThickness),
                "테두리 새그가 판 두께보다 크다 — 판 수를 늘려야 곡선으로 읽힌다.");
        }

        // `TheCurrentSpineDoesNotFitTheNewDiscYet` 이 여기 있었다. 그 검사는 통과가 아니라
        // <b>순서</b>를 지키는 것이었다 — 일자 스파인 선미 모서리(19, 3)가 새 원 밖이라
        // 발자국보다 껍질을 먼저 갈아 끼우면 배가 잘렸다. 이제 둘이 같은 커밋에서 옮겨졌으므로
        // 그 유예 근거가 사라졌고, 예고대로 삭제한다.
        //
        // 그 자리를 대신하는 것이 아래다: 지금 서 있는 발자국 일곱이 전부 새 원 안이어야 한다.

        [Test]
        public void EverySpaceNowFitsInsideTheDisc()
        {
            foreach (var footprint in LastShiftPlazaLayout.Footprints)
                Assert.That(LastShiftPlazaLayout.InscribedMargin(footprint), Is.GreaterThan(0f),
                    $"{footprint.Space} 가 원반 내접 다각형 밖이다 — 씬에서 그 방이 테두리 판에 잘린다.");

            // 껍질도 같은 반지름을 본다. 둘이 갈라지면 검산은 통과하고 씬만 틀린다.
            Assert.That(LastShiftHullShell.Radius, Is.EqualTo(LastShiftPlazaLayout.HullRadius));
            Assert.That(LastShiftHullShell.AspectRatio, Is.EqualTo(1f).Within(1e-6f),
                "외피가 아직 타원이다 — 허브 앤 스포크는 원형 실루엣이 맞다(§0-8).");
        }

        // ── 구역 판정 (§6.2) ─────────────────────────────────────────────────

        [Test]
        public void ZoneMembershipNoLongerFallsOutOfXAlone()
        {
            // 밴드 훑기를 못 쓰게 만든 자리다. 전력실과 냉각실이 같은 x 범위를 z 좌우로
            // 나눠 가지므로 같은 x 에서 구역이 갈린다 — x 하나로 답하는 판정은 여기서
            // 반드시 한쪽을 틀린다.
            Assert.That(LastShiftPlazaLayout.ResolveZone(0f, -8.5f), Is.EqualTo(LastShiftZone.Power));
            Assert.That(LastShiftPlazaLayout.ResolveZone(0f, 8.5f), Is.EqualTo(LastShiftZone.Cooling));

            // <b>이관이 끝났다</b>(§9.3-2). 예전 이 검사는 "밴드 훑기가 아직 z 를 안 본다" 를
            // 확인해 이관 필요를 증명했고, 지금은 그 반대를 잰다.
            Assert.That(LastShiftZoneAtlas.ResolveHull(new Vector3(0f, 0f, -8.5f)),
                Is.EqualTo(LastShiftZone.Power));
            Assert.That(LastShiftZoneAtlas.ResolveHull(new Vector3(0f, 0f, 8.5f)),
                Is.EqualTo(LastShiftZone.Cooling));
        }

        [Test]
        public void ThePlazaAndItsTwoAnnexesBelongToTheCockpitZone()
        {
            // §3.2. 조종석↔광장이 문 없는 개구부여야 하므로 둘은 정의상 같은 구역이고,
            // 광장에 일반문으로 붙은 에어록 홀·숙소가 그 구역을 따라온다.
            var expected = new (LastShiftPlazaSpace Space, LastShiftZone Zone)[]
            {
                (LastShiftPlazaSpace.Plaza, LastShiftZone.Cockpit),
                (LastShiftPlazaSpace.CockpitRoom, LastShiftZone.Cockpit),
                (LastShiftPlazaSpace.Quarters, LastShiftZone.Cockpit),
                (LastShiftPlazaSpace.PowerRoom, LastShiftZone.Power),
                (LastShiftPlazaSpace.CoolingRoom, LastShiftZone.Cooling),
                (LastShiftPlazaSpace.LifeSupportRoom, LastShiftZone.LifeSupport)
            };

            foreach (var (space, zone) in expected)
                Assert.That(LastShiftPlazaLayout.Of(space).Zone, Is.EqualTo(zone),
                    $"{space} 의 구역 소속이 바뀌었다 — 조항 S-1 의 구역 넷 표(§3.1)를 다시 봐야 한다.");

            // 판정 체적(부속 제외)이 48 / 30 / 30 / 48 이라 정적 발자국 기준 부피비가 1.6배다.
            // 실 기밀 체적으로 재면 264 / 30 = 8.8배로 즉시 위반이고, 그 회피를 닫는 것은
            // B-2(game-balance)다 — §3.3 이 숨기지 않고 적어 둔 부채라 여기서도 안 감춘다.
            var judged = new float[LastShiftZoneAtlas.ZoneCount];
            var sealedArea = new float[LastShiftZoneAtlas.ZoneCount];
            foreach (var footprint in LastShiftPlazaLayout.Footprints)
            {
                sealedArea[(int)footprint.Zone] += footprint.Area;
                if (footprint.Space is LastShiftPlazaSpace.Plaza or
                    LastShiftPlazaSpace.Quarters) continue;
                judged[(int)footprint.Zone] += footprint.Area;
            }

            Assert.That(Ratio(judged), Is.EqualTo(1.25f).Within(Tolerance),
                "RG-1(3) 현행 조문(정적 발자국) 기준 부피비가 1.6배에서 움직였다.");
            Assert.That(Ratio(sealedArea), Is.EqualTo(4.25f).Within(Tolerance),
                "실 기밀 체적 기준 부피비가 8.8배에서 움직였다 — B-2 개정이 닫아야 하는 값이다.");
        }

        // ── 도우미 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 광장을 격자로 훑어 동시 판독 구역 수의 분포를 센다. 인덱스 <c>0..3</c> 이 구역 수이고
        /// <c>4</c> 번 칸에 유효 표본 수가 들어간다.
        ///
        /// 좌표를 누적이 아니라 인덱스에서 바로 뽑는다 — 검산 스크립트는 <c>x += step</c> 로
        /// 밀지만 그쪽은 배정도라 <c>240</c>번 누적이 안 보이고, <c>float</c> 에서 같은 짓을 하면
        /// 마지막 열이 경계를 넘거나 못 미친다.
        /// </summary>
        private static int[] SampleReadings(float coreHalfExtent)
        {
            var counts = new int[5];
            const int steps = 240;
            for (var i = 0; i < steps; i++)
            {
                var x = LastShiftPlazaLayout.PlazaMinX + SampleStep * (i + 0.5f);
                for (var j = 0; j < steps; j++)
                {
                    var z = LastShiftPlazaLayout.PlazaMinZ + SampleStep * (j + 0.5f);
                    if (Mathf.Abs(x) <= coreHalfExtent && Mathf.Abs(z) <= coreHalfExtent) continue;
                    counts[4]++;
                    counts[LastShiftPlazaLayout.SimultaneousZoneReadings(x, z)]++;
                }
            }
            return counts;
        }

        private static float Ratio(float[] values)
        {
            var min = float.MaxValue;
            var max = 0f;
            foreach (var value in values)
            {
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }
            return max / min;
        }

        private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.0001f;
    }
}
