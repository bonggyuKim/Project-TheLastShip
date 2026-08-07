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
        Power = 1,
        Cooling = 2,
        LifeSupport = 3
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
        /// <summary>
        /// 구역 수. <b>이 값과 <see cref="BoundaryPlanes"/> 가 구역 위상의 정본이고, 나머지는
        /// 전부 여기서 파생한다.</b>
        ///
        /// 통로 재설계 v6(<c>docs/corridor-4p-redesign-v1.md</c> §4)이 "진짜 알고리즘 변경이
        /// 필요한 것은 <see cref="Resolve"/> 하나" 라고 짚은 자리다. 엔진실을 전력실·냉각실로
        /// 쪼개면 이 값이 <c>4</c> 가 되는데, 그때 손대야 하는 것이 분기 형태가 아니라 아래
        /// 경계 배열 하나가 되도록 미리 풀어 둔다 — 구역별 <c>if</c> 사슬로 두면 구역이 늘 때마다
        /// 사슬을 다시 짜야 하고, 그 과정에서 <see cref="LastShiftZonePressures.Lowest"/> 처럼
        /// 구역 수를 따로 들고 있는 자리가 조용히 뒤처진다.
        /// </summary>
        public const int ZoneCount = 4;

        /// <summary>인접 구역 쌍의 수. 구역이 일렬이므로 언제나 구역 수보다 하나 적다.</summary>
        public const int BoundaryCount = ZoneCount - 1;

        /// <summary>구역 판정 기준 x 경계. 치수 정본(<see cref="LastShiftShipDimensions"/>)에서 파생한다.</summary>
        public const float CockpitMaxX = -LastShiftShipDimensions.ZoneBoundaryX;
        /// <summary>전력실↔냉각실 경계. 선체 중앙이고 개구부 2 가 놓인 평면이다(§3).</summary>
        public const float PowerMaxX = 0f;
        public const float LifeSupportMinX = LastShiftShipDimensions.ZoneBoundaryX;

        /// <summary>
        /// 구역 경계 평면을 선수→선미 순으로 늘어놓은 것. index 가 곧 경계 번호이고,
        /// 경계 <c>b</c> 는 구역 <c>b</c> 와 <c>b+1</c> 을 가른다.
        ///
        /// 오름차순이어야 <see cref="Resolve"/> 의 선형 훑기가 성립한다 — 그 조건은
        /// <c>LastShiftZoneTopologyTests</c> 가 고정한다.
        /// </summary>
        private static readonly float[] BoundaryPlanes = { CockpitMaxX, PowerMaxX, LifeSupportMinX };

        /// <summary>
        /// 위치 → 구역. 경계를 선수 쪽부터 훑어 처음으로 <c>x ≤ 경계</c> 인 구역을 돌려주고,
        /// 어느 경계에도 안 걸리면 마지막 구역이다.
        ///
        /// <b>경계 평면 위의 점은 낮은 쪽(선수 쪽) 구역에 속한다.</b> 개구부 몇 개는 x 가 구역
        /// 경계와 <b>같은 값</b>이라 이 동점 규칙이 실제로 관측된다 — 규칙을 정하지 않고 두면
        /// 분기 순서 같은 우연이 답을 정하고, 구역이 늘 때 조용히 뒤집힌다. 그래서 규칙으로
        /// 못박고 테스트로 고정한다. 경계 위 좌표를 실제로 쓰지 말라는 요구는 그대로다 —
        /// 그쪽은 <see cref="LastShiftShipDimensions.SpaceCenterXBefore"/> 가 답이다.
        /// </summary>
        public static LastShiftZone Resolve(Vector3 position)
        {
            for (var boundary = 0; boundary < BoundaryCount; boundary++)
                if (position.x <= BoundaryPlanes[boundary])
                    return (LastShiftZone)boundary;
            return (LastShiftZone)(ZoneCount - 1);
        }

        public static string NameOf(LastShiftZone zone)
        {
            return zone switch
            {
                LastShiftZone.Cockpit => LastShiftSceneZones.CockpitZoneName,
                LastShiftZone.Power => LastShiftSceneZones.PowerZoneName,
                LastShiftZone.Cooling => LastShiftSceneZones.CoolingZoneName,
                _ => LastShiftSceneZones.LifeSupportZoneName
            };
        }

        /// <summary>
        /// 로그에 쓰는 영문 키. <see cref="LastShiftZonePressures.ToString"/> 가 구역을 훑어
        /// 찍을 때 쓴다 — 구역이 늘면 여기 한 줄만 보태면 로그 형식이 따라온다.
        /// </summary>
        public static string KeyOf(LastShiftZone zone)
        {
            return zone switch
            {
                LastShiftZone.Cockpit => "cockpit",
                LastShiftZone.Power => "power",
                LastShiftZone.Cooling => "cooling",
                _ => "lifeSupport"
            };
        }

        /// <summary>HUD 칸에 쓰는 짧은 이름. 문서 용어(조종석/전력실/냉각실/산소실)를 그대로 쓴다.</summary>
        public static string ShortLabelOf(LastShiftZone zone)
        {
            return zone switch
            {
                LastShiftZone.Cockpit => "조종석",
                LastShiftZone.Power => "전력실",
                LastShiftZone.Cooling => "냉각실",
                _ => "산소실"
            };
        }

        public static bool TryResolveName(string zoneName, out LastShiftZone zone)
        {
            if (zoneName == LastShiftSceneZones.CockpitZoneName) { zone = LastShiftZone.Cockpit; return true; }
            if (zoneName == LastShiftSceneZones.PowerZoneName) { zone = LastShiftZone.Power; return true; }
            if (zoneName == LastShiftSceneZones.CoolingZoneName) { zone = LastShiftZone.Cooling; return true; }
            if (zoneName == LastShiftSceneZones.LifeSupportZoneName) { zone = LastShiftZone.LifeSupport; return true; }
            zone = LastShiftZone.Power;
            return false;
        }

        /// <summary>
        /// 경계 index 의 낮은 쪽 구역. 0 = 조종석↔엔진실, 1 = 엔진실↔산소실.
        /// 경계 번호가 곧 낮은 쪽 구역 번호다 — 구역이 일렬이라 그렇고, 구역이 넷이 돼도 같다.
        /// </summary>
        public static LastShiftZone LowZoneOf(int boundary) =>
            (LastShiftZone)Mathf.Clamp(boundary, 0, BoundaryCount - 1);

        public static LastShiftZone HighZoneOf(int boundary) =>
            (LastShiftZone)(Mathf.Clamp(boundary, 0, BoundaryCount - 1) + 1);

        /// <summary>경계가 놓인 x. 벌크헤드/문 배치와 같은 값이어야 한다.</summary>
        public static float BoundaryX(int boundary) =>
            BoundaryPlanes[Mathf.Clamp(boundary, 0, BoundaryCount - 1)];

        /// <summary>
        /// 이 위치에서 가장 가까운 경계. 문 조작 프롬프트의 대상 판정에 쓴다.
        /// 동점이면 낮은 번호를 고른다 — 예전 <c>&lt;=</c> 비교의 동작을 그대로 옮긴 것이다.
        /// </summary>
        public static int NearestBoundary(Vector3 position)
        {
            var nearest = 0;
            var best = Mathf.Abs(position.x - BoundaryX(0));
            for (var boundary = 1; boundary < BoundaryCount; boundary++)
            {
                var distance = Mathf.Abs(position.x - BoundaryX(boundary));
                if (distance >= best) continue;
                best = distance;
                nearest = boundary;
            }
            return nearest;
        }
    }

    /// <summary>
    /// 두 경계의 문이 열려 있는지. 문이 닫히면 그 경계의 압력 교환이 0 이 된다(기획 §2.2.1).
    /// 문 자체의 개폐 애니메이션·판정은 <see cref="LastShiftZoneDoor"/> 가 갖고, 여기는
    /// 평준화 계산이 읽는 스냅샷만 담는다.
    /// </summary>
    public struct LastShiftDoorState
    {
        // 경계 이름이 아니라 번호로 든다. 이름으로 두면 구역이 늘 때 이름이 안 맞게 되고
        // ("CockpitUtility" 가 이제 조종석-전력실이다), 새 경계를 넣을 자리도 이름 사이에 없다.
        public bool Boundary0Open;
        public bool Boundary1Open;
        public bool Boundary2Open;

        /// <summary>
        /// 문이 아직 없는 구성(N0b 이전, EditMode 최소 조립)의 기본값은 전부 열림이다.
        /// 경계를 훑어 세운다 — 문이 셋이 되는 날 여기 리터럴이 남아 있으면 새 문만 조용히
        /// 닫힌 채로 시작하고, 압력 평준화가 그 경계에서만 안 일어난다.
        /// </summary>
        public static LastShiftDoorState AllOpen
        {
            get
            {
                var state = new LastShiftDoorState();
                for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                    state[boundary] = true;
                return state;
            }
        }

        public bool this[int boundary]
        {
            get => boundary switch
            {
                0 => Boundary0Open,
                1 => Boundary1Open,
                _ => Boundary2Open
            };
            set
            {
                switch (boundary)
                {
                    case 0: Boundary0Open = value; break;
                    case 1: Boundary1Open = value; break;
                    default: Boundary2Open = value; break;
                }
            }
        }

        public bool Equals(LastShiftDoorState other)
        {
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                if (this[boundary] != other[boundary])
                    return false;
            return true;
        }
    }

    /// <summary>
    /// 구역 네 개의 산소 압력. 기획 §2.2 A-2 의 <c>ZonePressure[zone]</c> 정본이다.
    ///
    /// <see cref="LastShiftShipState.OxygenPressure"/> 는 이 값을 대체하지 않고 <b>조종석 압력의
    /// 파생값</b>으로 남는다. 도킹 성공 판정(§2.2 "도킹 성공 판정은 조종석 압력으로 본다")과
    /// leak 점수·네트워크 스냅샷이 이미 그 필드를 읽고 있으며, 세 값의 평균으로 바꾸면
    /// "산소실을 버리고 평균을 맞추기" 라는 산수가 생겨 격리가 판단이 아니게 된다.
    /// </summary>
    public struct LastShiftZonePressures
    {
        public float Cockpit;
        public float Power;
        public float Cooling;
        public float LifeSupport;

        public LastShiftZonePressures(float cockpit, float power, float cooling, float lifeSupport)
        {
            Cockpit = cockpit;
            Power = power;
            Cooling = cooling;
            LifeSupport = lifeSupport;
        }

        /// <summary>
        /// 전 구역 같은 압력. 인자를 나열하지 않고 <see cref="SetAll"/> 을 거친다 — 구역이 늘 때
        /// 생성자는 인자 개수가 안 맞아 컴파일이 막아 주지만, 여기서 리터럴을 세 번 넘기는
        /// 형태는 네 번째 구역만 <c>0</c> 으로 남긴 채 조용히 통과한다.
        /// </summary>
        public static LastShiftZonePressures Uniform(float pressure)
        {
            var pressures = new LastShiftZonePressures();
            pressures.SetAll(pressure);
            return pressures;
        }

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
                LastShiftZone.Power => Power,
                LastShiftZone.Cooling => Cooling,
                _ => LifeSupport
            };
            set
            {
                switch (zone)
                {
                    case LastShiftZone.Cockpit: Cockpit = Mathf.Clamp01(value); break;
                    case LastShiftZone.Power: Power = Mathf.Clamp01(value); break;
                    case LastShiftZone.Cooling: Cooling = Mathf.Clamp01(value); break;
                    default: LifeSupport = Mathf.Clamp01(value); break;
                }
            }
        }

        /// <summary>
        /// 가장 낮은 구역 압력. 사이렌은 "어느 구역이든 0.15 이하" 이므로 이 값을 본다(§2.2 A-2 연쇄).
        /// 구역을 훑어 고른다 — 이름을 나열해 두면 구역이 늘 때 새 구역만 사이렌에서 빠지고,
        /// 그건 "산소실이 0.1 인데 경보가 안 울린다" 로만 드러나는 종류의 누락이다.
        /// </summary>
        public float Lowest
        {
            get
            {
                var lowest = this[(LastShiftZone)0];
                for (var zone = 1; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                    lowest = Mathf.Min(lowest, this[(LastShiftZone)zone]);
                return lowest;
            }
        }

        /// <summary>
        /// <see cref="Lowest"/> 인 구역. 결과 화면의 질식 원인 줄이 죽은 자리를 모를 때
        /// 대신 쓰는 값이다. 동률이면 먼저 선언된 구역이 남는다.
        /// </summary>
        public LastShiftZone LowestZone
        {
            get
            {
                var lowestZone = (LastShiftZone)0;
                for (var zone = 1; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                    if (this[(LastShiftZone)zone] < this[lowestZone]) lowestZone = (LastShiftZone)zone;
                return lowestZone;
            }
        }

        public void SetAll(float pressure)
        {
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                this[(LastShiftZone)zone] = pressure;
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

        public bool Equals(LastShiftZonePressures other)
        {
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                if (!this[(LastShiftZone)zone].Equals(other[(LastShiftZone)zone]))
                    return false;
            return true;
        }

        public override string ToString()
        {
            var text = string.Empty;
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                if (zone > 0) text += " ";
                text += $"{LastShiftZoneAtlas.KeyOf((LastShiftZone)zone)}={this[(LastShiftZone)zone]:F2}";
            }
            return text;
        }
    }
}
