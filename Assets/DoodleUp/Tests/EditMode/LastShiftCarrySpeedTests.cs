using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// CARRY_SPEED. 기획이 지정한 것은 "PatchPlate 와 CoolingCanister 를 든 동안에만"
    /// 이고, 설계 요구는 <c>CARRY_SPEED &lt; 3.5</c> 하나다. 값 2.8 자체는 game-balance
    /// 검증 대상이므로 여기서는 값이 아니라 <b>어느 부품이 걸리는가</b>와 그 결과가
    /// hold 8초를 넘기는가를 고정한다.
    ///
    /// 판정을 역할 이름이 아니라 치수로 두었기 때문에 이 테스트가 필요하다 — 치수 기준이
    /// 조용히 어긋나면 밧줄을 들고 느려지거나 판자를 들고 안 느려진다.
    /// </summary>
    public sealed class LastShiftCarrySpeedTests
    {
        [Test]
        public void CarrySpeedStaysUnderTheSoloRoundTripRequirement()
        {
            Assert.That(LastShiftPlayerController.CarrySpeed, Is.LessThan(3.5f),
                "설계 요구는 CARRY_SPEED < 3.5 다.");
            Assert.That(LastShiftPlayerController.CarrySpeed, Is.LessThan(LastShiftPlayerController.MoveSpeed),
                "물건을 들면 느려져야 한다. 같거나 빠르면 이 상수가 아무 일도 하지 않는다.");
        }

        [Test]
        public void OnlyBulkyPartsSlowTheCrewDown()
        {
            // 씬 빌더가 쓰는 실제 치수다. 여기가 빌더와 어긋나면 게임에서 걸리는 부품이 달라진다.
            Assert.That(IsBulky(new Vector3(1.15f, 1.15f, 0.18f)), Is.True, "PatchPlate 는 느려져야 한다.");
            Assert.That(IsBulky(new Vector3(0.55f, 1.10f, 0.55f)), Is.True, "CoolingCanister 는 느려져야 한다.");

            Assert.That(IsBulky(new Vector3(0.65f, 0.65f, 0.90f)), Is.False,
                "Battery 는 그대로다. 부피로 판정하면 여기가 뒤집힌다(Battery 0.380 > PatchPlate 0.238).");
            Assert.That(IsBulky(new Vector3(0.25f, 0.25f, 1.20f)), Is.False,
                "Tether 는 그대로다. 가장 긴 변으로만 판정하면 여기가 걸려 결속 동사가 이유 없이 무거워진다.");
        }

        [Test]
        public void CarryingABulkyPartMakesAdjacentZoneRoundTripExceedTheControlHold()
        {
            // 확정 치수의 구역 간 이동 3.5초는 MoveSpeed 4 기준이므로 거리는 14m 다.
            // 물건을 들고 그 거리를 왕복하면 hold 8초를 넘어야 한다 — 넘는 순간
            // "가서 물건을 가져오기" 가 솔로로 불가능해진다.
            const float zoneTransitDistance = 3.5f * LastShiftPlayerController.MoveSpeed;
            var carriedRoundTrip = 2f * zoneTransitDistance / LastShiftPlayerController.CarrySpeed;

            Assert.That(carriedRoundTrip, Is.GreaterThan(LastShiftRecoveryTuning.QuickBypassLifetimeSeconds / 7.5f),
                "sanity: 왕복이 0 에 가까우면 아래 비교가 의미를 잃는다.");
            Assert.That(carriedRoundTrip, Is.GreaterThan(8f),
                "물건을 들고 인접 구역을 왕복하면 hold 8초를 넘어야 한다.");
            Assert.That(2f * zoneTransitDistance / LastShiftPlayerController.MoveSpeed, Is.LessThan(8f),
                "빈손 왕복은 hold 안에 들어와야 한다. 아니면 느려진 것이 부품 때문이 아니라 거리 때문이 된다.");
        }

        private static bool IsBulky(Vector3 scale)
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.transform.localScale = scale;
            itemObject.AddComponent<Rigidbody>();
            var item = itemObject.AddComponent<LastShiftGrabbable>();
            item.Configure(LastShiftItemRole.PatchPlate, true);
            var bulky = LastShiftPlayerController.IsBulky(item);
            Object.DestroyImmediate(itemObject);
            return bulky;
        }
    }
}
