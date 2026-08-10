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
            // <b>에셋 안에서는 인스턴스 루트를 못 묻는다</b>(2026-08-11). 예전에는
            // GetPrefabAssetPathOfNearestInstanceRoot 로 셌는데 그 API 는 씬에 놓인 인스턴스를
            // 전제한다 — 여기서는 씬이 아니라 프리팹 <b>에셋</b>을 열고 그 안에 중첩된
            // 프리팹을 묻고 있어서 항상 빈 문자열이 돌아왔고, 배에 프롭이 91 곳 들어 있어도
            // 0 으로 셌다. 원본 에셋을 직접 물어 경로를 본다.
            var sourceRenderers = shipPrefab.GetComponentsInChildren<MeshRenderer>(true)
                .Count(renderer => AssetDatabase
                    .GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(renderer.gameObject)
                                  ?? renderer.gameObject)
                    .Contains("/RealProps/"));
            Assert.That(sourceRenderers, Is.GreaterThanOrEqualTo(linked.Length));

            Assert.That(scene.GetRootGameObjects(), Is.Not.Empty);
            Assert.That(scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
                .Any(renderer => renderer.gameObject.scene == scene));
        }
    }
}
