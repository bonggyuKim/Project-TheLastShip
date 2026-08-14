using DoodleUp.Editor;
using NUnit.Framework;
using UnityEditor;
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

        [TestCase("Cockpit")]
        [TestCase("Power")]
        [TestCase("Cooling")]
        [TestCase("LifeSupport")]
        public void PrimaryRoomFixturesPreserveShadowInformation(string room)
        {
            var path = $"Assets/DoodleUp/Prefabs/Dressing/LSDress_Lamp_{room}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);

            var light = prefab.GetComponentInChildren<Light>(true);
            Assert.That(light, Is.Not.Null, path);
            Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft), path);
            Assert.That(light.shadowStrength, Is.InRange(0.75f, 0.9f), path);
            Assert.That(light.bounceIntensity, Is.GreaterThanOrEqualTo(1.1f), path);
        }

        [Test]
        public void SharedFixtureMaterialStaysInReadableMidtoneBand()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/DoodleUp/Materials/LS_Fixture.mat");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.color.grayscale, Is.InRange(0.42f, 0.5f));
        }
    }
}
