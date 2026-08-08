using System.Collections.Generic;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// <c>docs/rg1-recalc-bow-cockpit-plaza-v1.md</c> 가 계산한 값을 <b>계산된 좌표 위에서</b>
    /// 고정한다. 대상은 <see cref="LastShiftPlazaProposal"/> 이고 정본(<c>LastShiftCompartments</c>)은
    /// 안 건드린다.
    ///
    /// <b>왜 <see cref="LastShiftRg1GuardrailTests"/> 의 고정값을 갈아 끼우지 않는가.</b> 저쪽이
    /// 재는 것은 <b>지금 서 있는 배</b>다(조종석 최장 이탈 <c>30.61m</c>·<c>8.45초</c>, 부속 체적비
    /// <c>8.46</c>/<c>9.21배</c>). 도안 좌표는 아직 <c>LastShiftCompartments</c> 에 안 들어갔으므로
    /// 저 값들은 여전히 실측이고, 지금 <c>29.67m</c>·<c>12.05배</c> 로 바꾸면 테스트가 아무것도 안
    /// 재는 상태가 된다. 재계산 §9-2·§9-5 가 그 갈이를 "구현 시"·"좌표가 코드에 들어가는 카드에서"
    /// 로 미룬 이유다. <b>이 파일이 그때까지 새 값을 지키는 자리다</b> — 도안 쪽 좌표가 흔들리면
    /// 여기가 먼저 걸리고, 채택 카드는 이 파일의 값을 저쪽으로 옮기기만 하면 된다.
    ///
    /// 재는 자는 <see cref="LastShiftRg1GuardrailTests"/> 와 같은 정의다(사슬 + 스파인, 압력문
    /// <c>0.8초</c> 상수). <b>한 곳만 다르다</b> — 쌍 읽기의 스파인 항을 <c>x</c> 차가 아니라 실거리로
    /// 잰다. 지금 배는 폭이 <c>6m</c> 라 <c>x</c> 근사가 늘 과대평가였지만, 광장(<c>18m</c>)과 선미
    /// 클러스터(<c>z -13~+18</c>)에서는 부호가 뒤집혀 과소평가가 된다(재계산 §5·§0-2).
    /// </summary>
    public sealed class LastShiftPlazaRg1Tests
    {
        /// <summary>가드레일 (1) 한도. 정본과 같은 값이다.</summary>
        private const float TraverseLimitSeconds = 10f;

        /// <summary>압력문 통과 시간. 가드레일 (1) 판정 상수 — 측정법 정본 §1 (M-5).</summary>
        private const float PressureDoorSeconds = 0.8f;

        /// <summary>
        /// 우주복 산소가 버티는 시간. 가드레일 <c>(4-b)</c> 가 견주는 예산이고, 상수로 안 적고
        /// 튜닝에서 파생시킨다 — 드레인이 바뀌면 이 테스트가 같이 움직여야 한다.
        /// </summary>
        private const float SuitOxygenSeconds =
            LastShiftRecoveryTuning.SuitOxygenInitial / LastShiftRecoveryTuning.SuitOxygenDrainPerSecond;

        /// <summary>
        /// 재계산 §7 이 명시적으로 승인한 부속 체적 래칫. 현행 배의 <c>8.5</c> 를 대신하는 값이고,
        /// 도안 좌표가 <c>LastShiftCompartments</c> 로 들어갈 때 저쪽 상수가 이 값이 된다.
        /// </summary>
        private const float AttachedVolumeRatioRatchet = 12.1f;

        /// <summary>같은 승인의 기항 개방 쪽. 현행 <c>9.25</c> 를 대신한다.</summary>
        private const float UnlockedAttachedVolumeRatioRatchet = 12.6f;

        /// <summary>
        /// 측정법 <c>v1.1</c> 의 관측 항목 <c>W-1</c>("구역 내 최장 동선") 래칫. 현행 배 값이므로
        /// 도안 값과 <b>직접 비교되는 기준선</b>이다 — <c>W-1</c> 은 한도가 아니라 래칫만 둔다.
        /// </summary>
        private const float CockpitWalkRatchet = 33.03f;

        private const float LifeSupportWalkRatchet = 28.47f;

        private const float Tolerance = 0.01f;

        // ── §2 정정 둘 (§9-7) ────────────────────────────────────────────────

        [Test]
        public void TheCargoHoldTouchesThePortWallAndHydroponicsIsOpen()
        {
            // 재계산 §2 가 "밸런스 이전에 기하가 안 닫히는 문제" 로 잡은 둘이다. 도안 §2.4 원안은
            // 화물칸이 좌현 벽에서 2m 떠 부모가 아예 안 생기고, 수경재배가 Locked 라 그 유일한
            // 이웃인 격납고 80m2 가 통째로 못 들어가는 방이 된다.
            var cargo = LastShiftPlazaProposal.Of("화물칸");
            Assert.That(cargo.MaxZ, Is.EqualTo(-LastShiftShipDimensions.HalfWidth).Within(Tolerance),
                $"화물칸이 좌현 벽에서 {Mathf.Abs(-LastShiftShipDimensions.HalfWidth - cargo.MaxZ):F1}m 떠 있다 — " +
                "부모가 없으므로 사슬이 안 만들어진다(재계산 §2-나).");
            Assert.That(cargo.Area, Is.EqualTo(80f).Within(Tolerance),
                "화물칸 발자국이 80m2 가 아니다 — 재계산 §7 의 부속 체적이 정정 전 64m2 로 되돌아간다.");

            var hydroponics = LastShiftPlazaProposal.Of("수경재배·산소재생실");
            Assert.That(hydroponics.OpenInP0, Is.True,
                "수경재배가 P0 에서 잠겨 있다 — 격납고의 유일한 이웃이라 격납고가 못 들어가는 방이 된다(재계산 §2-가).");
            Assert.That(LastShiftPlazaProposal.Of("격납고").Parent, Is.EqualTo(hydroponics.Name),
                "격납고 부모가 수경재배가 아니다 — 선미 클러스터가 선체에서 떨어졌다.");
        }

        // ── §3 문 좌표·부모 사슬 ─────────────────────────────────────────────

        [Test]
        public void EveryAttachedFootprintHasADoorOnItsOwnFace()
        {
            // §9-8 결정. 도안 §2-다 는 1m 간극 위에 문을 놓았는데 그러면 정본의
            // DoorSitsOnOwnBoundary 를 통과 못 한다. 문을 자기 면으로 당기는 쪽을 택했다 —
            // 발자국을 하나도 안 움직이므로 §7 의 래칫과 §11 의 원반 검사가 전부 그대로다.
            foreach (var footprint in Attached())
            {
                var face = OwnFaceOf(footprint);
                Assert.That(face, Is.Not.Null,
                    $"{footprint.Name} 의 문 ({footprint.DoorX}, {footprint.DoorZ}) 이 자기 경계면 위에 없다.");

                var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
                var (min, max) = SpanAlong(footprint, face);
                var centre = face.StartsWith("z") ? footprint.DoorX : footprint.DoorZ;
                Assert.That(centre - half, Is.GreaterThanOrEqualTo(min - Tolerance),
                    $"{footprint.Name} 의 문이 자기 면 밖으로 넘친다.");
                Assert.That(centre + half, Is.LessThanOrEqualTo(max + Tolerance),
                    $"{footprint.Name} 의 문이 자기 면 밖으로 넘친다.");
            }
        }

        [Test]
        public void FourBulkheadDoorsStillMissTheParentFaceByExactlyOneMetre()
        {
            // <b>여기가 §9-8 이 안 닫은 절반이다.</b> 문을 자기 면으로 당겨도 부모 면은 1m 떨어져
            // 있고, 정본의 EveryDoorAlsoSitsOnTheFaceItConnectsTo 는 두 면이 같은 평면일 것을
            // 요구한다. 실제 기하는 "1m 두께 격벽에 뚫린 문" 이라 모순이 아니고, 모순인 것은
            // 정본 사양 모델이 두께 있는 격벽을 표현 못 한다는 쪽이다.
            //
            // 도안 §2-다 는 간극 셋(격납고·숙소·의무실)만 셌는데 <b>휴게실이 빠져 있다</b> —
            // 휴게실 z -10~-4 와 화장실 z -3 사이도 같은 1m 다.
            //
            // 채택 카드가 고를 수 있는 길 둘의 값을 여기 적어 둔다. 둘 다 balance 소관이다.
            //   (가) 격벽을 자식 방에 준다(간극 0, 면적 +20m2)
            //          부속 체적비 12.55 -> 13.175 로 §7 이 승인한 12.6 래칫을 넘는다
            //   (나) 자식 방을 부모 쪽으로 1m 평행이동(면적 보존)
            //          체적비는 12.05/12.55 그대로지만 §5 최장 쌍이 39.25 -> 36.43m 로 내려가
            //          "여유 1.02배" 라는 재계산의 결론 자체가 바뀐다
            var offenders = Attached()
                .Where(footprint => footprint.Parent != null)
                .Where(footprint => ParentFaceGap(footprint) > Tolerance)
                .ToArray();

            Assert.That(offenders.Select(footprint => footprint.Name).OrderBy(name => name),
                Is.EqualTo(new[] { "격납고", "숙소", "의무실", "휴게실" }.OrderBy(name => name)),
                "1m 격벽 문 목록이 바뀌었다 — 채택 카드가 봐야 하는 대상이 달라졌다.");

            foreach (var footprint in offenders)
                Assert.That(ParentFaceGap(footprint), Is.EqualTo(1f).Within(Tolerance),
                    $"{footprint.Name} 과 부모 {footprint.Parent} 사이 격벽이 1m 가 아니다 — " +
                    "재계산 §2-다 가 판 두께로 읽은 전제가 깨졌다.");

            // 구명정은 같은 화장실 사슬인데 격벽이 없다. 목록이 "사슬 깊이 2 전부" 가 아니라
            // "간극이 있는 것만" 이라는 것을 여기서 고정한다.
            Assert.That(ParentFaceGap(LastShiftPlazaProposal.Of("구명정")), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void TheTwoClustersNeverMeet()
        {
            // 도안 §4 불변식 2. FORE 뿌리 넷은 광장, AFT 뿌리 셋은 산소실 방 또는 선체이고,
            // 부모 사슬을 타면 두 집합이 절대 안 만난다 — 만나면 두 구역이 압력문을 안 거치고
            // 이어진 것이다.
            foreach (var footprint in Attached())
            {
                var root = RootOf(footprint);
                var zone = LastShiftZoneAtlas.Resolve(root.Door);
                Assert.That(zone, Is.EqualTo(footprint.DoorX < 0f ? LastShiftZone.Cockpit : LastShiftZone.LifeSupport),
                    $"{footprint.Name} 의 사슬 뿌리 {root.Name} 이 다른 클러스터로 넘어갔다.");
            }

            // 그리고 뿌리는 자기가 붙는다고 적은 면 위에 문을 낸다.
            foreach (var footprint in Attached().Where(f => f.Parent == null))
            {
                if (footprint.Attaches == "선체 좌현벽")
                {
                    Assert.That(footprint.DoorZ, Is.EqualTo(-LastShiftShipDimensions.HalfWidth).Within(Tolerance),
                        $"{footprint.Name} 의 문이 선체 좌현벽 평면에 없다.");
                    continue;
                }

                var host = LastShiftPlazaProposal.Of(footprint.Attaches);
                Assert.That(FaceGap(footprint, host), Is.EqualTo(0f).Within(Tolerance),
                    $"{footprint.Name} 의 문 평면이 {host.Name} 의 경계면과 다르다 — 뿌리가 아니라 떠 있는 방이다.");
            }
        }

        // ── §4·§12.1 이탈 판정값 ─────────────────────────────────────────────

        [Test]
        public void EgressStaysUnderTheLimitAndTheWorstMovesToLifeSupport()
        {
            // 재계산 §0-1 의 표다. 최악 위치가 조종석에서 산소실로 넘어가는데 최악 값은
            // 8.45 -> 8.22초로 내려간다 — 조종석 사슬 깊이가 3 에서 1 로 펴진 결과다.
            var worst = WorstEgressPerZone(includeUnlockable: true);

            foreach (var (zone, meters, source) in worst)
                Assert.That(EgressSeconds(meters), Is.LessThan(TraverseLimitSeconds),
                    $"{LastShiftZoneAtlas.ShortLabelOf(zone)} 최장 이탈이 {EgressSeconds(meters):F2}초다 " +
                    $"(출발점 {source}) — 도안이 RG-1(1) 을 넘었다.");

            AssertZone(worst, LastShiftZone.Cockpit, 17.72f, "관측실");
            AssertZone(worst, LastShiftZone.Power, 5f, "구역 자체");
            AssertZone(worst, LastShiftZone.Cooling, 5f, "구역 자체");
            AssertZone(worst, LastShiftZone.LifeSupport, 29.67f, "의무실");

            var thinnest = worst.OrderByDescending(row => row.Meters).First();
            Assert.That(thinnest.Zone, Is.EqualTo(LastShiftZone.LifeSupport),
                "도안 최악이 산소실이 아니다 — 재계산 §0-1 의 전제가 무너졌다.");
            Assert.That(EgressSeconds(thinnest.Meters), Is.EqualTo(8.22f).Within(Tolerance),
                "도안 RG-1(1) 판정값이 8.22초에서 움직였다. 현행 배 8.45초 대비 개선이라는 결론이 " +
                "이 값에 걸려 있다(재계산 §12.1).");

            // P0 은 의무실이 Locked 이므로 최악이 생활공간으로 내려앉는다.
            var p0 = WorstEgressPerZone(includeUnlockable: false);
            AssertZone(p0, LastShiftZone.LifeSupport, 25.93f, "숙소");
            Assert.That(EgressSeconds(p0.Max(row => row.Meters)), Is.EqualTo(7.28f).Within(Tolerance),
                "P0 판정값이 7.28초에서 움직였다(재계산 §12.1 둘째 줄).");

            Debug.Log($"[LAST_SHIFT_PLAZA_RG1] egress worst={thinnest.Meters:F2}m " +
                      $"{EgressSeconds(thinnest.Meters):F2}s zone={thinnest.Zone} src={thinnest.Source} " +
                      $"margin={TraverseLimitSeconds / EgressSeconds(thinnest.Meters):F2}x result=PASS");
        }

        [Test]
        public void PullingTheBulkheadDoorsCostsThreeCentimetres()
        {
            // §9-8 을 "문을 자기 면으로" 로 닫은 대가가 이것뿐이라는 근거다. 재계산이 §12.1 에
            // 적은 29.64m·8.21초는 문이 격벽 한가운데 있을 때의 값이고, 자기 면으로 당기면
            // 방 안쪽 구간이 짧아지는 대신 문-문 구간이 길어져 순증이 3cm 다. 판정은 안 움직인다.
            var pulled = WorstEgressPerZone(includeUnlockable: true)
                .First(row => row.Zone == LastShiftZone.LifeSupport).Meters;

            Assert.That(pulled - 29.6356f, Is.EqualTo(0.0307f).Within(0.001f),
                "격벽 문을 당긴 대가가 3cm 에서 움직였다 — 재계산 §12.1 과의 차이를 다시 적어야 한다.");
            Assert.That(EgressSeconds(pulled), Is.LessThan(EgressSeconds(29.6356f) + 0.01f),
                "당긴 쪽이 원표보다 0.01초 넘게 나빠졌다 — 그러면 발자국 쪽으로 닫아야 한다.");
        }

        // ── §6 RG-1(4-b) 탈출 보장 ───────────────────────────────────────────

        [Test]
        public void EveryCompartmentEscapesOnATenthOfTheSuitOxygenBudget()
        {
            // (1) 과 재는 것이 다르다 — 종점이 구역 경계 평면(x)이 아니라 <b>출구 개구부 실좌표</b>
            // (x, z) 다. 둘이 거의 같은 양이 됐지만 도안에서는 개구부 3 의 z = -2.2 때문에
            // 29.67m 대 29.84m 로 갈린다. 판정이 갈릴 만한 차이가 아니라는 것이 §1 의 각주다.
            //
            // 선행 재계산이 "31배 -> 9.5배 로 3.3배 축소" 를 경고 신호로 적었는데, 도안은 그
            // 신호를 더 나쁘게도 더 좋게도 만들지 않는다 — <b>위치만 조종석에서 산소실로 옮긴다.</b>
            var worst = 0f;
            var worstName = string.Empty;
            foreach (var footprint in Attached())
            {
                var meters = EscapeMeters(footprint);
                var seconds = meters / LastShiftPlayerController.MoveSpeed + PressureDoorSeconds;
                Assert.That(seconds, Is.LessThan(SuitOxygenSeconds),
                    $"{footprint.Name} 탈출이 {seconds:F2}초로 SuitOxygen 예산을 넘는다 — RG-1(4-b) 위반이다.");
                if (meters <= worst) continue;
                (worst, worstName) = (meters, footprint.Name);
            }

            Assert.That(worstName, Is.EqualTo("의무실"));
            Assert.That(worst, Is.EqualTo(29.84f).Within(Tolerance),
                "산소실 최악 탈출 거리가 29.84m 에서 움직였다(재계산 §6 + §9-8 격벽 문 정합).");

            var seconds4b = worst / LastShiftPlayerController.MoveSpeed + PressureDoorSeconds;
            var budget = SuitOxygenSeconds / seconds4b;
            Assert.That(budget, Is.EqualTo(9.69f).Within(Tolerance),
                $"SuitOxygen 대비 여유가 {budget:F2}배다 — 재계산 §6 의 9.69배에서 움직였다. " +
                "현행 배 9.5배 대비 사실상 불변이라는 것이 도안 판정의 근거다.");

            // 조종석 구역은 두 배 좋아진다. 선수 사슬 16m 가 사라진 직접 효과이고, 도안이
            // RG-1 에 준 이득은 전부 여기 있다.
            var cockpitWorst = Attached()
                .Where(footprint => footprint.DoorX < 0f)
                .Max(EscapeMeters);
            Assert.That(cockpitWorst, Is.EqualTo(14.54f).Within(Tolerance),
                "조종석 구역 최악 탈출이 14.54m(관측실)에서 움직였다(재계산 §6).");

            // 조종석 방 자체의 선수 구석은 관측실보다 짧다 — 방이 구역 끝벽 안에 있어서다.
            var bowCorner = Vector3.Distance(
                new Vector3(-LastShiftShipDimensions.HalfLength, 0f, LastShiftShipDimensions.HalfWidth),
                BoundaryOpening(LastShiftZone.Cockpit));
            Assert.That(bowCorner, Is.LessThan(cockpitWorst),
                "조종석 방 선수 구석이 관측실보다 멀다 — 그러면 §6 표의 최악 출발점이 바뀐다.");

            Debug.Log($"[LAST_SHIFT_PLAZA_RG1] escape worst={worst:F2}m {seconds4b:F2}s ({worstName}) " +
                      $"budget={budget:F2}x cockpit={cockpitWorst:F2}m bowCorner={bowCorner:F2}m result=PASS");
        }

        // ── §5·§12.2 최장 동선 = 측정법 v1.1 의 W-1 ──────────────────────────

        [Test]
        public void TheLifeSupportWalkRatchetMovesAndLandsOnBranchB()
        {
            // 측정법 v1.1 §2.4 가 쌍 읽기를 <c>RG-1(2)</c> 트리거에서 떼어내 독립 관측 항목
            // W-1 로 옮겼다. 한도는 없고 래칫만 있다 — 그래서 여기서 재는 것은 "위반인가" 가
            // 아니라 "어느 분기로 가는가" 다.
            //
            // <b>이 도안은 (b) 다.</b> 산소실 쌍의 양 끝(의무실·격납고)이 둘 다 부속 구획이고
            // RG-1(2) 복구 항목표(파공 봉합 → 배전반 → 재가압)는 본선 좌표만 쓴다. 그래서
            // 33.2초가 안 움직이고 (2) 재계산 의무가 안 생긴다. 대신 (b) 가 요구하는 것이
            // 붙는다 — <b>산소실 구역에 새 목적지를 놓지 않는다.</b> 놓는 순간 (a) 가 켜진다.
            var pairs = LongestPairPerZone(includeUnlockable: true);

            Assert.That(pairs[(int)LastShiftZone.Cockpit], Is.EqualTo(28.44f).Within(Tolerance),
                "조종석 최장 동선이 28.44m 에서 움직였다(재계산 §12.2).");
            Assert.That(pairs[(int)LastShiftZone.Cockpit], Is.LessThan(CockpitWalkRatchet),
                "조종석 W-1 이 래칫 33.03m 를 넘었다 — 선수 쪽에서도 측정법 v1.1 §2.4 분기를 타야 한다.");

            Assert.That(pairs[(int)LastShiftZone.LifeSupport], Is.EqualTo(39.37f).Within(Tolerance),
                "산소실 최장 동선이 39.37m 에서 움직였다. 측정법 §5 각주가 적은 39.25m 와 12cm 차이가 " +
                "나는 것은 §9-8 을 '문을 자기 면으로' 로 닫았기 때문이다 — 그보다 크게 움직였으면 " +
                "발자국이나 사슬이 바뀐 것이다.");
            Assert.That(pairs[(int)LastShiftZone.LifeSupport], Is.GreaterThan(LifeSupportWalkRatchet),
                "산소실 W-1 이 래칫 28.47m 아래로 내려왔다 — 그러면 §9-9 의 검증 실험 전제가 사라진다.");

            // P0 에서도 이미 넘어 있다. 의무실 언락이 초과를 만든 것이 아니라 벌린 것이다.
            var p0 = LongestPairPerZone(includeUnlockable: false);
            Assert.That(p0[(int)LastShiftZone.LifeSupport], Is.EqualTo(35.64f).Within(Tolerance),
                "P0 산소실 최장 동선이 35.64m 에서 움직였다(재계산 §5 둘째 줄).");
            Assert.That(p0[(int)LastShiftZone.LifeSupport], Is.GreaterThan(LifeSupportWalkRatchet));

            Debug.Log($"[LAST_SHIFT_PLAZA_RG1] W-1 cockpit={pairs[(int)LastShiftZone.Cockpit]:F2}m " +
                      $"lifeSupport={pairs[(int)LastShiftZone.LifeSupport]:F2}m " +
                      $"(p0 {p0[(int)LastShiftZone.LifeSupport]:F2}m) ratchet={LifeSupportWalkRatchet:F2}m " +
                      "branch=b result=NO_RG1(2)_RECALC");
        }

        [Test]
        public void TheLongestWalkStaysADesignSignalNotAJudgement()
        {
            // 측정법 v1.1 §2.5. W-1 은 판정이 아니라 설계 신호다 — 상호작용 없이 걷기만 하는
            // 시간이라 10초 근처를 체감 상한으로 본다. 그 값을 여기 남기는 이유는 §7-5 가
            // 의무실 이전안을 <b>W-1 권고</b>로 승격했고, 승격의 근거가 이 초 단위이기 때문이다.
            //
            // 부등식(이탈 > 쌍)은 <b>안 잰다.</b> v1.1 §2.2 가 그것을 조문과 테스트에서 내렸다 —
            // 현행 배에 한정된 관측이었고 조문의 성질이 아니었다. 여기서 뒤집힌 쪽을 다시
            // 고정하면 내린 것을 반대 방향으로 되살리는 셈이 된다.
            var walkSeconds = LongestPairPerZone(includeUnlockable: true)
                .Max() / LastShiftPlayerController.MoveSpeed;

            Assert.That(walkSeconds, Is.EqualTo(9.84f).Within(Tolerance),
                "최장 동선이 9.84초에서 움직였다 — 10초 체감 상한까지 0.16초뿐이라 " +
                "재계산 §5 의 조정(의무실을 선미 사슬에서 뺀다)이 W-1 권고로 걸려 있다(측정법 v1.1 §7-5).");

            // 의무실을 산소실 방 좌현 후미로 빼면 8.91초가 된다. 권고가 실제로 값을 사는지를
            // 같이 고정한다 — 안 사면 그 권고는 근거를 잃는다.
            var moved = LastShiftPlazaProposal.Footprints
                .Select(footprint => footprint.Name == "의무실"
                    ? new LastShiftPlazaProposal.Footprint("의무실", 15f, 19f, -7f, -3f,
                        openInP0: false, doorX: 17f, doorZ: -3f, attaches: "선체 좌현벽")
                    : footprint)
                .ToArray();
            var relocated = LongestPairPerZone(moved, includeUnlockable: true).Max()
                            / LastShiftPlayerController.MoveSpeed;

            Assert.That(relocated, Is.LessThan(walkSeconds - 0.5f),
                $"의무실 이전안이 최장 동선을 {walkSeconds:F2} -> {relocated:F2}초로밖에 못 줄인다 — " +
                "재계산 §5 가 권고한 근거(사슬 깊이 3 을 1 로)가 이 좌표에서 성립 안 한다.");

            Debug.Log($"[LAST_SHIFT_PLAZA_RG1] longestWalk={walkSeconds:F2}s " +
                      $"relocatedMedBay={relocated:F2}s result=W1_ADVISORY");
        }

        [Test]
        public void FreeDoorPlacementBreaksTheLimitWithTheSameFootprints()
        {
            // §3 표가 권고가 아니라 요구인 이유. 접촉면 위 아무 데나 문을 놓으면 같은 발자국으로
            // 10.20초 위반이 만들어진다 — 재계산 §5 마지막 절.
            var free = LastShiftPlazaProposal.Footprints
                .Select(footprint => footprint.Name switch
                {
                    "의무실" => Moved(footprint, 23f, 10.5f),
                    "숙소" => Moved(footprint, 19f, 3.5f),
                    "수경재배·산소재생실" => Moved(footprint, 17f, 3f),
                    "격납고" => Moved(footprint, 17f, 9.5f),
                    _ => footprint
                })
                .ToArray();

            var pair = LongestPairPerZone(free, includeUnlockable: true)[(int)LastShiftZone.LifeSupport];
            Assert.That(pair, Is.EqualTo(40.81f).Within(Tolerance),
                "최악 문 배치 상한이 40.81m 에서 움직였다(재계산 §5).");
            Assert.That(pair / LastShiftPlayerController.MoveSpeed, Is.GreaterThan(TraverseLimitSeconds),
                "자유 배치가 더 이상 한도를 안 넘는다 — 그러면 §3 표를 요구로 둘 근거가 약해진다.");
        }

        // ── §7 부속 체적 래칫 ────────────────────────────────────────────────

        [Test]
        public void AttachedVolumeRatioMatchesTheApprovedRatchets()
        {
            // 재계산 §7 이 명시적으로 승인한 값이다. 8.46 -> 12.05, 9.21 -> 12.55 로 벌어지는데
            // 좁히는 유일한 방법이 큰 방 둘을 선수로 되돌리는 것이고 그러면 RG-1(1) 이 30.61m 로
            // 돌아간다 — 가드레일 둘이 반대 방향을 가리키고 플레이를 가르는 쪽은 (1)·(4-b) 다.
            var initial = AttachedVolumeRatio(includeUnlockable: false);
            var unlocked = AttachedVolumeRatio(includeUnlockable: true);

            Assert.That(initial, Is.EqualTo(12.05f).Within(Tolerance),
                "초기 Access 부속 체적비가 12.05 에서 움직였다(재계산 §7).");
            Assert.That(unlocked, Is.EqualTo(12.55f).Within(Tolerance),
                "기항 개방 후 부속 체적비가 12.55 에서 움직였다(재계산 §7).");

            Assert.That(initial, Is.LessThanOrEqualTo(AttachedVolumeRatioRatchet),
                $"초기 부속 체적비가 {initial:F2}배로 승인 래칫 {AttachedVolumeRatioRatchet} 를 넘었다. " +
                "재계산 §7-3 이 여유를 안 준 것은 다시 걸리는 것이 맞기 때문이다 — balance 재승인이 선행이다.");
            Assert.That(unlocked, Is.LessThanOrEqualTo(UnlockedAttachedVolumeRatioRatchet),
                $"개방 후 부속 체적비가 {unlocked:F2}배로 승인 래칫 {UnlockedAttachedVolumeRatioRatchet} 를 넘었다.");

            // 최대/최소 쌍이 조종석/전력실에서 산소실/전력실로 갈아탄 것이 이 절의 본체다.
            var volumes = ZoneVolumes(includeUnlockable: true);
            Assert.That(ArgMax(volumes), Is.EqualTo(LastShiftZone.LifeSupport),
                "최대 체적이 산소실이 아니다 — 큰 방 둘이 선미에 안 남았다는 뜻이다.");
            Assert.That(ArgMin(volumes), Is.EqualTo(LastShiftZone.Power));

            Debug.Log($"[LAST_SHIFT_PLAZA_RG1] attachedRatio initial={initial:F2}x unlocked={unlocked:F2}x " +
                      $"ratchet={AttachedVolumeRatioRatchet}/{UnlockedAttachedVolumeRatioRatchet} " +
                      $"max={ArgMax(volumes)} min={ArgMin(volumes)} result=APPROVED");
        }

        // ── 재는 자 ─────────────────────────────────────────────────────────
        //
        // <b>자는 더 이상 여기 없다.</b> 정본(LastShiftPlacementRules)이 재고, 이 절이 하는 일은
        // 제안표를 그 자가 읽는 형태로 옮기는 것뿐이다. 승격 전에는 이탈 계산이 이 파일과
        // LastShiftRg1GuardrailTests 에 독립 사본으로 있었고 둘 다 Editor 전용 어셈블리 안이라
        // 런타임에서 부를 방법이 없었다 —
        // docs/tech/free-placement-runtime-chain-estimate-v1.md §5.1.

        private static IEnumerable<LastShiftPlazaProposal.Footprint> Attached() =>
            LastShiftPlazaProposal.Footprints.Where(footprint => footprint.IsAttached);

        /// <summary>
        /// 제안표를 판정기 입력으로 옮긴다. <b>사슬에 참여하는 발자국만 들어간다</b> — 압력
        /// 스파인 여섯은 문이 아니라 개구부로 이어지므로 사슬의 마디가 아니다(<c>IsAttached</c>).
        ///
        /// 이 함수가 하는 일은 <b>부모를 이름으로 가리키는 표를 인덱스로 바꾸는 것</b>이 전부다.
        /// 정본 구획표는 이미 인덱스라 이 단계가 없다 — 두 표의 유일한 구조적 차이가 여기다.
        /// </summary>
        private static (LastShiftPlacement[] Placements, string[] Names) Chain(
            LastShiftPlazaProposal.Footprint[] table)
        {
            var attached = table.Where(footprint => footprint.IsAttached).ToArray();
            var index = new Dictionary<string, int>(attached.Length);
            for (var i = 0; i < attached.Length; i++)
                index[attached[i].Name] = i;

            var placements = new LastShiftPlacement[attached.Length];
            var names = new string[attached.Length];
            for (var i = 0; i < attached.Length; i++)
            {
                var footprint = attached[i];
                placements[i] = new LastShiftPlacement(
                    footprint.MinX, footprint.MaxX, footprint.MinZ, footprint.MaxZ,
                    footprint.Door,
                    footprint.Parent == null ? -1 : index[footprint.Parent],
                    footprint.OpenInP0);
                names[i] = footprint.Name;
            }

            return (placements, names);
        }

        private static float EgressSeconds(float meters) => LastShiftPlacementRules.EgressSeconds(meters);

        private static List<(LastShiftZone Zone, float Meters, string Source)> WorstEgressPerZone(
            bool includeUnlockable) =>
            WorstEgressPerZone(LastShiftPlazaProposal.Footprints, includeUnlockable);

        private static List<(LastShiftZone Zone, float Meters, string Source)> WorstEgressPerZone(
            LastShiftPlazaProposal.Footprint[] table, bool includeUnlockable)
        {
            var (placements, names) = Chain(table);
            var worst = new List<(LastShiftZone, float, string)>();
            foreach (var (zone, meters, index) in
                     LastShiftPlacementRules.WorstEgressPerZone(placements, includeUnlockable))
                worst.Add((zone, meters, index < 0 ? "구역 자체" : names[index]));

            return worst;
        }

        /// <summary>
        /// <c>(4-b)</c> 탈출 거리. 사슬에 <b>선체 문 → 출구 개구부 실좌표</b>를 더한다 —
        /// <c>(1)</c> 이 구역 경계 평면(x)에서 끝나는 자리에서 이쪽은 실제로 나가는 구멍까지 간다.
        ///
        /// 에어록은 §6 각주가 표에서 뺐지만(비가압이라 그 안에서 이미 <c>SuitOxygen</c> 이
        /// 깎이고 있어 자의 단위가 안 맞는다) 여기서는 같이 잰다 — 최악 근처에 안 오므로
        /// 어느 값도 안 움직이고, 빼면 그 사실이 코드에서 안 보인다.
        /// </summary>
        private static float EscapeMeters(LastShiftPlazaProposal.Footprint footprint)
        {
            var (placements, names) = Chain(LastShiftPlazaProposal.Footprints);
            var index = System.Array.IndexOf(names, footprint.Name);
            Assert.That(index, Is.GreaterThanOrEqualTo(0),
                $"{footprint.Name} 이 사슬 표에 없다 — 문 좌표가 NaN 인 스파인 발자국이라는 뜻이다.");

            Assert.That(
                LastShiftPlacementRules.TryChainToHull(
                    placements, placements[index], out var chain, out var hullDoor, out _),
                Is.True, $"{footprint.Name} 사슬이 선체에 안 닿는다 — 부모 사슬이 끊겼거나 순환이다.");

            var zone = LastShiftZoneAtlas.Resolve(hullDoor);
            return chain + Vector3.Distance(hullDoor, BoundaryOpening(zone));
        }

        /// <summary>
        /// 그 구역의 출구 개구부. 조종석은 <c>1</c>, 산소실은 <c>3</c> 이고 둘 다 압력문이라
        /// <see cref="PressureDoorSeconds"/> 가 붙는다. 전력실·냉각실은 양쪽이 다 출구라
        /// "출구 하나" 라는 전제가 안 서는데, 둘 다 <c>5m</c> 라 지금은 최악 근처에 안 온다.
        /// </summary>
        private static Vector3 BoundaryOpening(LastShiftZone zone)
        {
            var opening = zone == LastShiftZone.Cockpit ? 1 : 3;
            return new Vector3(
                LastShiftShipDimensions.OpeningX(opening), 0f,
                LastShiftShipDimensions.OpeningCenterZ(opening));
        }

        private static float[] LongestPairPerZone(bool includeUnlockable) =>
            LongestPairPerZone(LastShiftPlazaProposal.Footprints, includeUnlockable);

        /// <summary>
        /// 후보 셋 중 최대 — (가) 구역 x 길이, (나) 이탈값, (다) 같은 구역 구획 둘의 구석끼리.
        /// (다) 의 스파인 항이 정본과 다르다: <c>x</c> 차가 아니라 두 선체 문 사이 실거리다.
        /// 광장·선미 클러스터가 <c>z</c> 로 벌어져 <c>x</c> 근사가 과소평가로 뒤집히기 때문이다.
        ///
        /// <b>승격에서 두 사본이 유일하게 갈렸던 항이 이것이고, 갈린 것이 의도였다.</b> 그래서
        /// 합치지 않고 <see cref="LastShiftPairSpine"/> 로 남겼다 — 합쳤으면 정본 쪽 고정값
        /// 둘(<c>33.03</c>·<c>28.47</c>)이 조용히 움직였을 것이다.
        /// </summary>
        private static float[] LongestPairPerZone(
            LastShiftPlazaProposal.Footprint[] table, bool includeUnlockable) =>
            LastShiftPlacementRules.LongestPairPerZone(
                Chain(table).Placements, includeUnlockable, LastShiftPairSpine.StraightLine);

        private static float AttachedVolumeRatio(bool includeUnlockable)
        {
            var volumes = ZoneVolumes(includeUnlockable);
            return volumes.Max() / volumes.Min();
        }

        /// <summary>
        /// 정본 <c>AttachedVolumeRatio</c> 와 같은 상한 근사다 — 구역 전 길이 × 선체 폭 × 천장,
        /// 거기에 부속 구획 발자국 × 구획 높이를 자기 선체 문의 구역에 얹는다.
        ///
        /// <b>조종석 열은 광장과 에어록에서 정본 근사와 갈린다</b>(재계산 §7 각주). 비율에는 영향이
        /// 없다 — 최대는 산소실, 최소는 전력실이고 조종석은 어느 쪽도 아니다.
        /// </summary>
        private static float[] ZoneVolumes(bool includeUnlockable)
        {
            var hull = new float[LastShiftZoneAtlas.ZoneCount];
            for (var zone = (LastShiftZone)0; (int)zone < LastShiftZoneAtlas.ZoneCount; zone++)
                hull[(int)zone] = LastShiftShipDimensions.ZoneLength(zone)
                                  * LastShiftShipDimensions.InteriorWidth
                                  * LastShiftShipPhysics.CeilingInnerHeight;

            var (placements, names) = Chain(LastShiftPlazaProposal.Footprints);
            for (var index = 0; index < placements.Length; index++)
            {
                var footprint = LastShiftPlazaProposal.Of(names[index]);
                if (!footprint.OpenInP0 && !includeUnlockable) continue;
                if (!LastShiftPlacementRules.TryZoneOf(placements, placements[index], out var zone)) continue;
                hull[(int)zone] += footprint.Area * LastShiftCompartments.InteriorHeight;
            }

            return hull;
        }

        // ── 잔손 ────────────────────────────────────────────────────────────

        private static LastShiftPlazaProposal.Footprint RootOf(LastShiftPlazaProposal.Footprint footprint)
        {
            while (footprint.Parent != null)
                footprint = LastShiftPlazaProposal.Of(footprint.Parent);
            return footprint;
        }

        /// <summary>문이 앉은 자기 경계면. <c>z=min</c> 처럼 평면 이름을 돌려준다.</summary>
        private static string OwnFaceOf(in LastShiftPlazaProposal.Footprint footprint)
        {
            if (Mathf.Abs(footprint.DoorZ - footprint.MinZ) < Tolerance) return "z=min";
            if (Mathf.Abs(footprint.DoorZ - footprint.MaxZ) < Tolerance) return "z=max";
            if (Mathf.Abs(footprint.DoorX - footprint.MinX) < Tolerance) return "x=min";
            if (Mathf.Abs(footprint.DoorX - footprint.MaxX) < Tolerance) return "x=max";
            return null;
        }

        /// <summary>그 면 위에서 문이 움직일 수 있는 구간.</summary>
        private static (float Min, float Max) SpanAlong(
            in LastShiftPlazaProposal.Footprint footprint, string face) =>
            face != null && face.StartsWith("z")
                ? (footprint.MinX, footprint.MaxX)
                : (footprint.MinZ, footprint.MaxZ);

        /// <summary>문 평면과 부모 경계면 사이 거리. <c>0</c> 이면 두 방이 맞닿아 있다.</summary>
        private static float ParentFaceGap(in LastShiftPlazaProposal.Footprint footprint) =>
            FaceGap(footprint, LastShiftPlazaProposal.Of(footprint.Parent));

        private static float FaceGap(
            in LastShiftPlazaProposal.Footprint footprint, in LastShiftPlazaProposal.Footprint host)
        {
            var face = OwnFaceOf(footprint);
            return face != null && face.StartsWith("z")
                ? Mathf.Min(Mathf.Abs(footprint.DoorZ - host.MinZ), Mathf.Abs(footprint.DoorZ - host.MaxZ))
                : Mathf.Min(Mathf.Abs(footprint.DoorX - host.MinX), Mathf.Abs(footprint.DoorX - host.MaxX));
        }

        private static LastShiftPlazaProposal.Footprint Moved(
            in LastShiftPlazaProposal.Footprint footprint, float doorX, float doorZ) =>
            new(footprint.Name, footprint.MinX, footprint.MaxX, footprint.MinZ, footprint.MaxZ,
                footprint.OpenInP0, footprint.Protrudes, doorX, doorZ, footprint.Parent, footprint.Attaches);

        private static void AssertZone(
            List<(LastShiftZone Zone, float Meters, string Source)> rows,
            LastShiftZone zone, float meters, string source)
        {
            var row = rows.First(candidate => candidate.Zone == zone);
            Assert.That(row.Meters, Is.EqualTo(meters).Within(Tolerance),
                $"{LastShiftZoneAtlas.ShortLabelOf(zone)} 최장 이탈이 {row.Meters:F2}m 다 — " +
                $"재계산값 {meters:F2}m 에서 움직였다.");
            Assert.That(row.Source, Is.EqualTo(source),
                $"{LastShiftZoneAtlas.ShortLabelOf(zone)} 최악 출발점이 {row.Source} 로 바뀌었다.");
        }

        private static LastShiftZone ArgMax(float[] volumes)
        {
            var best = (LastShiftZone)0;
            for (var zone = (LastShiftZone)0; (int)zone < volumes.Length; zone++)
                if (volumes[(int)zone] > volumes[(int)best]) best = zone;
            return best;
        }

        private static LastShiftZone ArgMin(float[] volumes)
        {
            var best = (LastShiftZone)0;
            for (var zone = (LastShiftZone)0; (int)zone < volumes.Length; zone++)
                if (volumes[(int)zone] < volumes[(int)best]) best = zone;
            return best;
        }
    }
}
