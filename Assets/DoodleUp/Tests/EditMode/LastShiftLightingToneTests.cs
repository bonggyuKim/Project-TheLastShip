using DoodleUp.Editor;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftLightingToneTests
    {
        [TearDown]
        public void TearDown()
        {
            var directional = GameObject.Find("Directional Light");
            if (directional != null)
                Object.DestroyImmediate(directional);
        }

        [Test]
        public void SharedShipLightingIsWarmAndCrewReadable()
        {
            LastShiftSceneBuilder.CreateLighting();

            var ambient = RenderSettings.ambientLight;
            Assert.That(ambient.grayscale, Is.GreaterThanOrEqualTo(0.19f),
                "평상시 환경광이 낮아지면 동료와 소품 실루엣이 다시 암부에 묻힌다.");
            Assert.That(ambient.r, Is.GreaterThan(ambient.b),
                "함선 전체를 청회색 공포 톤으로 되돌리지 않는다.");

            var directional = GameObject.Find("Directional Light").GetComponent<Light>();
            Assert.That(directional.intensity, Is.GreaterThanOrEqualTo(0.4f));
            Assert.That(directional.color.r, Is.GreaterThan(directional.color.b),
                "형태 보조광은 차가운 달빛보다 친근한 피치색이어야 한다.");
        }
    }
}
