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
        public void DuctSectionMatchesTheCrouchPosture()
        {
            // 단면과 웅크림 높이가 어긋나면 "웅크렸는데도 안 들어가는" 통로가 된다.
            Assert.That(LastShiftBypassDuct.Section,
                Is.EqualTo(LastShiftShipPhysics.CrouchHeight).Within(Tolerance));
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
            var rise = -LastShiftBypassDuct.FloorY;
            Assert.That(rise, Is.LessThan(LastShiftShipPhysics.JumpApexHeight),
                $"승강구 깊이 {rise:F2}m 가 점프 정점 {LastShiftShipPhysics.JumpApexHeight:F2}m 를 넘는다 — 새 조작 동사가 필요해진다.");

            // 단을 밟고 오르면 여유가 두 배가 된다. 단 높이는 걸어서 오르는 높이여야 한다.
            var riseFromStep = rise - LastShiftBypassDuct.StepHeight;
            Assert.That(LastShiftShipPhysics.JumpApexHeight - riseFromStep,
                Is.GreaterThan(LastShiftShipPhysics.JumpApexHeight - rise),
                "단이 여유를 안 늘린다.");
            Assert.That(LastShiftBypassDuct.StepHeight, Is.LessThanOrEqualTo(0.3f),
                "CharacterController.stepOffset 기본값을 넘으면 걸어서 못 오른다.");
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
