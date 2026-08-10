using System.Linq;
using DoodleUp.Editor;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftRealPropImportTests
    {
        private static readonly string[] Names =
        {
            "LSReal_ControlPanel", "LSReal_CargoCrate", "LSReal_OxygenTank",
            "LSReal_PortableBattery", "LSReal_Toolbox", "LSReal_WorkLamp"
        };

        [Test]
        public void CuratedModelsHaveReusablePrefabsAndRenderableMeshes()
        {
            foreach (var name in Names)
            {
                var path = $"Assets/DoodleUp/Prefabs/Dressing/RealProps/{name}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true), Is.Not.Empty, name);
                Assert.That(prefab.transform.position, Is.EqualTo(Vector3.zero), $"{name} root must stay at floor origin");
            }
        }

        [Test]
        public void DressingDataAndBuiltSceneUseRealProps()
        {
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            var linked = set.Props.Where(prop => prop?.prefab != null && prop.prefab.name.StartsWith("LSReal_")).ToArray();
            Assert.That(linked.Length, Is.EqualTo(13));

            var scene = EditorSceneManager.OpenScene(LastShiftNetworkSceneBuilder.ScenePath);
            var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DoodleUp/Prefabs/LastShiftShipGraybox.prefab");
            Assert.That(shipPrefab, Is.Not.Null);
            var sourceRenderers = shipPrefab.GetComponentsInChildren<MeshRenderer>(true)
                .Count(renderer => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject)
                                       .Contains("/RealProps/"));
            Assert.That(sourceRenderers, Is.GreaterThanOrEqualTo(linked.Length));

            Assert.That(scene.GetRootGameObjects(), Is.Not.Empty);
            Assert.That(scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
                .Any(renderer => renderer.gameObject.scene == scene));
        }
    }
}
