using DoodleUp.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftFloorGroundingTests
    {
        private const float Tolerance = 0.002f;

        [TestCase("HelmSeat_Port")]
        [TestCase("BusCabinet")]
        [TestCase("HeatExchangerCoil")]
        [TestCase("ScrubberStack")]
        public void FloorStandingHeroTouchesTheMeasuredDeck(string objectName)
        {
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>(LastShiftSceneBuilder.ShipPrefabPath);
            Assert.That(ship, Is.Not.Null);
            var target = Find(ship.transform, objectName);
            Assert.That(target, Is.Not.Null, objectName);

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);

            Assert.That(bounds.min.y, Is.EqualTo(LastShiftModularKitImporter.DeckSurfaceY()).Within(Tolerance),
                $"{objectName} 밑면이 모듈 갑판 보행면에 닿아야 한다.");
        }

        private static Transform Find(Transform root, string objectName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child;
            return null;
        }
    }
}
