using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 갑판 하부 우회 통로(<c>docs/corridor-4p-redesign-v1.md</c> §5, §23)의 기하·비용 조건을
    /// 고정한다.
    ///
    /// 여기서 지키는 것은 형상이 아니라 <b>설계 의도</b>다. §5 가 우회로에 산소 비용을 건 이유가
    /// "비용이 없으면 주 통로 설계(배플·게이지·판독 3단) 전체가 장식이 된다" 이므로, 우회로가
    /// 더 빠르거나 공짜가 되는 순간 이 문서의 절반이 무의미해진다. 그 조건들을 코드로 박는다.
    /// </summary>
    public sealed class LastShiftBypassDuctTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void CrouchedCrewActuallyFitsInsideTheDuctSection()
        {
            // 이 검사가 원래 "단면 == 웅크림 높이" 였고, 그게 곧 버그였다 — 캡슐이 관에 딱
            // 맞으면 여유가 0 이라 CharacterController 는 천장에 닿은 채로 못 움직인다.
            // 지켜야 하는 것은 같음이 아니라 <b>들어간다</b> 이고, 여유는 컨트롤러가 접촉을
            // 만드는 거리(skinWidth) 위아래 한 겹이다.
            Assert.That(LastShiftBypassDuct.Section,
                Is.EqualTo(LastShiftShipPhysics.CrouchSection).Within(Tolerance),
                "덕트 단면과 웅크림 단면 정본이 갈라졌다.");
            Assert.That(LastShiftShipPhysics.CrouchHeight + LastShiftShipPhysics.CrewSkinWidth,
                Is.LessThan(LastShiftBypassDuct.Section),
                "웅크린 캡슐이 덕트 천장에 닿는다 — 웅크려도 못 들어가는 통로다.");

            // 폭도 같은 단면이다. 높이만 보고 폭을 안 보면 L 자 모서리에서 걸린다.
            Assert.That(LastShiftShipPhysics.CrewRadius * 2f + LastShiftShipPhysics.CrewSkinWidth,
                Is.LessThan(LastShiftBypassDuct.Section),
                "승무원 캡슐 지름이 덕트 폭을 채운다 — 관 안에서 옆으로 낀다.");

            Assert.That(LastShiftShipPhysics.CrouchHeight,
                Is.GreaterThan(LastShiftShipPhysics.CrewRadius * 2f),
                "웅크림 높이가 캡슐 지름보다 낮으면 CharacterController 가 높이를 되돌린다.");
            Assert.That(LastShiftShipPhysics.CrouchHeight,
                Is.LessThan(LastShiftShipPhysics.StandingHeight),
                "웅크림이 서 있는 높이보다 낮아야 자세가 의미를 갖는다.");
            Assert.That(LastShiftShipPhysics.CrouchEyeHeight,
                Is.LessThan(LastShiftShipPhysics.CrouchHeight),
                "눈이 웅크린 캡슐 밖으로 나가면 천장을 뚫고 본다.");
        }

        [Test]
        public void BypassCostsMoreThanTheMainCorridor()
        {
            // §5 의 존재 이유. 우회로가 더 짧으면 "급할 때만 쓰는 진짜 우회로" 가 아니라
            // 그냥 지름길이고, 그러면 주 통로의 배플·게이지·판독 설계가 통째로 장식이 된다.
            Assert.That(LastShiftBypassDuct.TravelDistance,
                Is.GreaterThan(LastShiftBypassDuct.MainCorridorDistance),
                "우회로가 주 통로보다 짧다 — 비용이 아니라 지름길이 됐다.");

            // 속도까지 보면 실제 통행 시간 차이는 더 벌어진다. 웅크림 속도가 걷는 속도보다
            // 느려야 거리 차이가 시간 차이로 이어진다.
            Assert.That(LastShiftShipPhysics.CrouchSpeed,
                Is.LessThan(LastShiftPlayerController.CarrySpeed),
                "웅크림이 물건 든 속도보다 빠르면 우회로가 주 통로보다 빨라질 수 있다.");
        }

        [Test]
        public void BypassSkipsBothMiddleRoomsAndEntersOnlyFromEndRooms()
        {
            // §5: 진입점은 조종석 쪽·산소실 쪽이고 전력실·냉각실 내부에는 두지 않는다.
            // 두 방 안에 진입점이 생기면 우회로가 그 방을 "건너뛰는" 것이 아니게 된다.
            var fore = new Vector3(LastShiftBypassDuct.ForeShaftX, 0f, 0f);
            var aft = new Vector3(LastShiftBypassDuct.AftShaftX, 0f, 0f);
            Assert.That(LastShiftZoneAtlas.Resolve(fore), Is.EqualTo(LastShiftZone.Cockpit));
            Assert.That(LastShiftZoneAtlas.Resolve(aft), Is.EqualTo(LastShiftZone.LifeSupport));

            Assert.That(LastShiftBypassDuct.ForeShaftX,
                Is.GreaterThanOrEqualTo(LastShiftShipDimensions.RoomMinX(LastShiftZone.Cockpit)),
                "선수 진입점이 조종석 방 밖이다.");
            Assert.That(LastShiftBypassDuct.AftShaftX,
                Is.LessThanOrEqualTo(LastShiftShipDimensions.RoomMaxX(LastShiftZone.LifeSupport)),
                "선미 진입점이 산소실 방 밖이다.");
        }

        [Test]
        public void DuctSitsEntirelyUnderTheDeck()
        {
            // 갑판 위로 올라오면 방 안에 관이 지나가고, §23.2 가 평면을 기각한 이유가 무너진다.
            Assert.That(LastShiftBypassDuct.CeilingY, Is.LessThanOrEqualTo(Tolerance),
                "덕트 천장이 갑판 위로 올라왔다.");
            Assert.That(LastShiftBypassDuct.AirlockFloorY,
                Is.LessThan(LastShiftBypassDuct.AirlockCeilingY),
                "에어록 바닥이 천장보다 높다.");
            Assert.That(LastShiftBypassDuct.AirlockCeilingY,
                Is.EqualTo(LastShiftBypassDuct.FloorY).Within(Tolerance),
                "에어록 천장이 덕트 바닥에 안 붙어 있으면 안쪽 해치가 허공에 뜬다.");
        }

        [Test]
        public void ShaftRiseStaysInsideTheJumpEnvelope()
        {
            // §23.6 의 판정. 사다리 같은 새 조작 동사 없이 기존 점프로 나올 수 있어야 한다.
            // 승강구 바닥에 단을 두지 않으므로 이 상승이 전부다.
            var rise = -LastShiftBypassDuct.FloorY;
            Assert.That(rise, Is.LessThan(LastShiftShipPhysics.JumpApexHeight),
                $"승강구 깊이 {rise:F2}m 가 점프 정점 {LastShiftShipPhysics.JumpApexHeight:F2}m 를 넘는다 — 새 조작 동사가 필요해진다.");

            // 승강구 발밑은 한 변이 Section 인 정사각형뿐이다. 여기에 단을 세우면 그 위에 선
            // 승무원의 머리가 덕트 천장 위로 나오고, 내려설 자리가 캡슐 지름보다 좁아 낀다 —
            // 실제로 "웅크려도 통로에 못 들어간다" 가 그렇게 났다. 이 검사가 그 자리를 지킨다.
            Assert.That(LastShiftBypassDuct.Section,
                Is.GreaterThan(LastShiftShipPhysics.CrewRadius * 2f + LastShiftShipPhysics.CrewSkinWidth),
                "승강구 발밑에 캡슐 하나가 안 들어간다.");
            Assert.That(LastShiftBypassDuct.RecoveryRise,
                Is.EqualTo(rise).Within(Tolerance),
                "승강구 상승이 단으로 쪼개졌다 — 그 단이 천장과 승무원을 낀다.");
        }

        /// <summary>
        /// 갑판 위에서 승강구를 덮는 것이 없어야 한다. 통로 안 치수가 다 맞아도 구멍 위에
        /// 설비가 걸쳐 있으면 들어갈 방법이 없다 — Tether 받침대가 실제로 <c>0.3m</c> 를
        /// 덮고 있었고, 좌표 검사가 전부 통과하는 채로 조종석 승강구가 막혀 있었다.
        /// </summary>
        [Test]
        public void ForeShaftIsNotCoveredByTheTetherRack()
        {
            var rackHalf = LastShiftShipDimensions.TetherRackScale * 0.5f;
            var rackCenter = LastShiftShipDimensions.TetherRackPosition;
            var shaftHalf = LastShiftBypassDuct.Section * 0.5f;
            var mouth = LastShiftBypassDuct.ShaftMouth(LastShiftBypassDuct.ForeShaft);

            var gapX = Mathf.Abs(rackCenter.x - mouth.x) - rackHalf.x - shaftHalf;
            var gapZ = Mathf.Abs(rackCenter.z - mouth.z) - rackHalf.z - shaftHalf;
            Assert.That(Mathf.Max(gapX, gapZ),
                Is.GreaterThanOrEqualTo(LastShiftBypassDuct.DeckPropClearance - Tolerance),
                $"받침대가 선수 승강구 위에 걸친다 — x 여유 {gapX:F2}, z 여유 {gapZ:F2}.");

            // 반대쪽으로 밀려 벽에 붙어도 안 된다. 구멍 옆에 설 자리가 없으면 결과가 같다.
            Assert.That(mouth.z - shaftHalf,
                Is.GreaterThan(-LastShiftShipDimensions.HalfWidth),
                "승강구가 좌현 벽을 파고든다.");
            Assert.That(LastShiftZoneAtlas.Resolve(mouth), Is.EqualTo(LastShiftZone.Cockpit),
                "승강구가 조종석 방 밖으로 나갔다.");
        }

        [Test]
        public void BendCountIsOneAndTheRouteActuallyBends()
        {
            // §8 미결 4 의 답. 꺾임이 0 이면 두 승강구가 일직선이라 관통선이 남는다.
            Assert.That(LastShiftBypassDuct.BendCount, Is.EqualTo(1));
            Assert.That(LastShiftBypassDuct.ForeShaftZ,
                Is.Not.EqualTo(LastShiftBypassDuct.RunZ).Within(Tolerance),
                "두 승강구의 z 가 같으면 꺾임이 없다.");
        }

        [Test]
        public void ContainsSeparatesInsideFromTheRoomsAbove()
        {
            // 이 판정이 틀리면 덕트 안 승무원이 머리 위 방의 압력을 받아 산소를 안 태운다.
            var insideRun = new Vector3(0f, LastShiftBypassDuct.FloorY + LastShiftBypassDuct.Section * 0.5f,
                LastShiftBypassDuct.RunZ);
            Assert.That(LastShiftBypassDuct.Contains(insideRun), Is.True, "덕트 한가운데가 밖으로 잡힌다.");

            var insideAirlock = new Vector3(LastShiftBypassDuct.AirlockCenterX,
                (LastShiftBypassDuct.AirlockFloorY + LastShiftBypassDuct.AirlockCeilingY) * 0.5f,
                LastShiftBypassDuct.AirlockCenterZ);
            Assert.That(LastShiftBypassDuct.Contains(insideAirlock), Is.True, "에어록 안이 밖으로 잡힌다.");

            // 바로 위 방 안, 그리고 선체 밖은 안 잡혀야 한다.
            Assert.That(LastShiftBypassDuct.Contains(new Vector3(0f, 1f, LastShiftBypassDuct.RunZ)), Is.False,
                "갑판 위 좌표가 덕트 안으로 잡힌다.");
            Assert.That(LastShiftBypassDuct.Contains(LastShiftShipDimensions.SpawnPoint), Is.False,
                "스폰 지점이 덕트 안으로 잡히면 시작하자마자 산소가 탄다.");
        }

        [Test]
        public void DuctIsUnpressurizedButNotAPressureZone()
        {
            // §5 는 비용을 요구하고 §24 는 압력존 4구역 고정을 요구한다. 둘 다 만족해야 한다 —
            // 구역으로 편입하면 Resolve()·게이지·SIMUL_ZONES·RG-1 이 전부 따라와야 한다.
            Assert.That(LastShiftBypassDuct.IsUnpressurized, Is.True);
            Assert.That(LastShiftZoneAtlas.ZoneCount, Is.EqualTo(4),
                "덕트가 압력존에 편입되면 이 값이 늘어난다 — §24 는 4 고정이다.");
        }
    }
}
