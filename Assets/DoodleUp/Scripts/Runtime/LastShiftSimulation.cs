using System;
using UnityEngine;

namespace DoodleUp.Runtime
{
    public enum LastShiftPreset
    {
        HighHeatHighThrust,
        PowerOverloadLooseBattery,
        BadAttitudeHighOxygen
    }

    public enum LastShiftDominantProblem
    {
        None,
        CoolingCouplingDetached,
        BatteryDisplacedBusDisconnected,
        SideOxygenLeak
    }

    public enum LastShiftItemRole
    {
        Battery,
        CoolingCanister,
        PatchPlate,
        Tether
    }

    [Serializable]
    public struct LastShiftMeteorStimulus
    {
        public Vector3 ImpactPoint;
        public Vector3 ImpactVector;
        public float Mass;
        public float Speed;
        public float Energy => 0.5f * Mass * Speed * Speed;

        public static LastShiftMeteorStimulus Canonical => new()
        {
            ImpactPoint = LastShiftShipDimensions.MeteorImpactPoint,
            ImpactVector = new Vector3(1f, -0.12f, 0.18f).normalized,
            Mass = 42f,
            Speed = 8f
        };
    }

    [Serializable]
    public struct LastShiftShipState
    {
        [Range(0f, 1f)] public float ThrustDemand;
        [Range(0f, 1f)] public float BusPower;
        [Range(0f, 1f)] public float OxygenPressure;
        [Range(0f, 1f)] public float HullIntegrity;
        [Range(0f, 1f)] public float EngineHeat;
        public float ShipAttitudeDegrees;
        [Range(0f, 1f)] public float ExistingDamage;

        /// <summary>
        /// 연료 예산(CT-06 N3, 기획 §2.3 B-2). <b>보급 지점은 0개다</b> — 한 번 쓰면 돌아오지 않는
        /// 항해 1회 예산이고, 그래서 추력을 쓰는 것 자체가 결정이 된다.
        ///
        /// 기본값 <c>0</c> 은 유효한 배 상태가 아니다. 배는 언제나
        /// <see cref="LastShiftPresetFactory"/> 를 거쳐 만들어지고 거기서 1.00 으로 채워진다.
        /// </summary>
        [Range(0f, 1f)] public float FuelReserve;

        /// <summary>
        /// 도킹 진행도(CT-06 N4, 기획 §2.3 B-2). 단위는 <c>thrust·s</c> 이며 매 tick
        /// <c>ThrustDemand × dt</c> 만큼 누적된다. <b>0~1 정규화가 아니다</b> — 목표가
        /// <see cref="LastShiftRecoveryTuning.DockTargetThrustSeconds"/>(150) 이라 범위 특성이
        /// 다르고, 정규화하면 "추력을 얼마나 오래 유지했는가" 라는 단위가 사라진다.
        /// </summary>
        public float DockProgress;
    }

    public readonly struct LastShiftResolverInput
    {
        public readonly LastShiftMeteorStimulus Meteor;
        public readonly LastShiftShipState State;
        public readonly Vector3 CrewPosition;
        public readonly Vector3 BatteryPosition;
        public readonly Vector3 BatteryNominalPosition;
        public readonly bool BatterySecured;
        public readonly Vector3 CoolingPosition;
        public readonly Vector3 CoolingNominalPosition;
        public readonly bool CoolingSecured;
        public readonly Vector3 PatchPosition;
        public readonly Vector3 PatchNominalPosition;
        public readonly bool PatchSecured;
        public readonly Vector3 TetherPosition;
        public readonly Vector3 TetherNominalPosition;
        public readonly bool TetherSecured;

        public LastShiftResolverInput(
            LastShiftMeteorStimulus meteor,
            LastShiftShipState state,
            Vector3 crewPosition,
            Vector3 batteryPosition,
            Vector3 batteryNominalPosition,
            bool batterySecured,
            Vector3 coolingPosition,
            Vector3 coolingNominalPosition,
            bool coolingSecured,
            Vector3 patchPosition,
            Vector3 patchNominalPosition,
            bool patchSecured,
            Vector3 tetherPosition,
            Vector3 tetherNominalPosition,
            bool tetherSecured)
        {
            Meteor = meteor;
            State = state;
            CrewPosition = crewPosition;
            BatteryPosition = batteryPosition;
            BatteryNominalPosition = batteryNominalPosition;
            BatterySecured = batterySecured;
            CoolingPosition = coolingPosition;
            CoolingNominalPosition = coolingNominalPosition;
            CoolingSecured = coolingSecured;
            PatchPosition = patchPosition;
            PatchNominalPosition = patchNominalPosition;
            PatchSecured = patchSecured;
            TetherPosition = tetherPosition;
            TetherNominalPosition = tetherNominalPosition;
            TetherSecured = tetherSecured;
        }
    }

    public readonly struct LastShiftResolverResult
    {
        public readonly LastShiftDominantProblem Problem;
        public readonly float CoolingScore;
        public readonly float BatteryScore;
        public readonly float LeakScore;
        public readonly string CauseChain;

        public LastShiftResolverResult(
            LastShiftDominantProblem problem,
            float coolingScore,
            float batteryScore,
            float leakScore,
            string causeChain)
        {
            Problem = problem;
            CoolingScore = coolingScore;
            BatteryScore = batteryScore;
            LeakScore = leakScore;
            CauseChain = causeChain;
        }
    }

    public static class LastShiftPresetFactory
    {
        /// <summary>
        /// 프리셋 상태. <b>연료·도킹 진행도는 프리셋별로 다르지 않다</b> — 세 프리셋은 운석
        /// 직후의 서로 다른 사고 상황이지 서로 다른 항해 시점이 아니므로, 항해 시작 시점의
        /// 예산(연료 1.00)과 누적(도킹 0)은 셋 다 같다. 프리셋마다 다르게 두면 "연료가 모자란
        /// 것이 내 조종 탓인지 프리셋 탓인지" 를 플레이어가 구분할 수 없다.
        /// </summary>
        public static LastShiftShipState Create(LastShiftPreset preset)
        {
            var state = preset switch
            {
                LastShiftPreset.HighHeatHighThrust => new LastShiftShipState
                {
                    ThrustDemand = 0.92f,
                    BusPower = 0.62f,
                    OxygenPressure = 0.64f,
                    HullIntegrity = 0.86f,
                    EngineHeat = 0.94f,
                    ShipAttitudeDegrees = 8f,
                    ExistingDamage = 0.12f
                },
                LastShiftPreset.PowerOverloadLooseBattery => new LastShiftShipState
                {
                    ThrustDemand = 0.46f,
                    // 0.98 -> 0.55 (balance 확정, 기획 §2.2 A-3). 세 후보를 거쳐 나온 값이다.
                    //
                    // 0.98  S-P1 발동선 0.65 위라 t=0 에 전력이 아무 말도 못 했다
                    // 0.62  HighHeat(0.62)와 전력 축이 통째로 겹쳤다
                    // 0.40  운석이 bus 를 0.1034 깎으므로 시작부터 미연결 상한(0.40) 아래가
                    //       되어 전력 시계가 한 번도 안 돈다
                    //
                    // 0.55 면 운석 직후 0.4466 으로 상한 위라 시계가 돌고, S-P1 은 즉시,
                    // S-P2 는 0.93초에 걸린다(BadAttitude 1.53초 / HighHeat 2.33초보다 먼저).
                    // 여유 0.0466 은 batteryTravel 이 29cm 흔들려도 버티는 폭이다 —
                    // 그 거리가 물리 결과라 얇게 잡으면 간헐 실패로 돌아온다.
                    BusPower = 0.55f,
                    OxygenPressure = 0.58f,
                    // 0.84 -> 0.70 (balance 확정, 기획 §2.2 A-4).
                    // 파공 발동선이 <=0.75 라 0.84 에서는 S-O1 이 안 걸리고, 악화가 파공 구역
                    // 에서만 도므로 O2 가 떨어지지 않는다. 그러면 이 프리셋의 성공선인
                    // S-O2(<=0.35)에 영원히 못 닿아 "전력 -> 재가압 정지 -> 산소" 연쇄
                    // (기획 :1084)가 성립하지 않는다.
                    HullIntegrity = 0.70f,
                    EngineHeat = 0.43f,
                    ShipAttitudeDegrees = 12f,
                    ExistingDamage = 0.14f
                },
                _ => new LastShiftShipState
                {
                    ThrustDemand = 0.38f,
                    BusPower = 0.58f,
                    OxygenPressure = 0.96f,
                    HullIntegrity = 0.48f,
                    EngineHeat = 0.40f,
                    ShipAttitudeDegrees = 72f,
                    ExistingDamage = 0.42f
                }
            };

            state.FuelReserve = LastShiftRecoveryTuning.FuelReserveInitial;
            state.DockProgress = 0f;
            return state;
        }
    }

    public static class LastShiftMeteorApplication
    {
        /// <summary>
        /// 구역별 압력이 없는 호출 경로를 위한 호환 진입점. 세 구역이 모두 같은 압력이다.
        /// </summary>
        public static LastShiftShipState Apply(
            in LastShiftMeteorStimulus meteor,
            in LastShiftShipState preImpactState,
            LastShiftGrabbable[] items)
        {
            var pressures = LastShiftZonePressures.Uniform(preImpactState.OxygenPressure);
            return Apply(meteor, preImpactState, ref pressures, LastShiftZone.LifeSupport, items);
        }

        /// <summary>
        /// 충격이 뚫은 압력 손실은 <b>파공 구역 하나</b>에만 들어간다(기획 v0.3 §2.2).
        /// 나머지 구역으로는 평준화를 통해서만 번지며, 그 전파를 끊는 것이 격리다.
        /// </summary>
        public static LastShiftShipState Apply(
            in LastShiftMeteorStimulus meteor,
            in LastShiftShipState preImpactState,
            ref LastShiftZonePressures pressures,
            LastShiftZone breachZone,
            LastShiftGrabbable[] items)
        {
            var state = preImpactState;
            var severity = CalculateSeverity(meteor);

            if (items != null)
            {
                foreach (var item in items)
                    if (item != null) item.ApplyImpact(meteor, severity);
            }

            var batteryTravel = FindTravel(items, LastShiftItemRole.Battery);
            var patchTravel = FindTravel(items, LastShiftItemRole.PatchPlate);
            state.HullIntegrity = Mathf.Clamp01(state.HullIntegrity - severity * (0.07f + state.ExistingDamage * 0.035f));
            state.ExistingDamage = Mathf.Clamp01(state.ExistingDamage + severity * 0.08f);
            state.EngineHeat = Mathf.Clamp01(state.EngineHeat + severity * state.ThrustDemand * 0.045f);
            state.BusPower = Mathf.Clamp01(state.BusPower - severity * 0.015f - batteryTravel * 0.16f);
            pressures[breachZone] -= severity * (1f - state.HullIntegrity) * 0.035f + patchTravel * 0.04f;
            state.OxygenPressure = pressures[LastShiftZone.Cockpit];
            return state;
        }

        public static float CalculateSeverity(in LastShiftMeteorStimulus meteor)
        {
            var energyScale = Mathf.Max(0f, meteor.Energy / LastShiftMeteorStimulus.Canonical.Energy);
            var direction = meteor.ImpactVector.sqrMagnitude > 0.0001f ? meteor.ImpactVector.normalized : Vector3.zero;
            var surfaceDirection = meteor.ImpactPoint.sqrMagnitude > 0.0001f ? meteor.ImpactPoint.normalized : Vector3.left;
            var directness = Mathf.Clamp01(Vector3.Dot(-direction, surfaceDirection));
            return energyScale * Mathf.Lerp(0.55f, 1f, directness);
        }

        private static float FindTravel(LastShiftGrabbable[] items, LastShiftItemRole role)
        {
            if (items == null) return 0f;
            foreach (var item in items)
            {
                if (item != null && item.Role == role)
                    return item.Secured ? 0f : item.DisplacementFromNominal;
            }
            return 0f;
        }
    }

    public static class LastShiftDamageResolver
    {
        public static LastShiftResolverResult Resolve(in LastShiftResolverInput input)
        {
            var state = input.State;
            var normalizedEnergy = Mathf.Max(0f, input.Meteor.Energy / LastShiftMeteorStimulus.Canonical.Energy);
            var direction = input.Meteor.ImpactVector.sqrMagnitude > 0.0001f
                ? input.Meteor.ImpactVector.normalized
                : Vector3.zero;
            var crewImpactProximity = Proximity(input.CrewPosition, input.Meteor.ImpactPoint, LastShiftShipDimensions.CrewProximityRange);
            var batteryTravel = input.BatterySecured ? 0f : NormalizedTravel(input.BatteryPosition, input.BatteryNominalPosition);
            var coolingTravel = input.CoolingSecured ? 0f : NormalizedTravel(input.CoolingPosition, input.CoolingNominalPosition);
            var patchUnavailable = input.PatchSecured ? 0f : NormalizedTravel(input.PatchPosition, input.PatchNominalPosition);
            var tetherUnavailable = input.TetherSecured ? 0f : NormalizedTravel(input.TetherPosition, input.TetherNominalPosition);
            var coolingExposure = Exposure(input.Meteor.ImpactPoint, input.CoolingNominalPosition, direction, Vector3.right);
            var batteryExposure = Exposure(input.Meteor.ImpactPoint, input.BatteryNominalPosition, direction, Vector3.forward);
            var leakExposure = Exposure(input.Meteor.ImpactPoint, input.PatchNominalPosition, direction, Vector3.right);

            var coolingScore = normalizedEnergy * (0.78f + coolingExposure * 0.22f) * (
                state.EngineHeat * 0.62f
                + state.ThrustDemand * 0.48f
                + (input.CoolingSecured ? 0f : 0.20f)
                + coolingTravel * 0.18f
                + state.ExistingDamage * 0.10f);
            var batteryScore = normalizedEnergy * (0.78f + batteryExposure * 0.22f) * (
                state.BusPower * 0.58f
                + (input.BatterySecured ? 0f : 0.72f)
                + batteryTravel * 0.24f
                + crewImpactProximity * 0.04f
                + state.ExistingDamage * 0.08f);
            var leakScore = normalizedEnergy * (0.78f + leakExposure * 0.22f) * (
                state.OxygenPressure * 0.46f
                + Mathf.Clamp01(Mathf.Abs(state.ShipAttitudeDegrees) / 90f) * 0.68f
                + (1f - state.HullIntegrity) * 0.52f
                + patchUnavailable * 0.18f
                + tetherUnavailable * 0.06f
                + state.ExistingDamage * 0.18f);

            var problem = LastShiftDominantProblem.None;
            var score = 0f;
            if (coolingScore >= 0.75f)
            {
                score = coolingScore;
                problem = LastShiftDominantProblem.CoolingCouplingDetached;
            }
            if (batteryScore >= 0.75f && batteryScore > score)
            {
                score = batteryScore;
                problem = LastShiftDominantProblem.BatteryDisplacedBusDisconnected;
            }
            if (leakScore >= 0.75f && leakScore > score)
                problem = LastShiftDominantProblem.SideOxygenLeak;

            var causeChain =
                $"meteor(point={input.Meteor.ImpactPoint}, E={input.Meteor.Energy:F1}, vector={input.Meteor.ImpactVector}) " +
                $"x state(thrust={state.ThrustDemand:F2}, bus={state.BusPower:F2}, O2={state.OxygenPressure:F2}, " +
                $"hull={state.HullIntegrity:F2}, heat={state.EngineHeat:F2}, attitude={state.ShipAttitudeDegrees:F0}, damage={state.ExistingDamage:F2}) " +
                $"x crew={input.CrewPosition} x items(battery={input.BatteryPosition}/nominal:{input.BatteryNominalPosition}/secured:{input.BatterySecured}, " +
                $"cooling={input.CoolingPosition}/nominal:{input.CoolingNominalPosition}/secured:{input.CoolingSecured}, " +
                $"patch={input.PatchPosition}/nominal:{input.PatchNominalPosition}/secured:{input.PatchSecured}, " +
                $"tether={input.TetherPosition}/nominal:{input.TetherNominalPosition}/secured:{input.TetherSecured}) " +
                $"=> scores(cooling={coolingScore:F2}, battery={batteryScore:F2}, leak={leakScore:F2}) => {problem}";

            return new LastShiftResolverResult(problem, coolingScore, batteryScore, leakScore, causeChain);
        }

        private static float NormalizedTravel(Vector3 position, Vector3 nominalPosition)
        {
            return Mathf.Clamp01(Vector3.Distance(position, nominalPosition) / LastShiftShipDimensions.DisplacementFullScale);
        }

        private static float Proximity(Vector3 point, Vector3 impactPoint, float range)
        {
            return 1f / (1f + Vector3.Distance(point, impactPoint) / Mathf.Max(0.01f, range));
        }

        private static float Exposure(Vector3 impactPoint, Vector3 nominalPosition, Vector3 direction, Vector3 vulnerableAxis)
        {
            var proximity = Proximity(nominalPosition, impactPoint, LastShiftShipDimensions.ItemExposureRange);
            var directional = Mathf.Clamp01(Vector3.Dot(direction, vulnerableAxis.normalized) * 0.5f + 0.5f);
            return Mathf.Clamp01(proximity * 0.55f + directional * 0.45f);
        }
    }

    public sealed class LastShiftControlHold
    {
        public const float HoldDuration = 8f;
        public float ThrustDemand { get; private set; }
        public float AttitudeDegrees { get; private set; }
        public float RemainingSeconds { get; private set; }
        public bool IsActive => RemainingSeconds > 0f;

        public void Set(float thrustDemand, float attitudeDegrees)
        {
            ThrustDemand = Mathf.Clamp01(thrustDemand);
            AttitudeDegrees = Mathf.Clamp(attitudeDegrees, -90f, 90f);
            RemainingSeconds = HoldDuration;
        }

        public void Tick(float deltaTime)
        {
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Mathf.Max(0f, deltaTime));
        }

        public void Reset(float thrustDemand, float attitudeDegrees)
        {
            ThrustDemand = Mathf.Clamp01(thrustDemand);
            AttitudeDegrees = Mathf.Clamp(attitudeDegrees, -90f, 90f);
            RemainingSeconds = 0f;
        }
    }
}
