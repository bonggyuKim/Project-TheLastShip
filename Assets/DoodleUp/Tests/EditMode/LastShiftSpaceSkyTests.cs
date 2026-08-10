using DoodleUp.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftSpaceSkyTests
    {
        [Test]
        public void NetworkSceneUsesDedicatedSpaceSky()
        {
            EditorSceneManager.OpenScene(LastShiftNetworkSceneBuilder.ScenePath);

            Assert.That(RenderSettings.skybox, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(RenderSettings.skybox),
                Is.EqualTo(LastShiftSceneBuilder.SpaceSkyMaterialPath));
            Assert.That(RenderSettings.skybox.shader.name,
                Is.EqualTo(LastShiftSceneBuilder.SpaceSkyShaderName));
            Assert.That(RenderSettings.reflectionIntensity, Is.EqualTo(0.35f).Within(0.001f));
        }

        [Test]
        public void SpaceSkyHasVisibleStarsAndControlledPalette()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(LastShiftSceneBuilder.SpaceSkyMaterialPath);

            Assert.That(sky, Is.Not.Null);
            Assert.That(sky.GetFloat("_StarDensity"), Is.InRange(0.97f, 0.9995f));
            Assert.That(sky.GetFloat("_StarIntensity"), Is.GreaterThan(0f));
            Assert.That(sky.GetColor("_ZenithColor").maxColorComponent, Is.LessThan(0.05f));
            Assert.That(sky.GetColor("_StarColor").b, Is.GreaterThan(sky.GetColor("_StarColor").r));
        }
    }
}
