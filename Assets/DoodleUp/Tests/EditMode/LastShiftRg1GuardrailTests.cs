using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// <c>RG-1</c> 재검토 가드레일 셋(<c>docs/ship-scale-and-density-v1.md</c> §5.4)을 좌표에서
    /// 다시 뽑아 고정한다.
    ///
    /// <b>M-2 가 이 파일의 값을 전부 갈아 끼웠다</b>(<c>docs/core-four-rooms-and-hull-schematic-v1.md</c>
    /// §5). 고정 구획이 열하나에서 하나로 줄면서 예전 래칫이 재던 대상 — 선수 사슬 넷, 선미
    /// 사슬 넷, 잠긴 셋 — 이 통째로 배에서 나갔다. 그 문서 §5.1 이 적은 대로 <b>재계산의
    /// 산출물은 "통과/위반" 이 아니라 새 래칫 수치</b>다.
    ///
    /// <code>
    ///   최악 이탈       30.61m / 8.45s / Cockpit  ->  19.00m / 5.55s / LifeSupport
    ///   W-1             33.03 / 28.47             ->  14.00 / 19.00
    ///   부속 체적비      8.46                      ->  3.55
    ///   개방 후 체적비   9.21                      ->  (없다 — 조항 K-2 로 개방 계열이 폐지됐다)
    /// </code>
    ///
    /// <b>래칫이 둘에서 하나로 줄었다.</b> <c>UnlockedAttachedVolumeRatioRatchet</c> 은 "기항
    /// 개방이 끝난 상태" 를 쟀는데, 개방 대상이 <c>0</c> 개가 되어(조항 K-2) 잴 상태 자체가
    /// 없어졌다 — 같은 이유로 개방 전후를 가르던 테스트 넷이 여기서 빠졌다.
    ///
    /// <b>그리고 이 파일이 재는 것이 이제 배의 전부가 아니다.</b> 실제 체적은 플레이어가 항해
    /// 중에 만들고, 정적 구획표를 재는 래칫은 구조상 그것을 못 본다(§5.4). 그 구멍을 닫는 것이
    /// <c>RG-1(3)</c> 실 기밀 체적 개정(M-3)이고, 그때까지 배치 후 상태를 보는 것은
    /// <see cref="LastShiftPlacementRules.Evaluate"/> 하나다.
    /// </summary>
    public sealed class LastShiftRg1GuardrailTests
    {
        /// <summary>가드레일 (1). 한 구역에서 구역 밖으로 나가는 최악 시간의 한도.</summary>
        private const float TraverseLimitSeconds = 10f;

        /// <summary>가드레일 (3). 압력존 부피비 한도.</summary>
        private const float VolumeRatioLimit = 3f;

        /// <summary>
        /// 부속 구획까지 포함한 실 기밀 체적비의 현재값 래칫. <b>한도가 아니라 현재값이다.</b>
        ///
        /// <b>M-2 에서 <c>8.5 → 3.6</c> 이 됐다.</b> 예전 값은 조종석 쪽에 <c>342m²</c> 가
        /// 붙어 있을 때의 것이고, 지금 붙어 있는 것은 산소실 쪽 숙소 <c>24m²</c> 하나뿐이다 —
        /// 실측 <c>3.55</c>(§5.4 의 표와 같은 값)에서 한 눈금 위로 둔다.
        ///
        /// <b>래칫을 실측 바로 위에 두는 것이 이 값의 용도다.</b> 예전 <c>8.5</c> 는 실측의
        /// <c>2.4</c>배 위에 떠 있어 아무것도 안 지키는 값이었다. 고정 표에 방을 하나라도
        /// 되돌리면 여기서 먼저 걸려야 한다.
        /// </summary>
        private const float AttachedVolumeRatioRatchet = 3.6f;

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
                    $"최악 출발점은 {source} 다. RG-1 을 다시 계산해야 한다(docs/core-four-rooms-and-hull-schematic-v1.md §5).");
            }
        }

        [Test]
        public void TheWorstTraverseIsTheQuartersAndItStaysWhereM2PutIt()
        {
            // <b>최악이 조종석에서 산소실로 옮겨갔다.</b> 조종석 쪽에 붙어 있던 사슬 넷이
            // 통째로 빠지면서 그 구역의 최악이 "구역 자체의 x 길이 14m" 로 내려앉았고,
            // 유일하게 남은 부속 방(숙소)이 산소실 끝벽에 붙어 있다.
            var worst = WorstTraversePerZone();
            var thinnestZone = (LastShiftZone)0;
            var longestMeters = 0f;
            foreach (var (zone, meters, _) in worst)
                if (meters > longestMeters) (thinnestZone, longestMeters) = (zone, meters);

            Assert.That(thinnestZone, Is.EqualTo(LastShiftZone.LifeSupport),
                "최장 이탈 최악이 산소실이 아니다 — 고정 표에 방이 하나 더 붙었거나 숙소가 옮겨갔다.");
            Assert.That(longestMeters, Is.EqualTo(19.00f).Within(Tolerance),
                "산소실 구역 최장 이탈 거리가 19.00m 에서 움직였다. 숙소 먼 구석 → 자기 문 5.00m 에 " +
                "선미 끝벽 → 구역 경계 14.00m 를 더한 값이다(조항 S-2).");
            Assert.That(EgressSeconds(longestMeters), Is.EqualTo(5.55f).Within(Tolerance),
                "RG-1(1) 판정값이 5.55초에서 움직였다. 한도 10초까지 남은 보행 거리는 17.80m 다 — " +
                "여유가 1.18배에서 1.79배로 늘었고, 그 여유를 쓰는 것은 이제 플레이어의 배치다.");

            // 조종석 구역에는 이제 붙은 방이 하나도 없다. 최악이 구역 자체라는 것이
            // "배가 실제로 비어서 출항한다"(§8 M-2 확인 항목)의 코드 쪽 증거다.
            foreach (var (zone, meters, source) in worst)
            {
                if (zone != LastShiftZone.Cockpit) continue;
                Assert.That(source, Is.EqualTo("구역 자체"),
                    $"조종석 구역 최악이 {source} 다 — 선수 쪽에 방이 다시 붙었다.");
                Assert.That(meters,
                    Is.EqualTo(LastShiftShipDimensions.ZoneLength(LastShiftZone.Cockpit)).Within(Tolerance));
            }
        }

        [Test]
        public void PressureZoneVolumeRatioStaysUnderThree()
        {
            // 압력 스파인은 M-2 대상이 아니다(조항 S-1) — 이 값은 개편 전후로 안 움직인다.
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
            var ratio = AttachedVolumeRatio();

            Assert.That(ratio, Is.LessThanOrEqualTo(AttachedVolumeRatioRatchet),
                $"부속 구획을 포함한 실 기밀 체적비가 {ratio:F2}배로 M-2 실측(3.55배)보다 벌어졌다. " +
                "판정값(가드레일 3)은 압력존 x 길이비라 이것만으로 위반은 아니지만, 고정 표에 " +
                "방이 되돌아왔다는 뜻이므로 이관 결정 자체를 다시 봐야 한다.");
            Assert.That(ratio, Is.EqualTo(3.55f).Within(Tolerance),
                "M-2 실측 3.55배에서 움직였다 — 숙소 발자국이나 붙는 구역이 바뀌었다.");
        }

        [Test]
        public void TheOnlyAttachedVolumeIsOnTheLifeSupportSide()
        {
            // 조종석 쪽 부속이 0 이라는 것이 §5.4 표의 셋째 줄이다. 이게 깨지면 위 비율이
            // 3.55 에서 왜 움직였는지가 안 갈린다 — 숙소가 커진 것인지 새 방이 붙은 것인지.
            var attached = new float[LastShiftZoneAtlas.ZoneCount];
            foreach (var spec in LastShiftCompartments.FixedSpecs)
                attached[(int)LastShiftZoneAtlas.Resolve(spec.DoorPosition)] +=
                    spec.LengthX * spec.WidthZ;

            Assert.That(attached[(int)LastShiftZone.Cockpit], Is.Zero,
                "조종석 구역에 붙은 고정 방이 있다 — 선수 사슬이 통째로 이관됐어야 한다.");
            Assert.That(attached[(int)LastShiftZone.LifeSupport], Is.EqualTo(24f).Within(Tolerance),
                "산소실 쪽 부속 발자국이 숙소 24m2 가 아니다.");
        }

        [Test]
        public void LongestPairInAZoneStaysWhereItIs()
        {
            // W-1 "구역 내 최장 동선" — 같은 구역 안 두 점 사이 최장 거리.
            // <b>RG-1 판정 대상이 아니다. 래칫만 둔다</b>(측정법 v1.1 §2.4).
            //
            // <b>시작 배 값이 급락했다</b>(§5.5). 부속이 숙소 하나뿐이라 "같은 구역에 붙은 배치
            // 둘" 이라는 쌍 자체가 없고, 남는 후보는 구역 x 길이와 숙소 이탈값뿐이다.
            //
            // <b>그래서 이 래칫은 이제 상한이 아니라 바닥이다.</b> W-1 을 실제로 밀어 올리는
            // 것은 플레이어의 배치이고(§5.5), 그 상한은 game-balance 몫으로 열려 있다(B-3).
            // 여기서 지키는 것은 "시작 배가 이 값에서 안 움직인다" 하나다.
            var expected = new (LastShiftZone Zone, float Meters)[]
            {
                (LastShiftZone.Cockpit, 14.00f),      // 붙은 방이 없다 — 구역 x 길이 그 자체다
                (LastShiftZone.LifeSupport, 19.00f)   // 숙소 안쪽 구석 → 구역 끝. 이탈 읽기와 같은 지점이다
            };

            var pairs = LongestPairPerZone();
            foreach (var (zone, meters) in expected)
                Assert.That(pairs[(int)zone], Is.EqualTo(meters).Within(Tolerance),
                    $"{LastShiftZoneAtlas.ShortLabelOf(zone)} 구역 내 최장 동선(W-1)이 " +
                    $"{pairs[(int)zone]:F2}m 다. 래칫 {meters:F2}m 에서 움직였다. 이건 RG-1 위반이 " +
                    "아니라 분기 신호다 — 측정법 §2.4 로 가서 (a) 쌍의 양 끝 중 하나라도 RG-1(2) " +
                    "복구 항목표에 등장하는 구획이면 (2) 최악 복구 경로를 다시 뽑고, (b) 둘 다 " +
                    "항목표 밖이면 래칫만 여기서 갱신한다.");
        }

        /// <summary>
        /// 부속 구획을 포함한 실 기밀 체적의 최대/최소비. 통로는 구역 x 범위에 이미 들어 있지만
        /// 폭이 좁다 — 여기서는 구역 전 길이를 선체 폭으로 재는 상한 근사를 쓴다.
        ///
        /// <b><c>includeUnlockable</c> 매개변수가 빠졌다.</b> 잠긴 구획이 <c>0</c> 개라
        /// (조항 K-2) 열고 닫을 상태가 없다.
        /// </summary>
        private static float AttachedVolumeRatio()
        {
            var hull = new float[LastShiftZoneAtlas.ZoneCount];
            for (var zone = (LastShiftZone)0; (int)zone < LastShiftZoneAtlas.ZoneCount; zone++)
                hull[(int)zone] = LastShiftShipDimensions.ZoneLength(zone)
                                  * LastShiftShipDimensions.InteriorWidth
                                  * LastShiftShipPhysics.CeilingInnerHeight;

            foreach (var spec in LastShiftCompartments.Specs)
            {
                if (!spec.IsPassable) continue;
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
            // RG-1(2) 최악 복구 경로가 성립하는 전제다. 항목표는 본선 좌표만 쓰고 부속 구획을
            // 하나도 안 지나므로 M-2 로 시작 배 값이 안 움직인다(§5.3) — 그 불변을 지키는 것이
            // "정위치가 부속 구획 안에 없다" 이 한 줄이다.
            //
            // <b>M-2 가 이 검사에 항목 하나를 되살린다.</b> 화물칸 예비 배터리 승인은 화물칸이
            // 시작 배에 없으므로 대상이 사라졌고(§5.3-1), 격리의 대가가 복원됐다.
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
                    $"{name} 정위치가 선체 끝벽 밖이다 — 부속 구획 안이라는 뜻이고, RG-1(2) 가 위반이다.");
                Assert.That(Mathf.Abs(position.z), Is.LessThanOrEqualTo(LastShiftShipDimensions.HalfWidth),
                    $"{name} 정위치가 선체 긴 벽 밖이다 — 우현 분기 구획 안이다.");

                foreach (var spec in LastShiftCompartments.Specs)
                    Assert.That(
                        position.x > spec.MinX && position.x < spec.MaxX &&
                        position.z > spec.MinZ && position.z < spec.MaxZ,
                        Is.False,
                        $"{name} 정위치가 {spec.Compartment} 안이다. 예비를 두는 것은 되지만 " +
                        "초기 배치분을 옮기는 것은 RG-1(2) 위반이다.");
            }
        }

        /// <summary>
        /// 정본 구획표를 판정기 입력으로 옮긴 것. 인덱스가 <see cref="LastShiftCompartments.Specs"/>
        /// 와 같으므로 <c>ParentIndex</c> 가 그대로 산다.
        /// </summary>
        private static readonly LastShiftPlacement[] Table =
            LastShiftPlacementRules.TableOf(LastShiftCompartments.Specs);

        /// <summary>
        /// 구역별 최장 횡단 거리. 부속 구획의 가장 먼 구석에서 자기 문 → 선체 문까지의 사슬
        /// 거리에, 선체 문에서 그 구역 반대쪽 끝까지의 스파인 거리를 더한다.
        /// </summary>
        private static List<(LastShiftZone Zone, float Meters, string Source)> WorstTraversePerZone()
        {
            var worst = new List<(LastShiftZone, float, string)>();
            foreach (var (zone, meters, index) in
                     LastShiftPlacementRules.WorstEgressPerZone(Table, includeImpassable: false))
                worst.Add((zone, meters, index < 0
                    ? "구역 자체"
                    : LastShiftCompartments.Specs[index].Compartment.ToString()));

            return worst;
        }

        /// <summary>이탈 거리 → 가드레일 <c>(1)</c> 판정 시간. 압력문 한 번을 상수로 더한다.</summary>
        private static float EgressSeconds(float meters) => LastShiftPlacementRules.EgressSeconds(meters);

        /// <summary>
        /// 구역별 "같은 구역 안 두 점 사이 최장 거리". <b>가드레일 <c>(1)</c> 판정이 아니라
        /// <c>(2)</c> 재계산 트리거다</b> — 측정법 정본 §2.1.
        /// </summary>
        private static float[] LongestPairPerZone() =>
            LastShiftPlacementRules.LongestPairPerZone(
                Table, includeImpassable: false, LastShiftPairSpine.AlongLength);
    }
}
