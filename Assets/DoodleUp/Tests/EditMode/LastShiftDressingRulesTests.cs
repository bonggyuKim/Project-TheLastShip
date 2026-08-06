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
            var marker = Prop("Placard", LastShiftDressingSpace.Of(LastShiftCompartment.Observatory),
                LastShiftDressingSemantics.HatchMarker);

            Assume.That(LastShiftCompartments.Of(LastShiftCompartment.Observatory).Access,
                Is.EqualTo(LastShiftCompartmentAccess.Locked));
            Assert.That(HasRule(Validate(marker), "C2_HatchMarker"), Is.True);
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
        public void DuplicateIdsAreRejected()
        {
            var a = Prop("Crate", LastShiftDressingSpace.Of(LastShiftCompartment.CargoBay));
            var b = Prop("Crate", LastShiftDressingSpace.Of(LastShiftCompartment.Hangar));

            Assert.That(HasRule(Validate(a, b), "R0_Id"), Is.True);
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
    }
}
