using DoodleUp.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftLightingToneTests
    {
        [SetUp]
        public void SetUp() => RemoveDirectionalLight();

        [TearDown]
        public void TearDown() => RemoveDirectionalLight();

        private static void RemoveDirectionalLight()
        {
            var directional = GameObject.Find("Directional Light");
            if (directional != null)
                Object.DestroyImmediate(directional);
        }

        [Test]
        public void SharedShipLightingIsNeutralAndCrewReadable()
        {
            LastShiftSceneBuilder.CreateLighting();

            var ambient = RenderSettings.ambientLight;
            Assert.That(ambient.grayscale, Is.GreaterThanOrEqualTo(0.19f),
                "평상시 환경광이 낮아지면 동료와 소품 실루엣이 다시 암부에 묻힌다.");
            Assert.That(ambient.r - ambient.b, Is.InRange(0f, 0.025f),
                "공용 환경광은 구역색을 덮지 않는 저채도 웜 그레이여야 한다.");

            var directional = GameObject.Find("Directional Light").GetComponent<Light>();
            Assert.That(directional.intensity, Is.GreaterThanOrEqualTo(0.4f));
            Assert.That(directional.color.r - directional.color.b, Is.LessThanOrEqualTo(0.1f),
                "형태 보조광에 강한 피치 캐스트가 남으면 구역색과 소품색이 왜곡된다.");
        }

        [TestCase("Assets/Scenes/LAST_SHIFT_SOLO.unity")]
        [TestCase("Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity")]
        public void ShippedScenesKeepSharedMidtonePass(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var ambient = RenderSettings.ambientLight;
            Assert.That(RenderSettings.ambientMode,
                Is.EqualTo(UnityEngine.Rendering.AmbientMode.Flat));
            Assert.That(ambient.grayscale, Is.GreaterThanOrEqualTo(0.19f), scenePath);
            Assert.That(ambient.r - ambient.b, Is.InRange(0f, 0.025f), scenePath);

            var directionalObject = GameObject.Find("Directional Light");
            Assert.That(directionalObject, Is.Not.Null, scenePath);
            var directional = directionalObject.GetComponent<Light>();
            Assert.That(directional.intensity, Is.GreaterThanOrEqualTo(0.4f), scenePath);
            Assert.That(directional.color.r - directional.color.b,
                Is.LessThanOrEqualTo(0.1f), scenePath);
        }
    }
}
