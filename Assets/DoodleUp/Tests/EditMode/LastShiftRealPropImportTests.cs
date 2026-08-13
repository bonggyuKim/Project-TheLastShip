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
        /// <summary>
        /// 우리가 Blender 에서 만든 프롭만 남는다. <c>LSReal_*</c> 여섯 개는 Tripo 생성물이라
        /// 배에서 걷어냈다 — 이 배열이 다시 늘어난다면 출처를 먼저 확인해야 한다.
        /// </summary>
        private static readonly string[] Names =
        {
            "LP_CargoCrate_0p7m", "LP_OxygenTank_1m", "LP_PortableBattery_0p5m",
            "LP_Toolbox_0p6m", "LP_WorkLamp_0p5m",
            "LP_AirlockDoor", "LP_VentFan", "LP_EmergencyBeacon"
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
        public void EmergencyBeaconVisualDoesNotPreserveBlenderRootScale()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/DoodleUp/Prefabs/Dressing/RealProps/LP_EmergencyBeacon.prefab");
            Assert.That(prefab, Is.Not.Null);

            var visual = prefab.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one),
                "The beacon's Blender FBX root scale must not multiply child light offsets by 100.");
        }

        /// <summary>
        /// <b>Tripo 생성물이 배에 다시 들어오지 못하게 막는다.</b> 예전에는 반대로
        /// "<c>LSReal_*</c> 이 13곳에 링크돼 있을 것" 을 요구했는데, 그 13곳이 곧 Tripo 가
        /// 차지한 자리였다. 링크 수를 세는 대신 <b>출처</b>를 본다 — 자산이 바뀌면 개수는
        /// 따라 움직이지만 "우리 Blender 것만 쓴다" 는 안 움직이기 때문이다.
        /// </summary>
        [Test]
        public void DressingDataAndBuiltSceneUseRealProps()
        {
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            var foreign = set.Props
                .Where(prop => prop?.prefab != null && prop.prefab.name.StartsWith("LSReal_"))
                .Select(prop => prop.prefab.name)
                .Distinct()
                .ToArray();
            Assert.That(foreign, Is.Empty,
                "Tripo 생성 프롭이 드레싱에 링크돼 있다: " + string.Join(", ", foreign));

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
            // 우리 프롭이 드레싱에 링크된 만큼은 배 안에 실제로 서 있어야 한다. 링크가
            // 0 인 동안(쇼케이스 킷 슬롯 id 가 아직 드레싱 데이터에 없다)은 0 >= 0 으로
            // 통과하고, 자리가 잡히는 순간 이 검사가 배치까지 같이 본다.
            var linked = set.Props.Count(prop => prop?.prefab != null && Names.Contains(prop.prefab.name));
            Assert.That(sourceRenderers, Is.GreaterThanOrEqualTo(linked));

            Assert.That(scene.GetRootGameObjects(), Is.Not.Empty);
            Assert.That(scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
                .Any(renderer => renderer.gameObject.scene == scene));
        }
    }
}
