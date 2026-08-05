using System;
using Unity.Netcode;

namespace DoodleUp.Runtime
{
    public struct LastShiftNetworkSnapshot : INetworkSerializable, IEquatable<LastShiftNetworkSnapshot>
    {
        public LastShiftPreset Preset;
        public LastShiftShipState ShipState;
        public LastShiftDominantProblem FirstProblem;
        public LastShiftDominantProblem CurrentProblem;
        public float CoolingScore;
        public float BatteryScore;
        public float LeakScore;
        public float DockingSecondsRemaining;
        public int ResetGeneration;
        public int ImpactApplicationCount;
        public byte SecuredItemMask;
        public bool HasAppliedImpact;
        public LastShiftVerdict Verdict;
        public byte SacrificedSystemMask;
        public float ThrustCeiling;
        public bool HeatProtectionEngaged;
        public bool SteeringDelayed;
        public bool OxygenPumpRunning;

        /// <summary>S-O3 전선 사이렌(N9). 모든 클라이언트가 같은 시점에 울려야 국소 정보 예외가 성립한다.</summary>
        public bool SirenActive;

        /// <summary>
        /// N0 구역 압력 중 엔진실·산소실. 조종석 압력은 <see cref="ShipState"/>.OxygenPressure 가
        /// 이미 나르므로(그 필드가 조종석 파생값이다) 여기서 다시 보내지 않는다.
        /// 클라이언트 HUD 3칸(N10)과 클라이언트 쪽 진공 판정이 이 둘을 읽는다.
        /// </summary>
        public float UtilityPressure;
        public float LifeSupportPressure;

        /// <summary>N0b 구역 문. 닫힌 경계는 압력 교환이 0 이므로 클라이언트도 같은 상태를 봐야 한다.</summary>
        public bool CockpitUtilityDoorOpen;
        public bool UtilityLifeSupportDoorOpen;

        /// <summary>
        /// T2 판독이 읽는 미억제 손상 계통. 손상 판정과 수리 완료 플래그는 서버에만 있으므로
        /// 클라이언트는 이 마스크 없이는 같은 등급을 낼 수 없다 — 고쳐도 게이지가 안 내려간다.
        /// </summary>
        public byte UncontainedSystemMask;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Preset);
            serializer.SerializeValue(ref ShipState.ThrustDemand);
            serializer.SerializeValue(ref ShipState.BusPower);
            serializer.SerializeValue(ref ShipState.OxygenPressure);
            serializer.SerializeValue(ref ShipState.HullIntegrity);
            serializer.SerializeValue(ref ShipState.EngineHeat);
            serializer.SerializeValue(ref ShipState.ShipAttitudeDegrees);
            serializer.SerializeValue(ref ShipState.ExistingDamage);
            serializer.SerializeValue(ref FirstProblem);
            serializer.SerializeValue(ref CurrentProblem);
            serializer.SerializeValue(ref CoolingScore);
            serializer.SerializeValue(ref BatteryScore);
            serializer.SerializeValue(ref LeakScore);
            serializer.SerializeValue(ref DockingSecondsRemaining);
            serializer.SerializeValue(ref ResetGeneration);
            serializer.SerializeValue(ref ImpactApplicationCount);
            serializer.SerializeValue(ref SecuredItemMask);
            serializer.SerializeValue(ref HasAppliedImpact);
            serializer.SerializeValue(ref Verdict);
            serializer.SerializeValue(ref SacrificedSystemMask);
            serializer.SerializeValue(ref ThrustCeiling);
            serializer.SerializeValue(ref HeatProtectionEngaged);
            serializer.SerializeValue(ref SteeringDelayed);
            serializer.SerializeValue(ref OxygenPumpRunning);
            serializer.SerializeValue(ref SirenActive);
            serializer.SerializeValue(ref UtilityPressure);
            serializer.SerializeValue(ref LifeSupportPressure);
            serializer.SerializeValue(ref CockpitUtilityDoorOpen);
            serializer.SerializeValue(ref UtilityLifeSupportDoorOpen);
            serializer.SerializeValue(ref UncontainedSystemMask);
        }

        public bool Equals(LastShiftNetworkSnapshot other)
        {
            return Preset == other.Preset &&
                   ShipState.ThrustDemand.Equals(other.ShipState.ThrustDemand) &&
                   ShipState.BusPower.Equals(other.ShipState.BusPower) &&
                   ShipState.OxygenPressure.Equals(other.ShipState.OxygenPressure) &&
                   ShipState.HullIntegrity.Equals(other.ShipState.HullIntegrity) &&
                   ShipState.EngineHeat.Equals(other.ShipState.EngineHeat) &&
                   ShipState.ShipAttitudeDegrees.Equals(other.ShipState.ShipAttitudeDegrees) &&
                   ShipState.ExistingDamage.Equals(other.ShipState.ExistingDamage) &&
                   FirstProblem == other.FirstProblem &&
                   CurrentProblem == other.CurrentProblem &&
                   CoolingScore.Equals(other.CoolingScore) &&
                   BatteryScore.Equals(other.BatteryScore) &&
                   LeakScore.Equals(other.LeakScore) &&
                   DockingSecondsRemaining.Equals(other.DockingSecondsRemaining) &&
                   ResetGeneration == other.ResetGeneration &&
                   ImpactApplicationCount == other.ImpactApplicationCount &&
                   SecuredItemMask == other.SecuredItemMask &&
                   HasAppliedImpact == other.HasAppliedImpact &&
                   Verdict == other.Verdict &&
                   SacrificedSystemMask == other.SacrificedSystemMask &&
                   ThrustCeiling.Equals(other.ThrustCeiling) &&
                   HeatProtectionEngaged == other.HeatProtectionEngaged &&
                   SteeringDelayed == other.SteeringDelayed &&
                   OxygenPumpRunning == other.OxygenPumpRunning &&
                   SirenActive == other.SirenActive &&
                   UtilityPressure.Equals(other.UtilityPressure) &&
                   LifeSupportPressure.Equals(other.LifeSupportPressure) &&
                   CockpitUtilityDoorOpen == other.CockpitUtilityDoorOpen &&
                   UtilityLifeSupportDoorOpen == other.UtilityLifeSupportDoorOpen &&
                   UncontainedSystemMask == other.UncontainedSystemMask;
        }
    }
}
