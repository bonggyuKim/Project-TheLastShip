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
        public void NoFixedCompartmentIsLockedSoTheHatchMarkerRuleHasNoTarget()
        {
            // <b>이 규칙의 대상이 M-2 에서 0 이 됐다</b>(조항 K-2). 잠긴 셋(서버/통신실·
            // 수경재배·의무실)이 전부 자유 배치 카탈로그로 갔고, 배치된 모듈은 언제나
            // Open 으로 선다 — 언락 전에 새어 나갈 상태 자체가 없다.
            //
            // <b>규칙을 지우지 않는 이유가 이 검사다.</b> 메타 진행 백본이 붙으면 Locked 가
            // 다시 쓰이고, 그때 표식 금지가 같이 돌아와야 한다. 여기서 확인하는 것은
            // "지금 잠긴 방이 없다" 이지 "규칙이 없다" 가 아니다.
            Assert.That(
                LastShiftCompartments.FixedSpecs.Any(
                    spec => spec.Access == LastShiftCompartmentAccess.Locked),
                Is.False,
                "잠긴 고정 구획이 생겼다 — 해치 표식 금지가 다시 대상을 갖는다.");
        }

        [Test]
        public void HatchMarkerOnAnOpenCompartmentPasses()
        {
            // 숙소는 언제나 열려 있으므로 표식이 흘릴 언락 상태가 없다(§21.4 는 "언락 전
            // 구획" 에만 걸린다).
            var marker = Prop("QuartersPlacard", LastShiftDressingSpace.Of(LastShiftCompartment.Quarters),
                LastShiftDressingSemantics.HatchMarker);

            Assume.That(LastShiftCompartments.Of(LastShiftCompartment.Quarters).Access,
                Is.EqualTo(LastShiftCompartmentAccess.Open));
            Assert.That(HasRule(Validate(marker), "C2_HatchMarker"), Is.False);
        }

        [Test]
        public void HatchMarkerOutsideACompartmentIsRejected()
        {
            var marker = Prop("PlazaSign", LastShiftDressingSpace.OfPlaza(),
                LastShiftDressingSemantics.HatchMarker);

            Assert.That(HasRule(Validate(marker), "C2_HatchMarker"), Is.True);
        }

        // ── 제약 3 · 게이지 · 사이렌 금지 ────────────────────────────────────────────

        [Test]
        public void PressureGaugeInACompartmentIsRejected()
        {
            var gauge = Prop("Gauge", LastShiftDressingSpace.Of(LastShiftCompartment.Quarters),
                LastShiftDressingSemantics.PressureGauge);

            Assert.That(HasRule(Validate(gauge), "C3_NoGauge"), Is.True);
        }

        [Test]
        public void SirenInACompartmentIsRejected()
        {
            var siren = Prop("Siren", LastShiftDressingSpace.Of(LastShiftCompartment.Quarters),
                LastShiftDressingSemantics.SirenEffect);

            Assert.That(HasRule(Validate(siren), "C3_NoSiren"), Is.True);
        }

        [Test]
        public void PressureGaugeInAPressureZonePasses()
        {
            // 금지 대상은 §24 미편입 구획이다. 압력 구역 안의 계기는 원래 그 방 것이다.
            var gauge = Prop("Gauge", LastShiftDressingSpace.Of(LastShiftZone.LifeSupport),
                LastShiftDressingSemantics.PressureGauge);

            Assert.That(Validate(gauge), Is.Empty);
        }

        [Test]
        public void RoomSystemReadoutNeedsAReason()
        {
            // 사유 검사는 공간 종류와 무관하다 — 구획 밖(압력 구역)에서도 걸린다.
            var led = Prop("RackLed", LastShiftDressingSpace.Of(LastShiftZone.LifeSupport),
                LastShiftDressingSemantics.RoomSystemReadout);

            Assert.That(HasRule(Validate(led), "C3_ReadoutReason"), Is.True);
        }

        [Test]
        public void NoCompartmentIsSanctionedForARoomSystemReadoutAnyMore()
        {
            // 브리프가 이름을 댄 넷(수경재배·서버통신실·의무실·구명정)은 M-2 에서 전부
            // 배를 떠났다(맵 개편 §3.2). 사유를 적어도 통과 못 하는 것이 맞다 — 예외를
            // <b>방 이름으로</b> 못 박는 것이 제약 3 의 실체이고, 이름이 없으면 예외도 없다.
            var led = Prop("BunkLed", LastShiftDressingSpace.Of(LastShiftCompartment.Quarters),
                LastShiftDressingSemantics.RoomSystemReadout);
            led.justification = "침상 점유 표시";

            Assert.That(HasRule(Validate(led), "C3_ReadoutRoom"), Is.True,
                "고정 방에 고유 시스템 계기가 통과했다 — 모듈에 고유 계기를 허용할지는 " +
                "game-planning 결정이 있어야 열리는 문이다.");
            Assert.That(HasRule(Validate(led), "C3_ReadoutReason"), Is.False,
                "사유를 적었는데 사유 누락으로도 걸린다 — 두 검사가 섞였다.");
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
            var mast = Prop("Mast", LastShiftDressingSpace.Of(LastShiftCompartment.Quarters));
            mast.size = new Vector3(0.2f, LastShiftCompartments.InteriorHeight + 1f, 0.2f);

            Assert.That(HasRule(Validate(mast), "R1_Bounds"), Is.True);
        }

        [Test]
        public void PropBelowTheDeckIsRejected()
        {
            var sunken = Prop("Sunken", LastShiftDressingSpace.Of(LastShiftCompartment.Quarters));
            sunken.bottomY = -0.5f;

            Assert.That(HasRule(Validate(sunken), "R1_Bounds"), Is.True);
        }

        [Test]
        public void PropOutsideItsRoomIsRejected()
        {
            var stray = Prop("Stray", LastShiftDressingSpace.Of(LastShiftCompartment.Quarters));
            stray.anchor = new Vector2(60f, 0f);

            Assert.That(HasRule(Validate(stray), "R1_Bounds"), Is.True);
        }

        [Test]
        public void DuplicateIdsInTheSameSpaceAreRejected()
        {
            var a = Prop("Crate", LastShiftDressingSpace.Of(LastShiftCompartment.Quarters));
            var b = Prop("Crate", LastShiftDressingSpace.Of(LastShiftCompartment.Quarters));

            Assert.That(HasRule(Validate(a, b), "R0_Id"), Is.True);
        }

        [Test]
        public void SameIdInDifferentSpacesPasses()
        {
            // 소품은 공간별 루트 아래에 붙으므로 하이어라키에서 안 겹친다. 전역 유일을
            // 요구하면 방 이름을 접두어로 달아야 하고 이름이 두 번 반복된다.
            var a = Prop("Bench_Port", LastShiftDressingSpace.Of(LastShiftCompartment.Quarters));
            var b = Prop("Bench_Port", LastShiftDressingSpace.OfPlaza());

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

        // ── 제약 5 · 문 통행 폭 ─────────────────────────────────────────────────────

        /// <summary>
        /// 냉각실↔통로B 문 앞에 <paramref name="halfWidth"/> 만큼 z 로 뻗은 상자를 세운다.
        /// 2026-08-08 플레이테스트에서 실제로 그 자리에 있던 <c>CrateStack_Aft</c> 와 같은 배치다.
        /// </summary>
        private static LastShiftDressingProp CoolingDoorProp(string id, float centerAlongDoor, float sizeAlongDoor,
            float sizeY = 1.55f)
        {
            var prop = Prop(id, LastShiftDressingSpace.Of(LastShiftZone.Cooling));
            // <b>문의 자유축이 z 에서 x 로 바뀌었다.</b> 냉각실 문은 이제 광장 우현 변
            // (z = +6)에 있고, 그 구멍을 막는 방향은 x 다. 크기를 그대로 두면
            // 상자가 문 앞을 가로로 가로지르는 것이 아니라 깊이로 서서 아무것도 안 막는다.
            prop.size = new Vector3(sizeAlongDoor, sizeY, 1f);

            var door = LastShiftPlazaLayout.DoorOf(LastShiftPlazaSpace.CoolingRoom);
            // 문 평면에서 방 안쪽으로 0.5m. 문틀에 딱 붙이면 ApproachDepth 검사가
            // 재는 띄 밖으로 나가 문을 막아도 통과한다.
            var insideZ = door.Plane + 0.5f;
            prop.anchor = new Vector2(
                centerAlongDoor - LastShiftShipDimensions.RoomCenterX(LastShiftZone.Cooling),
                insideZ - LastShiftShipDimensions.RoomCenterZ(LastShiftZone.Cooling));
            return prop;
        }

        [Test]
        public void PropAcrossADoorwayIsRejected()
        {
            var door = LastShiftPlazaLayout.DoorOf(LastShiftPlazaSpace.CoolingRoom).Center;
            var crate = CoolingDoorProp("CrateStack", door, 0.8f);

            Assert.That(HasRule(Validate(crate), "C5_DoorwayClearance"), Is.True);
        }

        [Test]
        public void PropBesideADoorwayIsAllowed()
        {
            // 문 구멍 밖으로 완전히 비켜난 상자. 문 앞이라도 구멍을 안 물면 통행이 남는다.
            var door = LastShiftPlazaLayout.DoorOf(LastShiftPlazaSpace.CoolingRoom).Center;
            var crate = CoolingDoorProp("CrateStack",
                door - LastShiftZoneDoor.OpeningWidth * 0.5f - 0.6f, 0.8f);

            Assert.That(HasRule(Validate(crate), "C5_DoorwayClearance"), Is.False);
        }

        [Test]
        public void TwoPropsPinchingOppositeEdgesAreRejectedTogether()
        {
            // 하나씩 재면 둘 다 통과한다 — 각각 한쪽 끝만 조금 물어 반대쪽에 넓은 토막이
            // 남기 때문이다. 문 단위로 합쳐야 가운데 한 토막만 남은 것이 보인다.
            var door = LastShiftPlazaLayout.DoorOf(LastShiftPlazaSpace.CoolingRoom).Center;
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
            var door = LastShiftPlazaLayout.DoorOf(LastShiftPlazaSpace.CoolingRoom).Center;
            var band = CoolingDoorProp("DeckGrate", door, 0.9f, LastShiftDoorways.WalkOverHeight * 0.5f);

            Assert.That(HasRule(Validate(band), "C5_DoorwayClearance"), Is.False);
        }

        [Test]
        public void OnlyPassableCompartmentDoorsAreTracked()
        {
            // 잠긴 구획의 문은 구멍이 아니라 메운 판이라(§15.2) 통행 문 목록에서 빠진다.
            // 지금 잠긴 고정 구획은 없으므로(조항 K-2) 이 검사가 지키는 것은 반대 방향이다 —
            // <b>열린 방의 문은 반드시 들어 있다.</b> 빠지면 그 앞에 소품을 놓아도 아무도
            // 안 보고, 배에 하나뿐인 고정 방의 문이 막힌 채로 통과한다.
            foreach (var spec in LastShiftCompartments.FixedSpecs)
                Assert.That(
                    LastShiftDoorways.All.Any(d => d.Name == LastShiftCompartments.NameOf(spec.Compartment)),
                    Is.EqualTo(spec.IsPassable),
                    $"{spec.Compartment}(passable={spec.IsPassable}) 가 통행 문 목록과 안 맞는다.");
        }
    }
}
