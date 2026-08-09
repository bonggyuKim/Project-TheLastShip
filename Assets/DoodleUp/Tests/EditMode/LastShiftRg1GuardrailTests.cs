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
        public void TheWorstEgressIsTheCockpitZoneAndItStaysWhereThePlazaPutIt()
        {
            // §9.4 재래칫. <b>최악 구역이 산소실에서 조종석으로 옮겨왔다</b> — 조종석 구역이
            // 광장·조종석 방·에어록 홀·숙소 넷의 합집합이라 자기 문을 지나 광장을 가로지르는
            // 사슬이 유일하게 여기만 남았고, 나머지 셋은 단칸이라 자기 문이 곧 이탈구다.
            var worst = LastShiftZone.Cockpit;
            var worstMeters = 0f;
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                var meters = LastShiftPlazaLayout.WorstEgressMeters((LastShiftZone)zone);
                if (meters <= worstMeters) continue;
                worstMeters = meters;
                worst = (LastShiftZone)zone;
            }

            Assert.That(worst, Is.EqualTo(LastShiftZone.Cockpit),
                "최악 이탈이 조종석 구역이 아니다 — 사슬 깊이가 1 인 방에 경유가 생겼다.");
            Assert.That(worstMeters, Is.EqualTo(17.03f).Within(0.01f),
                "최악 이탈 거리가 17.03m 에서 움직였다(§9.4). 조종석 방 선수 구석 → 개구부 → " +
                "전력실 문이 그 경로다.");
            Assert.That(EgressSeconds(worstMeters), Is.EqualTo(5.06f).Within(0.01f),
                "RG-1(1) 판정값이 5.06초에서 움직였다. 한도 10초까지 남은 여유가 4.94초다.");
            Assert.That(EgressSeconds(worstMeters),
                Is.LessThanOrEqualTo(LastShiftPlacementRules.TraverseLimitSeconds));
        }

        [Test]
        public void SingleRoomZonesEgressThroughTheirOwnDoor()
        {
            // §9.4 의 구역별 표. <b>전력실·냉각실 5.83m 는 §6.1 표에 없던 값이다</b> — 단칸
            // 구역은 자기 문이 곧 이탈구라 광장을 안 지난다. 문이 광장 변에서 떨어지는 순간
            // 이 값이 두 배로 뛰므로, 여기가 "직결" 을 수치로 지키는 자리다.
            var expected = new (LastShiftZone Zone, float Meters)[]
            {
                (LastShiftZone.Power, 5.83f),
                (LastShiftZone.Cooling, 5.83f),
                (LastShiftZone.LifeSupport, 8.54f)
            };

            foreach (var (zone, meters) in expected)
                Assert.That(LastShiftPlazaLayout.WorstEgressMeters(zone), Is.EqualTo(meters).Within(0.01f),
                    $"{zone} 최악 이탈이 {meters:F2}m 에서 움직였다 — 방이 커졌거나 문이 광장 변에서 떨어졌다.");
        }

        [Test]
        public void StaticFootprintRatioStaysWellUnderThree()
        {
            // RG-1(3) 정적 발자국 기준. §9.4 실측 <c>1.60배</c>(<c>48 / 30</c>) —
            // 조종석 방 <c>8x6 = 48</c>, 전력실·냉각실 <c>6x5 = 30</c>.
            //
            // <b>부피가 아니라 발자국이다.</b> 방사형에서 구역을 x 밴드 길이로 재던 옛 척도는
            // 뜻을 잃었다 — 조종석 구역이 x 로 23m 를 걸치지만 그중 대부분이 다른 방이다.
            var min = float.MaxValue;
            var max = 0f;
            foreach (var footprint in LastShiftPlazaLayout.Footprints)
            {
                if (footprint.Space == LastShiftPlazaSpace.Plaza) continue;
                if (footprint.Zone == LastShiftZone.Cockpit &&
                    footprint.Space != LastShiftPlazaSpace.CockpitRoom) continue;
                min = Mathf.Min(min, footprint.Area);
                max = Mathf.Max(max, footprint.Area);
            }

            Assert.That(max / min, Is.EqualTo(1.60f).Within(0.01f),
                "정적 발자국비가 1.60배에서 움직였다(§9.4).");
            Assert.That(max / min, Is.LessThanOrEqualTo(3f),
                "RG-1(3) 위반 — EQUALIZE_RATE 를 부피 가중으로 재검토해야 한다.");
        }

        [Test]
        public void RealAirtightVolumeRatioIsRatchetedNotBounded()
        {
            // §9.4 실측 <c>8.80배</c>(<c>264 / 30</c>). <b>한도가 아니라 래칫이다</b> —
            // 조종석 구역이 넷의 합집합이라 실 기밀 체적으로 재면 <c>3배</c>를 훌쩍 넘고,
            // 그것이 §3.3 이 적어 둔 회피다. 이 값을 상한으로 걸면 배치가 성립하지 않으므로,
            // 여기서는 <b>더 벌어지지 않는지</b>만 지킨다. 닫는 것은 <c>B-2</c> 몫이다.
            // §9.4 의 <c>264 / 30</c> 은 발자국 <b>면적</b> 합이다 — 천장을 곱하면
            // 본선 3.2 / 부속 3.0 이 섞여 조종석 구역만 살짝 낮아지고(8.65) 문서 값과 갈린다.
            var byZone = new float[LastShiftZoneAtlas.ZoneCount];
            foreach (var footprint in LastShiftPlazaLayout.Footprints)
                byZone[(int)footprint.Zone] += footprint.Area;

            var min = float.MaxValue;
            var max = 0f;
            foreach (var volume in byZone)
            {
                min = Mathf.Min(min, volume);
                max = Mathf.Max(max, volume);
            }

            var ratio = max / min;
            Assert.That(ratio, Is.EqualTo(8.80f).Within(0.05f),
                $"실 기밀 체적비가 {ratio:F2}배로 §9.4 실측 8.80배에서 움직였다. 방이 커졌거나 " +
                "구역 소속이 바뀌었다는 뜻이므로 §3.3 회피의 크기를 다시 봐야 한다.");
            Assert.That(ratio, Is.LessThanOrEqualTo(AttachedVolumeRatioRatchet),
                "실 기밀 체적비가 래칫 위로 벌어졌다 — B-2 가 닫기 전에 더 벌리지 않는다.");
        }

        [Test]
        public void EveryZoneStaysUnderTheEgressLimit()
        {
            // 구역 넷 전부가 RG-1(1) 한도 안이어야 한다. 최악 하나만 보면 새로 생긴 구역이
            // 조용히 한도를 넘어도 안 보인다.
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                var meters = LastShiftPlazaLayout.WorstEgressMeters((LastShiftZone)zone);
                Assert.That(EgressSeconds(meters),
                    Is.LessThanOrEqualTo(LastShiftPlacementRules.TraverseLimitSeconds),
                    $"{(LastShiftZone)zone} 이탈이 한도를 넘는다 — {meters:F2}m.");
            }
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
