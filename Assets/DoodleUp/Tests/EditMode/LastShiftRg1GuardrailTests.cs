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
    /// </summary>
    public sealed class LastShiftRg1GuardrailTests
    {
        /// <summary>가드레일 (1). 한 구역 내부 최장 횡단 한도.</summary>
        private const float TraverseLimitSeconds = 10f;

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
        /// </summary>
        private const float AttachedVolumeRatioRatchet = 8.5f;

        private const float Tolerance = 0.01f;

        [Test]
        public void AttachedCompartmentsKeepEveryZoneTraverseUnderTheLimit()
        {
            var worst = WorstTraversePerZone();

            foreach (var (zone, meters, source) in worst)
            {
                var seconds = meters / LastShiftPlayerController.MoveSpeed;
                Assert.That(seconds, Is.LessThan(TraverseLimitSeconds),
                    $"{LastShiftZoneAtlas.ShortLabelOf(zone)} 최장 횡단 {seconds:F2}초 — 가드레일 {TraverseLimitSeconds}초 초과. " +
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
                "최장 횡단 최악이 조종석이 아니다 — 선수 사슬이나 선미 사슬 중 하나가 바뀌었다.");
            Assert.That(longestMeters, Is.EqualTo(30.61f).Within(Tolerance),
                "조종석 구역 최장 횡단 거리가 30.61m 에서 움직였다. RG-1(1) 여유는 1.31배뿐이라 " +
                "구획을 하나 더 잇기 전에 game-balance 재계산이 선행이다.");
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
            var hull = new float[LastShiftZoneAtlas.ZoneCount];
            for (var zone = (LastShiftZone)0; (int)zone < LastShiftZoneAtlas.ZoneCount; zone++)
                hull[(int)zone] = LastShiftShipDimensions.ZoneLength(zone)
                                  * LastShiftShipDimensions.InteriorWidth
                                  * LastShiftShipPhysics.CeilingInnerHeight;

            // 통로는 구역 x 범위에 이미 들어 있지만 폭이 좁다. 여기서는 구역 전 길이를 선체
            // 폭으로 재는 상한 근사를 쓴다 — 부속 구획 쪽이 압도적이라 결론이 안 바뀐다.
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

            Assert.That(max / min, Is.LessThanOrEqualTo(AttachedVolumeRatioRatchet),
                $"부속 구획을 포함한 실 기밀 체적비가 {max / min:F2}배로 재계산 시점(7.98배)보다 벌어졌다. " +
                "판정값(가드레일 3)은 압력존 x 길이비라 이것만으로 위반은 아니지만, 조문 취지와의 " +
                "격차가 커지는 것은 game-balance 가 봐야 한다.");
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
        /// 구역별 최장 횡단 거리. 부속 구획의 가장 먼 구석에서 자기 문 → 부모 문 → … → 선체
        /// 문까지의 사슬 거리에, 선체 문에서 그 구역 반대쪽 끝까지의 스파인 거리를 더한다.
        ///
        /// 잠긴 구획은 그레이박스에서 문이 아니라 메운 판이라(<c>IsPassable</c>) 빠진다 —
        /// 언락 설계가 되살아나면 그때 이 테스트가 자동으로 다시 센다.
        /// </summary>
        private static List<(LastShiftZone Zone, float Meters, string Source)> WorstTraversePerZone()
        {
            var worst = new List<(LastShiftZone, float, string)>();
            for (var zone = (LastShiftZone)0; (int)zone < LastShiftZoneAtlas.ZoneCount; zone++)
                worst.Add((zone, LastShiftShipDimensions.ZoneLength(zone), "구역 자체"));

            foreach (var spec in LastShiftCompartments.Specs)
            {
                if (!spec.IsPassable) continue;

                var (chain, hullDoor) = ChainToHull(spec);
                var zone = LastShiftZoneAtlas.Resolve(hullDoor);
                var spine = Mathf.Max(
                    Mathf.Abs(hullDoor.x - LastShiftShipDimensions.ZoneMinX(zone)),
                    Mathf.Abs(hullDoor.x - LastShiftShipDimensions.ZoneMaxX(zone)));

                var total = chain + spine;
                if (total > worst[(int)zone].Item2)
                    worst[(int)zone] = (zone, total, spec.Compartment.ToString());
            }

            return worst;
        }

        /// <summary>가장 먼 구석에서 선체에 붙는 문까지의 사슬 거리와, 그 선체 문 좌표.</summary>
        private static (float Meters, Vector3 HullDoor) ChainToHull(LastShiftCompartmentSpec spec)
        {
            var door = Flatten(spec.DoorPosition);
            var meters = 0f;
            foreach (var x in new[] { spec.MinX, spec.MaxX })
            foreach (var z in new[] { spec.MinZ, spec.MaxZ })
                meters = Mathf.Max(meters, Vector3.Distance(new Vector3(x, 0f, z), door));

            var current = spec;
            while (current.ParentIndex >= 0)
            {
                var parent = LastShiftCompartments.Specs[current.ParentIndex];
                meters += Vector3.Distance(Flatten(current.DoorPosition), Flatten(parent.DoorPosition));
                current = parent;
            }

            return (meters, Flatten(current.DoorPosition));
        }

        private static Vector3 Flatten(Vector3 position) => new(position.x, 0f, position.z);
    }
}
