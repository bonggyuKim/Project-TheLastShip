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
                   OxygenPumpRunning == other.OxygenPumpRunning;
        }
    }
}
