using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftSimulationTests
    {
        // 정위치는 치수 정본에서 가져온다. 리터럴로 두면 배 크기가 바뀔 때 이 테스트만
        // 옛 배의 좌표로 계속 통과하고, 정작 검증하려던 "정위치 대비 이탈" 이 무의미해진다.
        private static readonly Vector3 BatteryNominal = LastShiftShipDimensions.BatteryNominal;
        private static readonly Vector3 CoolingNominal = LastShiftShipDimensions.CoolingNominal;
        private static readonly Vector3 PatchNominal = LastShiftShipDimensions.PatchPlateNominal;
        private static readonly Vector3 TetherNominal = LastShiftShipDimensions.TetherNominal;
        private static readonly Vector3 CrewPosition = LastShiftShipDimensions.SpawnPoint;

        [Test]
        public void SameMeteorProducesDifferentDominantProblemForEachStateAndPlacement()
        {
            var meteor = LastShiftMeteorStimulus.Canonical;
            var outcomes = new HashSet<LastShiftDominantProblem>
            {
                Resolve(meteor, LastShiftPreset.HighHeatHighThrust, true, false, true).Problem,
                Resolve(meteor, LastShiftPreset.PowerOverloadLooseBattery, false, true, true).Problem,
                Resolve(meteor, LastShiftPreset.BadAttitudeHighOxygen, true, true, false).Problem
            };

            Assert.That(outcomes, Is.EquivalentTo(new[]
            {
                LastShiftDominantProblem.CoolingCouplingDetached,
                LastShiftDominantProblem.BatteryDisplacedBusDisconnected,
                LastShiftDominantProblem.SideOxygenLeak
            }));
        }

        [Test]
        public void CanonicalMeteorIsIdenticalAcrossPresetResolution()
        {
            var meteor = LastShiftMeteorStimulus.Canonical;
            foreach (LastShiftPreset preset in System.Enum.GetValues(typeof(LastShiftPreset)))
            {
                var result = Resolve(
                    meteor,
                    preset,
                    preset != LastShiftPreset.PowerOverloadLooseBattery,
                    preset != LastShiftPreset.HighHeatHighThrust,
                    preset != LastShiftPreset.BadAttitudeHighOxygen);
                Assert.That(result.CauseChain, Does.Contain($"point={meteor.ImpactPoint}"));
                Assert.That(result.CauseChain, Does.Contain($"E={meteor.Energy:F1}"));
                Assert.That(result.CauseChain, Does.Contain($"vector={meteor.ImpactVector}"));
            }
        }

        [Test]
        public void ResolverOutcomeUsesNominalTravelAndIgnoresSecuredSceneReposition()
        {
            var meteor = LastShiftMeteorStimulus.Canonical;
            var state = LastShiftPresetFactory.Create(LastShiftPreset.PowerOverloadLooseBattery);
            var looseAtNominal = Resolve(meteor, state, BatteryNominal, BatteryNominal, false);
            var looseDisplaced = Resolve(meteor, state, BatteryNominal + Vector3.right * 4f, BatteryNominal, false);
            var securedDisplaced = Resolve(meteor, state, BatteryNominal + Vector3.right * 4f, BatteryNominal, true);
            var securedAtNominal = Resolve(meteor, state, BatteryNominal, BatteryNominal, true);

            Assert.That(looseDisplaced.BatteryScore, Is.GreaterThan(looseAtNominal.BatteryScore));
            Assert.That(securedDisplaced.BatteryScore, Is.EqualTo(securedAtNominal.BatteryScore).Within(0.0001f));
            Assert.That(looseDisplaced.CauseChain, Does.Contain($"nominal:{BatteryNominal}"));
        }

        [Test]
        public void ImpactPointDirectionAndEnergyEachAffectResolvedScores()
        {
            var state = LastShiftPresetFactory.Create(LastShiftPreset.PowerOverloadLooseBattery);
            var canonical = LastShiftMeteorStimulus.Canonical;
            var movedPoint = canonical;
            movedPoint.ImpactPoint = BatteryNominal + Vector3.one * 24f;
            var reversed = canonical;
            reversed.ImpactVector = -canonical.ImpactVector;
            var lowerEnergy = canonical;
            lowerEnergy.Speed *= 0.5f;

            var baseline = Resolve(canonical, state, BatteryNominal, BatteryNominal, false);
            var pointResult = Resolve(movedPoint, state, BatteryNominal, BatteryNominal, false);
            var directionResult = Resolve(reversed, state, BatteryNominal, BatteryNominal, false);
            var energyResult = Resolve(lowerEnergy, state, BatteryNominal, BatteryNominal, false);

            Assert.That(pointResult.BatteryScore, Is.Not.EqualTo(baseline.BatteryScore).Within(0.0001f));
            Assert.That(directionResult.BatteryScore, Is.Not.EqualTo(baseline.BatteryScore).Within(0.0001f));
            Assert.That(energyResult.BatteryScore, Is.LessThan(baseline.BatteryScore));
        }

        [Test]
        public void TetherAvailabilityContributesToLeakRisk()
        {
            var meteor = LastShiftMeteorStimulus.Canonical;
            var state = LastShiftPresetFactory.Create(LastShiftPreset.BadAttitudeHighOxygen);
            var available = ResolveWithTether(meteor, state, TetherNominal, true);
            var unavailable = ResolveWithTether(meteor, state, TetherNominal + Vector3.forward * 4f, false);

            Assert.That(unavailable.LeakScore, Is.GreaterThan(available.LeakScore));
            Assert.That(unavailable.CauseChain, Does.Contain("tether="));
        }

        [Test]
        public void MeteorApplicationMutatesLooseItemFromItsNominalPosition()
        {
            var battery = CreateItem(LastShiftItemRole.Battery, BatteryNominal, false);
            var beforeState = LastShiftPresetFactory.Create(LastShiftPreset.PowerOverloadLooseBattery);
            var beforePosition = battery.transform.position;

            var afterState = LastShiftMeteorApplication.Apply(
                LastShiftMeteorStimulus.Canonical,
                beforeState,
                new[] { battery });

            Assert.That(battery.transform.position, Is.Not.EqualTo(beforePosition));
            Assert.That(battery.DisplacementFromNominal, Is.GreaterThan(0f));
            Assert.That(battery.Body.linearVelocity.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(afterState.HullIntegrity, Is.LessThan(beforeState.HullIntegrity));
            Assert.That(afterState.BusPower, Is.LessThan(beforeState.BusPower));

            Object.DestroyImmediate(battery.gameObject);
        }

        [Test]
        public void SoloControlHoldExpiresAfterEightSeconds()
        {
            var hold = new LastShiftControlHold();
            hold.Set(0.7f, 35f);
            hold.Tick(7.9f);
            Assert.That(hold.RemainingSeconds, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(hold.ThrustDemand, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(hold.AttitudeDegrees, Is.EqualTo(35f).Within(0.0001f));
            hold.Tick(0.2f);
            Assert.That(hold.RemainingSeconds, Is.Zero);
        }

        private static LastShiftResolverResult Resolve(
            LastShiftMeteorStimulus meteor,
            LastShiftPreset preset,
            bool batterySecured,
            bool coolingSecured,
            bool patchSecured)
        {
            var state = LastShiftPresetFactory.Create(preset);
            return LastShiftDamageResolver.Resolve(new LastShiftResolverInput(
                meteor,
                state,
                CrewPosition,
                BatteryNominal,
                BatteryNominal,
                batterySecured,
                CoolingNominal,
                CoolingNominal,
                coolingSecured,
                PatchNominal,
                PatchNominal,
                patchSecured,
                TetherNominal,
                TetherNominal,
                true));
        }

        private static LastShiftResolverResult Resolve(
            LastShiftMeteorStimulus meteor,
            LastShiftShipState state,
            Vector3 batteryPosition,
            Vector3 batteryNominal,
            bool batterySecured)
        {
            return LastShiftDamageResolver.Resolve(new LastShiftResolverInput(
                meteor,
                state,
                CrewPosition,
                batteryPosition,
                batteryNominal,
                batterySecured,
                CoolingNominal,
                CoolingNominal,
                true,
                PatchNominal,
                PatchNominal,
                true,
                TetherNominal,
                TetherNominal,
                true));
        }

        private static LastShiftResolverResult ResolveWithTether(
            LastShiftMeteorStimulus meteor,
            LastShiftShipState state,
            Vector3 tetherPosition,
            bool tetherSecured)
        {
            return LastShiftDamageResolver.Resolve(new LastShiftResolverInput(
                meteor,
                state,
                CrewPosition,
                BatteryNominal,
                BatteryNominal,
                true,
                CoolingNominal,
                CoolingNominal,
                true,
                PatchNominal,
                PatchNominal,
                false,
                tetherPosition,
                TetherNominal,
                tetherSecured));
        }

        private static LastShiftGrabbable CreateItem(LastShiftItemRole role, Vector3 position, bool secured)
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.transform.position = position;
            itemObject.AddComponent<Rigidbody>();
            var item = itemObject.AddComponent<LastShiftGrabbable>();
            item.Configure(role, secured);
            return item;
        }
    }
}
