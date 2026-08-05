using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 구역 식별자. 기획 v0.3 §2.2 A-2 가 압력을 배 전체 단일 값에서 구역별 값으로 바꾸면서
    /// 필요해졌다. 문서의 조종석/엔진실/산소실이 씬의 Zone_Cockpit / Zone_UtilityCorridor /
    /// Zone_LifeSupport 에 각각 대응한다.
    /// </summary>
    public enum LastShiftZone
    {
        Cockpit = 0,
        Utility = 1,
        LifeSupport = 2
    }

    /// <summary>
    /// 위치 → 구역 매핑의 정본. 이 매핑이 두 벌이 되면 손상 표시와 진공 판정이 서로 다른
    /// 구역을 가리키게 되므로 <see cref="LastShiftImpactFeedback.ResolveDamagedZone"/> 도
    /// 여기를 거친다.
    ///
    /// 구역 경계는 씬의 벌크헤드 위치와 같다. 경계는 둘이고(조종석↔엔진실, 엔진실↔산소실)
    /// 각 경계에 문이 하나씩 붙는다.
    ///
    /// 통로가 한 구역에 통째로 붙지 않는 것이 여기서 나온다 — 판정 기준 x 가 문이 놓인 경계
    /// 평면과 <b>같은 값</b>이므로 문 앞 공간은 자동으로 양쪽에 반씩 갈린다. 부피 불균형이
    /// 생기지 않으므로 평준화율을 부피 가중으로 바꿀 필요가 없다.
    /// </summary>
    public static class LastShiftZoneAtlas
    {
        public const int ZoneCount = 3;

        /// <summary>인접 구역 쌍의 수. 구역이 일렬로 셋이므로 경계는 둘이다.</summary>
        public const int BoundaryCount = 2;

        /// <summary>구역 판정 기준 x 경계. 치수 정본(<see cref="LastShiftShipDimensions"/>)에서 파생한다.</summary>
        public const float CockpitMaxX = -LastShiftShipDimensions.ZoneBoundaryX;
        public const float LifeSupportMinX = LastShiftShipDimensions.ZoneBoundaryX;

        public static LastShiftZone Resolve(Vector3 position)
        {
            if (position.x <= CockpitMaxX) return LastShiftZone.Cockpit;
            if (position.x >= LifeSupportMinX) return LastShiftZone.LifeSupport;
            return LastShiftZone.Utility;
        }

        public static string NameOf(LastShiftZone zone)
        {
            return zone switch
            {
                LastShiftZone.Cockpit => LastShiftSceneZones.CockpitZoneName,
                LastShiftZone.LifeSupport => LastShiftSceneZones.LifeSupportZoneName,
                _ => LastShiftSceneZones.UtilityZoneName
            };
        }

        /// <summary>HUD 3칸에 쓰는 짧은 이름. 문서 용어(조종석/엔진실/산소실)를 그대로 쓴다.</summary>
        public static string ShortLabelOf(LastShiftZone zone)
        {
            return zone switch
            {
                LastShiftZone.Cockpit => "조종석",
                LastShiftZone.LifeSupport => "산소실",
                _ => "엔진실"
            };
        }

        public static bool TryResolveName(string zoneName, out LastShiftZone zone)
        {
            if (zoneName == LastShiftSceneZones.CockpitZoneName) { zone = LastShiftZone.Cockpit; return true; }
            if (zoneName == LastShiftSceneZones.UtilityZoneName) { zone = LastShiftZone.Utility; return true; }
            if (zoneName == LastShiftSceneZones.LifeSupportZoneName) { zone = LastShiftZone.LifeSupport; return true; }
            zone = LastShiftZone.Utility;
            return false;
        }

        /// <summary>경계 index 의 낮은 쪽 구역. 0 = 조종석↔엔진실, 1 = 엔진실↔산소실.</summary>
        public static LastShiftZone LowZoneOf(int boundary) => boundary <= 0 ? LastShiftZone.Cockpit : LastShiftZone.Utility;

        public static LastShiftZone HighZoneOf(int boundary) => boundary <= 0 ? LastShiftZone.Utility : LastShiftZone.LifeSupport;

        /// <summary>경계가 놓인 x. 벌크헤드/문 배치와 같은 값이어야 한다.</summary>
        public static float BoundaryX(int boundary) => boundary <= 0 ? CockpitMaxX : LifeSupportMinX;

        /// <summary>이 위치에서 가장 가까운 경계. 문 조작 프롬프트의 대상 판정에 쓴다.</summary>
        public static int NearestBoundary(Vector3 position)
        {
            return Mathf.Abs(position.x - BoundaryX(0)) <= Mathf.Abs(position.x - BoundaryX(1)) ? 0 : 1;
        }
    }

    /// <summary>
    /// 두 경계의 문이 열려 있는지. 문이 닫히면 그 경계의 압력 교환이 0 이 된다(기획 §2.2.1).
    /// 문 자체의 개폐 애니메이션·판정은 <see cref="LastShiftZoneDoor"/> 가 갖고, 여기는
    /// 평준화 계산이 읽는 스냅샷만 담는다.
    /// </summary>
    public struct LastShiftDoorState
    {
        public bool CockpitUtilityOpen;
        public bool UtilityLifeSupportOpen;

        /// <summary>문이 아직 없는 구성(N0b 이전, EditMode 최소 조립)의 기본값은 전부 열림이다.</summary>
        public static LastShiftDoorState AllOpen => new() { CockpitUtilityOpen = true, UtilityLifeSupportOpen = true };

        public bool this[int boundary]
        {
            get => boundary <= 0 ? CockpitUtilityOpen : UtilityLifeSupportOpen;
            set
            {
                if (boundary <= 0) CockpitUtilityOpen = value;
                else UtilityLifeSupportOpen = value;
            }
        }

        public bool Equals(LastShiftDoorState other) =>
            CockpitUtilityOpen == other.CockpitUtilityOpen && UtilityLifeSupportOpen == other.UtilityLifeSupportOpen;
    }

    /// <summary>
    /// 구역 세 개의 산소 압력. 기획 §2.2 A-2 의 <c>ZonePressure[zone]</c> 정본이다.
    ///
    /// <see cref="LastShiftShipState.OxygenPressure"/> 는 이 값을 대체하지 않고 <b>조종석 압력의
    /// 파생값</b>으로 남는다. 도킹 성공 판정(§2.2 "도킹 성공 판정은 조종석 압력으로 본다")과
    /// leak 점수·네트워크 스냅샷이 이미 그 필드를 읽고 있으며, 세 값의 평균으로 바꾸면
    /// "산소실을 버리고 평균을 맞추기" 라는 산수가 생겨 격리가 판단이 아니게 된다.
    /// </summary>
    public struct LastShiftZonePressures
    {
        public float Cockpit;
        public float Utility;
        public float LifeSupport;

        public LastShiftZonePressures(float cockpit, float utility, float lifeSupport)
        {
            Cockpit = cockpit;
            Utility = utility;
            LifeSupport = lifeSupport;
        }

        public static LastShiftZonePressures Uniform(float pressure) => new(pressure, pressure, pressure);

        /// <summary>
        /// 평준화 적분 한 걸음의 최대 길이. 테스트가 <c>AdvanceMission(80f)</c> 처럼 크게 밀어도
        /// 결과가 1초씩 민 것과 거의 같아야 "튜닝 값이 곧 관측 결과" 라는 성질이 유지된다.
        /// </summary>
        private const float MaxEqualizeStepSeconds = 0.25f;

        /// <summary>도킹 타이머 300초를 한 번에 미는 호출도 있으므로 걸음 수에 상한을 둔다.</summary>
        private const int MaxEqualizeSteps = 256;

        public float this[LastShiftZone zone]
        {
            get => zone switch
            {
                LastShiftZone.Cockpit => Cockpit,
                LastShiftZone.LifeSupport => LifeSupport,
                _ => Utility
            };
            set
            {
                switch (zone)
                {
                    case LastShiftZone.Cockpit: Cockpit = Mathf.Clamp01(value); break;
                    case LastShiftZone.LifeSupport: LifeSupport = Mathf.Clamp01(value); break;
                    default: Utility = Mathf.Clamp01(value); break;
                }
            }
        }

        /// <summary>가장 낮은 구역 압력. 사이렌은 "어느 구역이든 0.15 이하" 이므로 이 값을 본다(§2.2 A-2 연쇄).</summary>
        public float Lowest => Mathf.Min(Cockpit, Mathf.Min(Utility, LifeSupport));

        public void SetAll(float pressure)
        {
            var clamped = Mathf.Clamp01(pressure);
            Cockpit = clamped;
            Utility = clamped;
            LifeSupport = clamped;
        }

        /// <summary>
        /// 문이 열린 두 구역의 압력을 서로 접근시킨다(§2.2.1).
        ///
        /// <code>
        /// 차이 = A - B
        /// 두 구역이 차이의 EqualizeRatePerSecond 배만큼 서로 접근한다
        /// </code>
        ///
        /// 즉 1초에 <b>차이가</b> 0.08 만큼 줄고 각 구역은 그 절반씩 움직인다. 한쪽만
        /// 0.08 씩 움직이게 하면 두 구역이 만나는 데 걸리는 시간이 문서의 "차 0.5 → 약 28초"
        /// 와 어긋난다.
        /// </summary>
        public void Equalize(in LastShiftDoorState doors, float deltaTime)
        {
            if (deltaTime <= 0f) return;

            // 큰 걸음 하나로 풀지 않고 잘게 나눠 적분한다. 구역이 일렬로 셋이라 압력은 경계를
            // 한 번에 하나씩 건넌다: 산소실이 뚫린 직후 조종석↔엔진실 경계의 압력차는 아직
            // 정확히 0 이므로, dt=1s 를 한 걸음으로 처리하면 조종석이 1초 동안 "정확히"
            // 움직이지 않는다. 연속 시간에서는 2차항으로 분명히 내려가는 값이고, 이건 물리가
            // 아니라 이산화 오차다. 파공에서 두 구역 떨어진 조종석의 하강이 통째로 사라진다.
            var steps = Mathf.Clamp(Mathf.CeilToInt(deltaTime / MaxEqualizeStepSeconds), 1, MaxEqualizeSteps);
            var stepSeconds = deltaTime / steps;
            var ratio = Mathf.Clamp01(LastShiftRecoveryTuning.ZoneEqualizeRatePerSecond * stepSeconds);
            for (var step = 0; step < steps; step++)
            {
                for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                {
                    if (!doors[boundary]) continue;
                    var low = LastShiftZoneAtlas.LowZoneOf(boundary);
                    var high = LastShiftZoneAtlas.HighZoneOf(boundary);
                    var difference = this[low] - this[high];
                    if (Mathf.Abs(difference) <= Mathf.Epsilon) continue;
                    var move = difference * ratio * 0.5f;
                    this[low] -= move;
                    this[high] += move;
                }
            }
        }

        public bool Equals(LastShiftZonePressures other) =>
            Cockpit.Equals(other.Cockpit) && Utility.Equals(other.Utility) && LifeSupport.Equals(other.LifeSupport);

        public override string ToString() => $"cockpit={Cockpit:F2} utility={Utility:F2} lifeSupport={LifeSupport:F2}";
    }
}
