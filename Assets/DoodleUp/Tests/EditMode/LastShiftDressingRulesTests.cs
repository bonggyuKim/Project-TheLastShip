using System.Collections.Generic;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 브리프 4대 제약 검증기의 계약을 고정한다. <b>여기서 보는 것은 드레싱 내용이 아니라
    /// 검사 자체다</b> — 실제 에셋이 제약을 지키는지는 씬 빌드가 막고,
    /// 이 파일은 "위반을 넣으면 정말 걸리는가" 와
    /// "정상을 넣으면 안 걸리는가" 를 본다. 검사가 조용히 무력화되는 것이 위반보다 나쁘다.
    /// </summary>
    public sealed class LastShiftDressingRulesTests
    {
        private static LastShiftDressingProp Prop(string id, LastShiftDressingSpace space,
            LastShiftDressingSemantics semantics = LastShiftDressingSemantics.None) => new()
        {
            id = id,
            space = space,
            size = new Vector3(0.5f, 0.5f, 0.5f),
            anchorMode = LastShiftDressingAnchorMode.MetersFromSpaceCenter,
            anchor = Vector2.zero,
            bottomY = 0f,
            semantics = semantics
        };

        private static List<LastShiftDressingViolation> Validate(params LastShiftDressingProp[] props) =>
            LastShiftDressingRules.Validate(props);

        private static bool HasRule(IEnumerable<LastShiftDressingViolation> violations, string rule) =>
            violations.Any(v => v.Rule == rule);

        // ── 제약 1 · 노출 원뿔 ───────────────────────────────────────────────────────

        [Test]
        public void StateResponsivePropPastTheSafeBandIsRejected()
        {
            // 안전대 바로 밖. 중심이 아니라 상자가 차지하는 가장 큰 z 로 걸려야 한다.
            var cue = Prop("Frost", LastShiftDressingSpace.Of(LastShiftZone.Cooling),
                LastShiftDressingSemantics.StateResponsive);
            cue.anchor = new Vector2(0f, LastShiftDressingRules.StateCueSafeMaxZ + 0.5f);

            Assert.That(HasRule(Validate(cue), "C1_ExposureCone"), Is.True);
        }

        [Test]
        public void StateResponsivePropWhoseEdgeCrossesTheBandIsRejected()
        {
            // 중심은 안전대 안이지만 폭이 커서 모서리가 넘어가는 경우. 중심만 재는 검사로
            // 되돌아가면 이 테스트가 먼저 죽는다.
            var cue = Prop("WideFrost", LastShiftDressingSpace.Of(LastShiftZone.Cooling),
                LastShiftDressingSemantics.StateResponsive);
            cue.anchor = new Vector2(0f, LastShiftDressingRules.StateCueSafeMaxZ - 0.1f);
            cue.size = new Vector3(1f, 0.1f, 1.2f);

            Assert.That(HasRule(Validate(cue), "C1_ExposureCone"), Is.True);
        }

        [Test]
        public void StateResponsivePropInsideTheSafeBandPasses()
        {
            var cue = Prop("Frost", LastShiftDressingSpace.Of(LastShiftZone.Cooling),
                LastShiftDressingSemantics.StateResponsive);
            cue.anchor = new Vector2(0f, -2f);

            Assert.That(Validate(cue), Is.Empty);
        }

        [Test]
        public void StaticFixturePastTheSafeBandPasses()
        {
            // 제한 대상은 상태에 반응하는 단서뿐이다. 열교환기·배전반은 늘 같은 모습이라
            // 원뿔 안에 있어도 새는 정보가 없다 — 여기서 걸리기 시작하면 두 방의 설비를
            // 전부 좌현으로 몰아야 하고 방이 텅 빈다.
            var fixtureProp = Prop("HeatExchanger", LastShiftDressingSpace.Of(LastShiftZone.Cooling));
            fixtureProp.anchor = new Vector2(0f, 2.5f);

            Assert.That(Validate(fixtureProp), Is.Empty);
        }

        [Test]
        public void StateResponsivePropInAnUnwatchedZonePasses()
        {
            // 원뿔은 개구부 2(전력실↔냉각실)의 것이다. 조종석·산소실에는 안 걸린다.
            var cue = Prop("Readout", LastShiftDressingSpace.Of(LastShiftZone.Cockpit),
                LastShiftDressingSemantics.StateResponsive);
            cue.anchor = new Vector2(0f, 2.5f);

            Assert.That(Validate(cue), Is.Empty);
        }

        // ── 제약 2 · 해치 표식 금지 ──────────────────────────────────────────────────

        [Test]
        public void HatchMarkerOnALockedCompartmentIsRejected()
        {
            // 관측실이 아니라 서버/통신실을 쓴다 — 관측실은 확장 검토 §2 로 P0 상시 개방이
            // 됐고, 서버/통신실은 §2.2 가 "정보 우위에 접근 비용" 설계 전이라 P0 밖으로
            // 남긴 셋 중 하나다.
            var marker = Prop("Placard", LastShiftDressingSpace.Of(LastShiftCompartment.ServerRoom),
                LastShiftDressingSemantics.HatchMarker);

            Assume.That(LastShiftCompartments.Of(LastShiftCompartment.ServerRoom).Access,
                Is.EqualTo(LastShiftCompartmentAccess.Locked));
            Assert.That(HasRule(Validate(marker), "C2_HatchMarker"), Is.True);
        }

        [Test]
        public void HatchMarkerOnAnOpenedBowCompartmentPasses()
        {
            // 선수 사슬 넷은 P0 에서 열려 있으므로 표식이 흘릴 언락 상태가 없다(§21.4 는
            // "언락 전 구획" 에만 걸린다). 이 넷을 다시 잠글 때 이 테스트가 같이 뒤집힌다.
            var marker = Prop("CargoPlacard", LastShiftDressingSpace.Of(LastShiftCompartment.CargoBay),
                LastShiftDressingSemantics.HatchMarker);

            Assume.That(LastShiftCompartments.Of(LastShiftCompartment.CargoBay).Access,
                Is.EqualTo(LastShiftCompartmentAccess.Open));
            Assert.That(HasRule(Validate(marker), "C2_HatchMarker"), Is.False);
        }

        [Test]
        public void HatchMarkerOnTheEscapePodPasses()
        {
            // 구명정은 공간이 처음부터 열려 있고 기능만 잠겨 있다(§15.4). 표식이 흘릴
            // 언락 상태가 없으므로 브리프 §4.3 이 원칙 적용 대상에서 뺐다.
            var marker = Prop("PodPlacard", LastShiftDressingSpace.Of(LastShiftCompartment.EscapePod),
                LastShiftDressingSemantics.HatchMarker);

            Assume.That(LastShiftCompartments.Of(LastShiftCompartment.EscapePod).Access,
                Is.Not.EqualTo(LastShiftCompartmentAccess.Locked));
            Assert.That(Validate(marker), Is.Empty);
        }

        [Test]
        public void HatchMarkerOutsideACompartmentIsRejected()
        {
            var marker = Prop("CorridorSign", LastShiftDressingSpace.OfPassage(0),
                LastShiftDressingSemantics.HatchMarker);

            Assert.That(HasRule(Validate(marker), "C2_HatchMarker"), Is.True);
        }

        // ── 제약 3 · 게이지 · 사이렌 금지 ────────────────────────────────────────────

        [Test]
        public void PressureGaugeInACompartmentIsRejected()
        {
            var gauge = Prop("Gauge", LastShiftDressingSpace.Of(LastShiftCompartment.Workshop),
                LastShiftDressingSemantics.PressureGauge);

            Assert.That(HasRule(Validate(gauge), "C3_NoGauge"), Is.True);
        }

        [Test]
        public void SirenInACompartmentIsRejected()
        {
            var siren = Prop("Siren", LastShiftDressingSpace.Of(LastShiftCompartment.Hangar),
                LastShiftDressingSemantics.SirenEffect);

            Assert.That(HasRule(Validate(siren), "C3_NoSiren"), Is.True);
        }

        [Test]
        public void PressureGaugeInAPressureZonePasses()
        {
            // 금지 대상은 §24 미편입 구획 열한 개다. 압력 구역 안의 계기는 원래 그 방 것이다.
            var gauge = Prop("Gauge", LastShiftDressingSpace.Of(LastShiftZone.LifeSupport),
                LastShiftDressingSemantics.PressureGauge);

            Assert.That(Validate(gauge), Is.Empty);
        }

        [Test]
        public void RoomSystemReadoutNeedsAReason()
        {
            var led = Prop("RackLed", LastShiftDressingSpace.Of(LastShiftCompartment.ServerRoom),
                LastShiftDressingSemantics.RoomSystemReadout);

            Assert.That(HasRule(Validate(led), "C3_ReadoutReason"), Is.True);
        }

        [Test]
        public void RoomSystemReadoutInASanctionedRoomWithAReasonPasses()
        {
            var led = Prop("RackLed", LastShiftDressingSpace.Of(LastShiftCompartment.ServerRoom),
                LastShiftDressingSemantics.RoomSystemReadout);
            led.justification = "통신 상태 표현 — 브리프 §6.2";

            Assert.That(Validate(led), Is.Empty);
        }

        [Test]
        public void RoomSystemReadoutOutsideTheSanctionedRoomsIsRejected()
        {
            // 예외를 방 이름으로 못 박지 않으면 "이것도 그 방 고유 시스템" 이라는 말로
            // 열한 개 전부에 계기가 붙는다.
            var led = Prop("BasinLed", LastShiftDressingSpace.Of(LastShiftCompartment.Lavatory),
                LastShiftDressingSemantics.RoomSystemReadout);
            led.justification = "세면대 상태";

            Assert.That(HasRule(Validate(led), "C3_ReadoutRoom"), Is.True);
        }

        // ── 제약 4 · 우회 통로는 불편해야 한다 ───────────────────────────────────────

        [Test]
        public void ComfortFixtureInTheBypassIsRejected()
        {
            var rail = Prop("Handrail", LastShiftDressingSpace.OfBypassRun(),
                LastShiftDressingSemantics.Comfort);
            rail.bottomY = 0.2f;

            Assert.That(HasRule(Validate(rail), "C4_BypassComfort"), Is.True);
        }

        [Test]
        public void ComfortFixtureInTheAirlockBranchIsRejected()
        {
            var rail = Prop("Handrail", LastShiftDressingSpace.OfAirlock(),
                LastShiftDressingSemantics.Comfort);

            Assert.That(HasRule(Validate(rail), "C4_BypassComfort"), Is.True);
        }

        [Test]
        public void BypassLightsOverTheBudgetAreRejected()
        {
            var a = Prop("Lamp_A", LastShiftDressingSpace.OfBypassRun(), LastShiftDressingSemantics.LightSource);
            a.lightIntensity = LastShiftDressingRules.BypassLightBudget;
            var b = Prop("Lamp_B", LastShiftDressingSpace.OfBypassRun(), LastShiftDressingSemantics.LightSource);
            b.lightIntensity = 0.5f;

            Assert.That(HasRule(Validate(a, b), "C4_BypassLightBudget"), Is.True);
        }

        [Test]
        public void BypassLightsInsideTheBudgetPass()
        {
            var lane = Prop("DuctLane", LastShiftDressingSpace.OfBypassRun(), LastShiftDressingSemantics.LightSource);
            lane.lightIntensity = LastShiftDressingRules.BypassLightBudget;

            Assert.That(Validate(lane), Is.Empty);
        }

        [Test]
        public void LightsOutsideTheBypassDoNotSpendTheBudget()
        {
            // 예산은 관 안에서만 잰다. 갑판 위 조명까지 합산하면 배가 밝아질수록 관에
            // 유도띠 한 줄도 못 넣는 이상한 결합이 생긴다.
            var deck = Prop("DeckLamp", LastShiftDressingSpace.Of(LastShiftZone.Power),
                LastShiftDressingSemantics.LightSource);
            deck.lightIntensity = 50f;
            deck.anchor = new Vector2(0f, -2f);

            Assert.That(HasRule(Validate(deck), "C4_BypassLightBudget"), Is.False);
        }

        // ── 공통 검사 ────────────────────────────────────────────────────────────────

        [Test]
        public void PropThroughTheCeilingIsRejected()
        {
            var mast = Prop("Mast", LastShiftDressingSpace.Of(LastShiftCompartment.Lounge));
            mast.size = new Vector3(0.2f, LastShiftCompartments.InteriorHeight + 1f, 0.2f);

            Assert.That(HasRule(Validate(mast), "R1_Bounds"), Is.True);
        }

        [Test]
        public void PropBelowTheDeckIsRejected()
        {
            var sunken = Prop("Sunken", LastShiftDressingSpace.Of(LastShiftCompartment.Lounge));
            sunken.bottomY = -0.5f;

            Assert.That(HasRule(Validate(sunken), "R1_Bounds"), Is.True);
        }

        [Test]
        public void PropOutsideItsRoomIsRejected()
        {
            var stray = Prop("Stray", LastShiftDressingSpace.Of(LastShiftCompartment.Lounge));
            stray.anchor = new Vector2(60f, 0f);

            Assert.That(HasRule(Validate(stray), "R1_Bounds"), Is.True);
        }

        [Test]
        public void DuplicateIdsInTheSameSpaceAreRejected()
        {
            var a = Prop("Crate", LastShiftDressingSpace.Of(LastShiftCompartment.CargoBay));
            var b = Prop("Crate", LastShiftDressingSpace.Of(LastShiftCompartment.CargoBay));

            Assert.That(HasRule(Validate(a, b), "R0_Id"), Is.True);
        }

        [Test]
        public void SameIdInDifferentSpacesPasses()
        {
            // 소품은 공간별 루트 아래에 붙으므로 하이어라키에서 안 겹친다. 전역 유일을
            // 요구하면 방 이름을 접두어로 달아야 하고 이름이 두 번 반복된다.
            var a = Prop("Bench_Port", LastShiftDressingSpace.Of(LastShiftCompartment.Workshop));
            var b = Prop("Bench_Port", LastShiftDressingSpace.Of(LastShiftCompartment.Lounge));

            Assert.That(HasRule(Validate(a, b), "R0_Id"), Is.False);
        }

        [Test]
        public void UnitAnchorKeepsPropsInsideTheRoomWhateverTheSize()
        {
            // 단위좌표의 존재 이유가 이것이다 — 벽에 붙이라고 -1 을 적었을 때 소품이 커도
            // 벽을 뚫지 않는다. 이게 깨지면 art 가 미터로 적을 수밖에 없고, 그 순간
            // §17.4 치수 개정이 드레싱 전체를 조용히 망가뜨린다.
            foreach (var compartment in System.Enum.GetValues(typeof(LastShiftCompartment)).Cast<LastShiftCompartment>())
            foreach (var ux in new[] { -1f, 0f, 1f })
            foreach (var uz in new[] { -1f, 0f, 1f })
            {
                var prop = Prop($"Fit_{compartment}_{ux}_{uz}", LastShiftDressingSpace.Of(compartment));
                prop.anchorMode = LastShiftDressingAnchorMode.UnitOfSpace;
                prop.anchor = new Vector2(ux, uz);
                prop.size = new Vector3(2f, 1f, 2f);

                Assert.That(HasRule(LastShiftDressingRules.Validate(new[] { prop }), "R1_Bounds"), Is.False,
                    $"{compartment} 에서 단위좌표 ({ux}, {uz}) 가 방을 벗어난다.");
            }
        }

        // ── 상부 회랑 ────────────────────────────────────────────────────────────────

        [Test]
        public void GalleryBoundsFollowTheLegFootprint()
        {
            // 회랑 좌표 정본이 둘이 되는 것을 막는다. 드레싱이 자기 사본을 들면 구획 하나가
            // 움직였을 때 회랑은 따라가고 소품만 제자리에 남는다.
            for (var index = 0; index < LastShiftUpperGallery.LegCount; index++)
            {
                var leg = LastShiftUpperGallery.LegAt(index);
                var bounds = LastShiftDressingSpaces.BoundsOf(LastShiftDressingSpace.OfGallery(index));

                Assert.That(bounds.MinX, Is.EqualTo(leg.MinX).Within(0.0001f), $"{leg.Name} MinX");
                Assert.That(bounds.MaxX, Is.EqualTo(leg.MaxX).Within(0.0001f), $"{leg.Name} MaxX");
                Assert.That(bounds.MinZ, Is.EqualTo(leg.MinZ).Within(0.0001f), $"{leg.Name} MinZ");
                Assert.That(bounds.MaxZ, Is.EqualTo(leg.MaxZ).Within(0.0001f), $"{leg.Name} MaxZ");
                Assert.That(bounds.FloorY, Is.EqualTo(0f).Within(0.0001f), $"{leg.Name} FloorY");
                Assert.That(bounds.CeilingY,
                    Is.EqualTo(LastShiftUpperGallery.InteriorHeight).Within(0.0001f), $"{leg.Name} CeilingY");
            }
        }

        [Test]
        public void GalleryLegsAreSeparateSpacesForIdUniqueness()
        {
            // 다리마다 같은 이름을 쓸 수 있어야 한다 — 등은 어느 다리에서나 Lamp_0 이다.
            // 반대로 한 다리 안에서 겹치면 걸려야 한다. 이 둘이 같이 성립해야 다리 번호가
            // 실제로 공간을 가르는 값이 된다.
            var run = LastShiftDressingSpace.OfGalleryRun();
            var descent = LastShiftDressingSpace.OfGallery(LastShiftUpperGallery.DescentLeg);

            Assert.That(HasRule(Validate(Prop("Lamp_0", run), Prop("Lamp_0", descent)), "R0_Id"), Is.False);
            Assert.That(HasRule(Validate(Prop("Lamp_0", run), Prop("Lamp_0", run)), "R0_Id"), Is.True);
        }

        [Test]
        public void GalleryPropOutsideItsLegIsRejected()
        {
            // 강하 다리는 폭이 2m 뿐이다. 긴 구간 좌표를 그대로 적으면 다리 밖으로 나간다.
            var descent = LastShiftDressingSpace.OfGallery(LastShiftUpperGallery.DescentLeg);
            var stray = Prop("Stray", descent);
            stray.anchor = new Vector2(LastShiftUpperGallery.Width * 2f, 0f);

            Assert.That(HasRule(Validate(stray), "R1_Bounds"), Is.True);
        }

        [Test]
        public void GalleryComfortIsAllowed()
        {
            // 유도띠는 Comfort 다. 그 금지는 우회 통로 전용이라(§5) 회랑에 옮겨 붙으면
            // art §4.3 이 근거를 대고 만든 띠 일곱 장이 통째로 위반이 된다.
            var band = Prop("Trim", LastShiftDressingSpace.OfGalleryRun(),
                LastShiftDressingSemantics.Comfort);

            Assert.That(HasRule(Validate(band), "C4_BypassComfort"), Is.False);
        }

        [Test]
        public void GalleryLightIsOutsideTheBypassBudget()
        {
            // 회랑 등 열둘의 합(15.12)은 우회 통로 예산 2.0 을 한참 넘는다. 회랑이 그
            // 예산에 섞이면 등을 다는 순간 관 쪽 위반으로 잡힌다.
            var props = new List<LastShiftDressingProp>();
            for (var index = 0; index < 12; index++)
            {
                var lamp = Prop($"Lamp_{index}", LastShiftDressingSpace.OfGalleryRun(),
                    LastShiftDressingSemantics.LightSource);
                lamp.lightIntensity = 1.26f;
                props.Add(lamp);
            }

            var violations = LastShiftDressingRules.Validate(props);
            Assert.That(HasRule(violations, "C4_BypassLightBudget"), Is.False);
            Assert.That(HasRule(violations, "C4_AirlockLightBudget"), Is.False);
        }

        // ── 제약 5 · 문 통행 폭 ─────────────────────────────────────────────────────

        /// <summary>
        /// 냉각실↔통로B 문 앞에 <paramref name="halfWidth"/> 만큼 z 로 뻗은 상자를 세운다.
        /// 2026-08-08 플레이테스트에서 실제로 그 자리에 있던 <c>CrateStack_Aft</c> 와 같은 배치다.
        /// </summary>
        private static LastShiftDressingProp CoolingDoorProp(string id, float centerZ, float sizeZ,
            float sizeY = 1.55f)
        {
            var prop = Prop(id, LastShiftDressingSpace.Of(LastShiftZone.Cooling));
            prop.size = new Vector3(1f, sizeY, sizeZ);
            // 냉각실 방 중심에서 문 평면(x = +5) 쪽으로 붙인다.
            var doorX = LastShiftShipDimensions.OpeningX(3);
            prop.anchor = new Vector2(doorX - LastShiftShipDimensions.RoomCenterX(LastShiftZone.Cooling) - 0.5f,
                centerZ);
            return prop;
        }

        [Test]
        public void PropAcrossADoorwayIsRejected()
        {
            var door = LastShiftShipDimensions.OpeningCenterZ(3);
            var crate = CoolingDoorProp("CrateStack", door, 0.8f);

            Assert.That(HasRule(Validate(crate), "C5_DoorwayClearance"), Is.True);
        }

        [Test]
        public void PropBesideADoorwayIsAllowed()
        {
            // 문 구멍 밖으로 완전히 비켜난 상자. 문 앞이라도 구멍을 안 물면 통행이 남는다.
            var door = LastShiftShipDimensions.OpeningCenterZ(3);
            var crate = CoolingDoorProp("CrateStack",
                door - LastShiftZoneDoor.OpeningWidth * 0.5f - 0.6f, 0.8f);

            Assert.That(HasRule(Validate(crate), "C5_DoorwayClearance"), Is.False);
        }

        [Test]
        public void TwoPropsPinchingOppositeEdgesAreRejectedTogether()
        {
            // 하나씩 재면 둘 다 통과한다 — 각각 한쪽 끝만 조금 물어 반대쪽에 넓은 토막이
            // 남기 때문이다. 문 단위로 합쳐야 가운데 한 토막만 남은 것이 보인다.
            var door = LastShiftShipDimensions.OpeningCenterZ(3);
            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
            var fore = CoolingDoorProp("PinchFore", door - half, 0.8f);
            var aft = CoolingDoorProp("PinchAft", door + half, 0.8f);

            Assert.That(HasRule(Validate(fore), "C5_DoorwayClearance"), Is.False);
            Assert.That(HasRule(Validate(aft), "C5_DoorwayClearance"), Is.False);
            Assert.That(HasRule(Validate(fore, aft), "C5_DoorwayClearance"), Is.True);
        }

        [Test]
        public void DeckDecalInADoorwayIsAllowed()
        {
            // 갑판 띠·격자는 밟고 지나간다. 이걸 세면 문 앞 갑판 표시가 전부 위반이 된다.
            var door = LastShiftShipDimensions.OpeningCenterZ(3);
            var band = CoolingDoorProp("DeckGrate", door, 0.9f, LastShiftDoorways.WalkOverHeight * 0.5f);

            Assert.That(HasRule(Validate(band), "C5_DoorwayClearance"), Is.False);
        }

        [Test]
        public void LockedCompartmentDoorsAreNotTracked()
        {
            // 잠긴 구획의 문은 구멍이 아니라 메운 판이다(§15.2). 그 앞을 비우라고 요구하면
            // 서버실·수경재배·의무실 벽 앞이 전부 통행 예약 구역이 된다.
            var locked = new[]
            {
                LastShiftCompartment.ServerRoom,
                LastShiftCompartment.Hydroponics,
                LastShiftCompartment.MedBay
            };

            foreach (var compartment in locked)
                Assert.That(LastShiftDoorways.All.Any(d => d.Name == LastShiftCompartments.NameOf(compartment)),
                    Is.False, $"{compartment} 는 잠겨 있는데 통행 문 목록에 들어 있다.");
        }
    }
}
