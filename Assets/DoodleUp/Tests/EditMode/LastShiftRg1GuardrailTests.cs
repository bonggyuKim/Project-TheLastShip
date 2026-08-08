using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// <c>RG-1</c> 재검토 가드레일 셋(<c>docs/ship-scale-and-density-v1.md</c> §5.4)을 좌표에서
    /// 다시 뽑아 고정한다. <c>docs/rg1-recalc-cargo-procurement-v1.md</c> 가 이 파일이 지키는 값을
    /// 계산한 자리다.
    ///
    /// <b>이 파일이 생긴 이유는 가드레일 (1) 이 조용히 낡았기 때문이다.</b> 문서는 최장 횡단을
    /// <c>3.5초</c>(구역 x 길이 <c>14m</c> / <c>4.0</c>)로 적어 두었는데, <see cref="LastShiftZoneAtlas.Resolve"/>
    /// 는 x 하나로 구역을 정하고 하한이 없다 — 선체 끝벽 너머에 붙은 구획에 서 있는 승무원도
    /// 인접 구역 소속이고 그 구역이 진공이면 거기서 <c>SuitOxygen</c> 이 깎인다. 생활공간 셋과
    /// 구명정이 들어온 시점부터 실제 값은 <c>7.12초</c> 였고 아무것도 그것을 안 봤다. 선수 사슬
    /// 개방(콘텐츠 확장 검토 §2.1-a)이 그 위에 <c>7.65초</c> 를 얹었다.
    ///
    /// 그래서 여기서 검사하는 것은 문서에 적힌 숫자가 아니라 <b>좌표에서 다시 뽑은 숫자</b>다.
    /// 구획이 하나 더 붙거나 사슬이 한 칸 깊어지면 이 테스트가 먼저 걸린다.
    ///
    /// <b>선수 조종석 + 중앙 광장 도안은 이 파일의 값을 전부 갈아 끼운다.</b> 다만 그 좌표는 아직
    /// <see cref="LastShiftCompartments"/> 에 안 들어갔고, 지금 값을 미리 바꾸면 이 파일이 <b>지금
    /// 서 있는 배</b>를 아무것도 안 재게 된다. 그래서 새 값은 <see cref="LastShiftPlazaRg1Tests"/>
    /// 가 제안표 위에서 지키고, 채택 카드가 옮겨 온다 —
    /// <c>docs/rg1-recalc-bow-cockpit-plaza-v1.md</c> §9-2·§9-5 의 목록이 이것이다.
    ///
    /// <code>
    ///   AttachedVolumeRatioRatchet            8.5   ->  12.1     (재계산 §7, balance 승인)
    ///   UnlockedAttachedVolumeRatioRatchet    9.25  ->  12.6
    ///   ...StaysUnderTheRaisedRatchet 고정값  9.21  ->  12.55
    ///   TheThinnestTraverse... 30.61m/8.45s/Cockpit -> 29.67m/8.22s/LifeSupport
    ///   UnlockableCompartments... 개방 전후 동일 -> 25.93 -> 29.67m 로 움직이므로 단언을 다시 쓴다
    ///   LongestPairInAZone...(W-1) 33.03/28.47 -> 28.44/39.37
    /// </code>
    ///
    /// <c>W-1</c> 산소실 값이 재계산 §5 의 <c>39.25m</c> 가 아니라 <c>39.37m</c> 인 것은 §9-8 을
    /// "문을 자기 면으로 당긴다" 로 닫았기 때문이다(<see cref="LastShiftPlazaRg1Tests"/> 의 격벽 절).
    /// </summary>
    public sealed class LastShiftRg1GuardrailTests
    {
        /// <summary>가드레일 (1). 한 구역에서 구역 밖으로 나가는 최악 시간의 한도.</summary>
        private const float TraverseLimitSeconds = 10f;

        /// <summary>
        /// 압력문(구역 경계) 통과 시간. <b>가드레일 (1) 판정에 상수로 들어간다 — 조건부 가산이
        /// 아니다.</b> 구역을 벗어난다는 것은 정의상 구역 경계를 통과하는 것이고 그 경계에 있는
        /// 것이 압력문이므로, 이 문을 안 지나고 구역을 나가는 경로가 없다. 그리고 정확히 한
        /// 번이다 — 첫 경계를 넘는 순간 이탈이 끝난다.
        ///
        /// 구획 문은 압력문이 아니므로 <c>0</c> 이다.
        /// 측정법 정본은 <c>docs/rg1-1-measurement-definition-v1.md</c> §1 (M-5).
        /// </summary>
        private const float PressureDoorSeconds = 0.8f;

        /// <summary>가드레일 (3). 압력존 부피비 한도.</summary>
        private const float VolumeRatioLimit = 3f;

        /// <summary>
        /// 부속 구획까지 포함한 실 기밀 체적비의 현재값 래칫. <b>한도가 아니라 현재값이다.</b>
        ///
        /// 가드레일 (3) 의 조문상 대상은 압력존이고 구획은 <c>ZonePressure</c> 배열에 안 들어가므로
        /// (<c>corridor-4p-redesign-v1.md</c> §24) <c>2.80배</c> 가 판정값이다. 다만 조문의 취지는
        /// "부피 가중 평준화 검토" 이고 실 체적으로 재면 <c>7.98배</c> 라, 값과 취지가 갈라져 있다.
        /// 지금 좁히려면 압력존을 늘리거나 <c>Resolve()</c> 를 볼륨 판정으로 바꿔야 하는데 둘 다
        /// §24 가 기각한 비용이다 — 그래서 좁히는 대신 <b>더 벌어지지 않게</b> 못박는다.
        ///
        /// 값이 문서의 <c>7.98배</c> 가 아니라 <c>8.46배</c> 인 것은 근사 방식 차이다 — 여기서는
        /// 구역 전 길이에 선체 폭을 곱하는 상한 근사를 쓰고, 문서는 방·통로 발자국을 따로 잰다.
        /// 부속 구획 쪽이 압도적이라 어느 쪽으로 재도 결론이 같아서 테스트는 싼 쪽을 쓴다.
        ///
        /// <b>도안 채택 시 <c>12.1</c> 이 된다</b> — 재계산 §7 이 <c>8.46 → 12.05배</c> 를 재고
        /// 명시적으로 승인한 값이고, <see cref="LastShiftPlazaRg1Tests"/> 가 그때까지 지킨다.
        /// </summary>
        private const float AttachedVolumeRatioRatchet = 8.5f;

        /// <summary>
        /// 기항 개방(<c>docs/voyage-run-structure-v1.md</c> §4.2)이 끝난 상태의 실 체적비 래칫.
        /// <b><see cref="AttachedVolumeRatioRatchet"/> 와 합치지 않는다</b> — 합치면 초기 상태가
        /// 조용히 벌어지는 것을 못 잡는다. 초기값은 저쪽이, 개방 후는 이쪽이 지킨다.
        ///
        /// 잠긴 셋 중 이 값을 올리는 것은 서버/통신실 하나다. 수경재배·의무실은 이미 작은 쪽
        /// (산소실)에 붙어서 최대/최소를 안 건드린다 —
        /// <c>docs/rg1-recalc-voyage-port-unlock-v1.md</c> §3.2.
        ///
        /// <b>도안 채택 시 <c>12.6</c> 이 된다</b>(재계산 §7). 두 래칫의 간격이 <c>0.75 → 0.5</c> 로
        /// 좁아지는 것도 의도다 — 도안에서는 잠긴 셋 중 큰 쪽을 올리는 것이 서버/통신실 하나뿐이고
        /// 그 발자국이 <c>15m2</c> 라, 개방이 비율에 미치는 영향 자체가 줄어든다.
        /// </summary>
        private const float UnlockedAttachedVolumeRatioRatchet = 9.25f;

        private const float Tolerance = 0.01f;

        [Test]
        public void AttachedCompartmentsKeepEveryZoneTraverseUnderTheLimit()
        {
            var worst = WorstTraversePerZone();

            foreach (var (zone, meters, source) in worst)
            {
                var seconds = EgressSeconds(meters);
                Assert.That(seconds, Is.LessThan(TraverseLimitSeconds),
                    $"{LastShiftZoneAtlas.ShortLabelOf(zone)} 최장 이탈 {seconds:F2}초 — 가드레일 {TraverseLimitSeconds}초 초과. " +
                    $"최악 출발점은 {source} 다. RG-1 을 다시 계산해야 한다(docs/rg1-recalc-cargo-procurement-v1.md).");
            }
        }

        [Test]
        public void TheThinnestTraverseIsTheCockpitChainAndItStaysWhereTheRecalcPutIt()
        {
            // 재계산 시점의 실측값을 그대로 고정한다. 회랑 둘이 만드는 고리는 경로를 짧게만
            // 만들므로 여기 값은 상한이고, 회랑 좌표가 움직여도 안 흔들린다.
            var worst = WorstTraversePerZone();
            var thinnestZone = (LastShiftZone)0;
            var longestMeters = 0f;
            foreach (var (zone, meters, _) in worst)
                if (meters > longestMeters) (thinnestZone, longestMeters) = (zone, meters);

            Assert.That(thinnestZone, Is.EqualTo(LastShiftZone.Cockpit),
                "최장 이탈 최악이 조종석이 아니다 — 선수 사슬이나 선미 사슬 중 하나가 바뀌었다.");
            Assert.That(longestMeters, Is.EqualTo(30.61f).Within(Tolerance),
                "조종석 구역 최장 이탈 거리가 30.61m 에서 움직였다. RG-1(1) 여유는 1.18배뿐이라 " +
                "구획을 하나 더 잇기 전에 game-balance 재계산이 선행이다.");
            Assert.That(EgressSeconds(longestMeters), Is.EqualTo(8.45f).Within(Tolerance),
                "RG-1(1) 판정값이 8.45초에서 움직였다. 한도 10초까지 남은 보행 거리는 6.19m 뿐이다 " +
                "(docs/rg1-1-measurement-definition-v1.md §3).");
        }

        [Test]
        public void PressureZoneVolumeRatioStaysUnderThree()
        {
            var min = float.MaxValue;
            var max = 0f;
            for (var zone = (LastShiftZone)0; (int)zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                var length = LastShiftShipDimensions.ZoneLength(zone);
                min = Mathf.Min(min, length);
                max = Mathf.Max(max, length);
            }

            Assert.That(max / min, Is.LessThanOrEqualTo(VolumeRatioLimit),
                "RG-1(3) 위반 — EQUALIZE_RATE 를 부피 가중으로 재검토해야 한다.");
            Assert.That(max / min, Is.EqualTo(2.80f).Within(Tolerance),
                "여유가 1.07배뿐이다. 조종석·산소실이 1m 만 커져도 3.00배로 즉시 위반이다.");
        }

        [Test]
        public void AttachedVolumeRatioDoesNotGrowFurther()
        {
            var ratio = AttachedVolumeRatio(includeUnlockable: false);

            Assert.That(ratio, Is.LessThanOrEqualTo(AttachedVolumeRatioRatchet),
                $"부속 구획을 포함한 실 기밀 체적비가 {ratio:F2}배로 재계산 시점(7.98배)보다 벌어졌다. " +
                "판정값(가드레일 3)은 압력존 x 길이비라 이것만으로 위반은 아니지만, 조문 취지와의 " +
                "격차가 커지는 것은 game-balance 가 봐야 한다.");
        }

        // ── 기항 개방 재계산 (docs/rg1-recalc-voyage-port-unlock-v1.md) ──────────────
        //
        // 위 넷은 전부 <b>초기 Access 값만</b> 본다. 기항 개방(voyage-run-structure-v1.md §4.2)이
        // 들어오면 잠긴 셋은 "이번 항해 동안" 열리는 가변 상태가 되고, 그러면 초기값만 세는
        // 것으로는 실제로 플레이되는 최악을 못 잡는다. 아래 넷이 그 열린 상태를 고정한다.

        [Test]
        public void UnlockableCompartmentsDoNotMoveTheWorstTraverse()
        {
            var worst = WorstTraversePerZone(includeUnlockable: true);
            var thinnestZone = (LastShiftZone)0;
            var longestMeters = 0f;
            var source = string.Empty;
            foreach (var (zone, meters, from) in worst)
            {
                var seconds = EgressSeconds(meters);
                Assert.That(seconds, Is.LessThan(TraverseLimitSeconds),
                    $"기항 개방 상태에서 {LastShiftZoneAtlas.ShortLabelOf(zone)} 최장 이탈이 {seconds:F2}초다 — " +
                    $"가드레일 {TraverseLimitSeconds}초 초과. 최악 출발점은 {from} 다.");
                if (meters > longestMeters) (thinnestZone, longestMeters, source) = (zone, meters, from);
            }

            // 셋이 전부 열려도 최악은 안 바뀐다. 셋 다 사슬 연장이 아니라 분기이기 때문이다
            // (재계산 §2.2) — 이 등식이 깨지면 개방이 사슬을 잇는 방식으로 구현된 것이다(§5.2).
            Assert.That(thinnestZone, Is.EqualTo(LastShiftZone.Cockpit),
                $"기항 개방으로 최장 횡단 최악이 조종석에서 {thinnestZone} 로 옮겨갔다 (출발점 {source}).");
            Assert.That(longestMeters, Is.EqualTo(30.61f).Within(Tolerance),
                $"기항 개방 상태의 최장 이탈이 {longestMeters:F2}m 다 — 잠금 상태와 같은 30.61m 여야 한다. " +
                "잠긴 방 하나가 사슬 연장이 됐다는 뜻이고, RG-1(1) 재계산이 선행이다.");
        }

        [Test]
        public void LongestPairInAZoneStaysWhereItIs()
        {
            // W-1 "구역 내 최장 동선" — 같은 구역 안 두 점 사이 최장 거리.
            // <b>RG-1 판정 대상이 아니다. 래칫만 둔다</b>(측정법 v1.1 §2.4).
            //
            // (1) 이 보장하는 것은 SuitOxygen 소모가 멈추는 시점까지의 시간이고 그건 구역
            // 경계에서 멈추므로, 종점이 구역 안인 이 값은 (1) 이 재는 양이 아니다 —
            // docs/rg1-1-measurement-definition-v1.md §2. 그 논증은 배의 모양과 무관하다.
            //
            // <b>v1.1 에서 "이탈 판정 > 쌍 최악" 단언을 지웠다.</b> v1 §2.2 가 현행 배의
            // 8.45 > 8.26 을 조문 선택의 근거처럼 적었고 이 테스트가 그것을 회귀로 못 박았는데,
            // 그건 이 배에서 우연히 성립한 관측이지 조문의 성질이 아니었다. 선수 조종석
            // 도안에서 8.21 < 9.81 로 뒤집힌다(docs/rg1-recalc-bow-cockpit-plaza-v1.md §12.3).
            // <b>조문에 없는 것을 테스트가 막고 있었으므로 값 갈이가 아니라 삭제다</b> — 측정법 §2.2.
            var expected = new (LastShiftZone Zone, float Meters)[]
            {
                (LastShiftZone.Cockpit, 33.03f),      // 관측실 ↔ 격납고. 화물칸 문 하나를 공유하며 반대로 뻗는다
                (LastShiftZone.LifeSupport, 28.47f)   // 구명정 안쪽 구석 → 구역 끝. 이탈 읽기와 같은 지점이다
            };

            foreach (var includeUnlockable in new[] { false, true })
            {
                var pairs = LongestPairPerZone(includeUnlockable);
                foreach (var (zone, meters) in expected)
                    Assert.That(pairs[(int)zone], Is.EqualTo(meters).Within(Tolerance),
                        $"{LastShiftZoneAtlas.ShortLabelOf(zone)} 구역 내 최장 동선(W-1)이 " +
                        $"{pairs[(int)zone]:F2}m 다 (기항 개방 {includeUnlockable}). 래칫 {meters:F2}m 에서 " +
                        "움직였다. 이건 RG-1 위반이 아니라 분기 신호다 — " +
                        "측정법 §2.4 로 가서 (a) 쌍의 양 끝 중 하나라도 RG-1(2) 복구 항목표에 " +
                        "등장하는 구획이면 (2) 최악 복구 경로를 다시 뽑고, (b) 둘 다 항목표 밖이면 " +
                        "래칫만 여기서 갱신하고 그 구역에 새 목적지를 놓지 않는다. " +
                        "(c) 복구 항목이 부속 구획으로 옮겨간 경우는 이 값이 안 움직여도 (2) 를 다시 뽑는다.");
            }
        }

        [Test]
        public void EveryUnlockableCompartmentEgressIsPinned()
        {
            // 방별 이탈 거리. 셋 다 현행 최악(관측실 30.61m)보다 짧다는 것이 개방 승인의 근거다.
            var expected = new (LastShiftCompartment Compartment, float Meters)[]
            {
                (LastShiftCompartment.ServerRoom, 16.32f),
                (LastShiftCompartment.Hydroponics, 14.71f),
                (LastShiftCompartment.MedBay, 25.44f)
            };

            foreach (var (compartment, meters) in expected)
            {
                var spec = LastShiftCompartments.Of(compartment);
                Assert.That(spec.Access, Is.EqualTo(LastShiftCompartmentAccess.Locked),
                    $"{compartment} 이 더 이상 기항 개방 대상이 아니다 — 이 테스트가 세는 대상이 바뀌었다.");

                var actual = EgressMeters(spec);
                Assert.That(actual, Is.EqualTo(meters).Within(Tolerance),
                    $"{compartment} 이탈 거리가 {actual:F2}m 로 재계산값 {meters:F2}m 에서 움직였다 " +
                    "(docs/rg1-recalc-voyage-port-unlock-v1.md §2.1).");
                Assert.That(actual, Is.LessThan(30.61f),
                    $"{compartment} 이탈 거리 {actual:F2}m 가 현행 최악 30.61m 를 넘었다 — " +
                    "개방 목록에서 빼거나 RG-1(1) 을 다시 계산해야 한다.");
            }
        }

        [Test]
        public void UnlockedAttachedVolumeRatioStaysUnderTheRaisedRatchet()
        {
            var ratio = AttachedVolumeRatio(includeUnlockable: true);

            Assert.That(ratio, Is.LessThanOrEqualTo(UnlockedAttachedVolumeRatioRatchet),
                $"기항 개방 상태의 실 기밀 체적 근사비가 {ratio:F2}배로 재계산값 9.21배보다 벌어졌다. " +
                "판정값(가드레일 3, 압력존 x 길이비 2.80배)은 그대로지만 조문 취지와의 격차가 커진다.");
            Assert.That(ratio, Is.EqualTo(9.21f).Within(Tolerance),
                "개방 후 근사비가 9.21 에서 움직였다 — 잠긴 셋 중 하나의 발자국이나 문 위치가 바뀌었다.");
        }

        [Test]
        public void OnlyTheServerRoomWidensTheVolumeGap()
        {
            // 최소가 전력실(96m³)로 고정돼 있어 최대/최소는 조종석이 커질 때만 커진다.
            // 수경재배·의무실은 작은 쪽(산소실)에 붙으므로 선수/선미 격차를 오히려 좁힌다 —
            // voyage-run-structure-v1.md §7 이 반대로 적어 둔 자리다(재계산 §4).
            var baseline = AttachedVolumeRatio(includeUnlockable: false);

            foreach (var compartment in new[] { LastShiftCompartment.Hydroponics, LastShiftCompartment.MedBay })
                Assert.That(AttachedVolumeRatio(compartment), Is.EqualTo(baseline).Within(Tolerance),
                    $"{compartment} 개방이 실 체적비를 움직였다 — 이 방이 붙는 구역이 최대 구역으로 바뀌었다.");

            Assert.That(AttachedVolumeRatio(LastShiftCompartment.ServerRoom), Is.GreaterThan(baseline),
                "서버/통신실 개방이 실 체적비를 안 움직인다 — 이 방이 조종석 구역에서 떨어져 나갔다.");
        }

        /// <summary>
        /// 부속 구획을 포함한 실 기밀 체적의 최대/최소비. 통로는 구역 x 범위에 이미 들어 있지만
        /// 폭이 좁다 — 여기서는 구역 전 길이를 선체 폭으로 재는 상한 근사를 쓴다. 부속 구획
        /// 쪽이 압도적이라 어느 쪽으로 재도 결론이 안 바뀐다.
        /// </summary>
        private static float AttachedVolumeRatio(bool includeUnlockable) =>
            AttachedVolumeRatio(spec => spec.IsPassable || includeUnlockable);

        /// <summary>잠긴 방 하나만 추가로 연 경우의 체적비.</summary>
        private static float AttachedVolumeRatio(LastShiftCompartment unlocked) =>
            AttachedVolumeRatio(spec => spec.IsPassable || spec.Compartment == unlocked);

        private static float AttachedVolumeRatio(System.Func<LastShiftCompartmentSpec, bool> isOpen)
        {
            var hull = new float[LastShiftZoneAtlas.ZoneCount];
            for (var zone = (LastShiftZone)0; (int)zone < LastShiftZoneAtlas.ZoneCount; zone++)
                hull[(int)zone] = LastShiftShipDimensions.ZoneLength(zone)
                                  * LastShiftShipDimensions.InteriorWidth
                                  * LastShiftShipPhysics.CeilingInnerHeight;

            foreach (var spec in LastShiftCompartments.Specs)
            {
                if (!isOpen(spec)) continue;
                var zone = LastShiftZoneAtlas.Resolve(spec.DoorPosition);
                hull[(int)zone] += spec.LengthX * spec.WidthZ * LastShiftCompartments.InteriorHeight;
            }

            var min = float.MaxValue;
            var max = 0f;
            foreach (var volume in hull)
            {
                min = Mathf.Min(min, volume);
                max = Mathf.Max(max, volume);
            }

            return max / min;
        }

        [Test]
        public void NoItemNominalSitsInsideACompartment()
        {
            // RG-1(2) 최악 복구 경로 33.2초(여유 1.21배)가 성립하는 전제다. 예비를 추가하는 것은
            // 대안이 하나 느는 것이라 최악 경로를 안 건드리지만, 초기 배치분을 선수 사슬로
            // "옮기면" 산소실 → 화물칸 → 전력실이 되어 합계 46.5초, 40초 한도를 넘는다.
            var nominals = new (string Name, Vector3 Position)[]
            {
                ("Battery", LastShiftShipDimensions.BatteryNominal),
                ("CoolingCanister", LastShiftShipDimensions.CoolingNominal),
                ("PatchPlate", LastShiftShipDimensions.PatchPlateNominal),
                ("Tether", LastShiftShipDimensions.TetherNominal)
            };

            foreach (var (name, position) in nominals)
            {
                Assert.That(Mathf.Abs(position.x), Is.LessThanOrEqualTo(LastShiftShipDimensions.HalfLength),
                    $"{name} 정위치가 선체 끝벽 밖이다 — 부속 구획 안이라는 뜻이고, RG-1(2) 가 46.5초로 위반이다.");
                Assert.That(Mathf.Abs(position.z), Is.LessThanOrEqualTo(LastShiftShipDimensions.HalfWidth),
                    $"{name} 정위치가 선체 긴 벽 밖이다 — 우현 분기 구획 안이다.");

                foreach (var spec in LastShiftCompartments.Specs)
                    Assert.That(
                        position.x > spec.MinX && position.x < spec.MaxX &&
                        position.z > spec.MinZ && position.z < spec.MaxZ,
                        Is.False,
                        $"{name} 정위치가 {spec.Compartment} 안이다. 예비를 두는 것은 되지만 " +
                        "초기 배치분을 옮기는 것은 RG-1(2) 위반이다(docs/rg1-recalc-cargo-procurement-v1.md §2.2).");
            }
        }

        /// <summary>
        /// 정본 구획표를 판정기 입력으로 옮긴 것. 인덱스가 <see cref="LastShiftCompartments.Specs"/>
        /// 와 같으므로 <c>ParentIndex</c> 가 그대로 산다.
        ///
        /// <b>이 파일이 자를 직접 들고 있지 않은 것이 이 카드의 요점이다.</b> 이탈 거리 계산이
        /// 여기와 <see cref="LastShiftPlazaRg1Tests"/> 에 독립 사본으로 있었고, 둘 다 Editor 전용
        /// 어셈블리 안이라 런타임에서 부를 방법이 없었다. 지금 재는 것은
        /// <see cref="LastShiftPlacementRules"/> 하나이고, <b>이 파일의 고정값이 하나도 안 움직이는
        /// 것이 그 승격이 값을 안 바꿨다는 증거다</b> —
        /// <c>docs/tech/free-placement-runtime-chain-estimate-v1.md</c> §5.3.
        /// </summary>
        private static readonly LastShiftPlacement[] Table =
            LastShiftPlacementRules.TableOf(LastShiftCompartments.Specs);

        /// <summary>
        /// 구역별 최장 횡단 거리. 부속 구획의 가장 먼 구석에서 자기 문 → 부모 문 → … → 선체
        /// 문까지의 사슬 거리에, 선체 문에서 그 구역 반대쪽 끝까지의 스파인 거리를 더한다.
        ///
        /// 잠긴 구획은 그레이박스에서 문이 아니라 메운 판이라(<c>IsPassable</c>) 기본값에서
        /// 빠진다. <paramref name="includeUnlockable"/> 가 기항 개방이 끝난 상태다 —
        /// <c>docs/rg1-recalc-voyage-port-unlock-v1.md</c> §2.
        /// </summary>
        private static List<(LastShiftZone Zone, float Meters, string Source)> WorstTraversePerZone(
            bool includeUnlockable = false)
        {
            var worst = new List<(LastShiftZone, float, string)>();
            foreach (var (zone, meters, index) in
                     LastShiftPlacementRules.WorstEgressPerZone(Table, includeUnlockable))
                worst.Add((zone, meters, index < 0
                    ? "구역 자체"
                    : LastShiftCompartments.Specs[index].Compartment.ToString()));

            return worst;
        }

        /// <summary>
        /// 이탈 거리 → 가드레일 <c>(1)</c> 판정 시간. 압력문 한 번을 상수로 더한다 —
        /// <see cref="PressureDoorSeconds"/> 와 <c>docs/rg1-1-measurement-definition-v1.md</c> §1.1.
        /// </summary>
        private static float EgressSeconds(float meters) => LastShiftPlacementRules.EgressSeconds(meters);

        /// <summary>
        /// 구역별 "같은 구역 안 두 점 사이 최장 거리". <b>가드레일 <c>(1)</c> 판정이 아니라
        /// <c>(2)</c> 재계산 트리거다</b> — 측정법 정본 §2.1.
        ///
        /// 스파인 자는 <see cref="LastShiftPairSpine.AlongLength"/> 다. 이 배는 부속 구획이 전부
        /// <c>z</c> 얕은 자리에 붙어 있어 <c>x</c> 차 근사가 성립하고, 그 자로 잰 값이 여기
        /// 고정돼 있다. 도안 쪽은 <c>z</c> 로 벌어져서 다른 자를 쓴다 —
        /// <see cref="LastShiftPlazaRg1Tests"/>.
        /// </summary>
        private static float[] LongestPairPerZone(bool includeUnlockable) =>
            LastShiftPlacementRules.LongestPairPerZone(
                Table, includeUnlockable, LastShiftPairSpine.AlongLength);

        /// <summary>
        /// 구획 가장 먼 구석에서 자기 구역을 빠져나갈 때까지의 거리. 가드레일 <c>(1)</c> 이 실제로
        /// 재는 것은 구역 소속이 아니라 이 값이다. 정의와 근사의 한계는
        /// <see cref="LastShiftPlacementRules.TryEgress"/> 가 들고 있다.
        /// </summary>
        private static float EgressMeters(LastShiftCompartmentSpec spec)
        {
            Assert.That(
                LastShiftPlacementRules.TryEgress(Table, Table[(int)spec.Compartment], out var meters, out _),
                Is.True, $"{spec.Compartment} 사슬이 선체에 안 닿는다 — 부모 사슬이 끊겼거나 순환이다.");
            return meters;
        }
    }
}
