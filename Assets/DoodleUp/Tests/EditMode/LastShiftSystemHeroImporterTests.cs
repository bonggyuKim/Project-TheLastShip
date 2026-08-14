using System.Linq;
using DoodleUp.Editor;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftSystemHeroImporterTests
    {
        [TestCase("LSDress_BusPanel", "LPK_Power_BusPanel")]
        [TestCase("LSDress_HeatExchangerCoil", "LPK_Cooling_HeatExchanger")]
        [TestCase("LSDress_ScrubberStack", "LPK_LifeSupport_ScrubberHero")]
        public void HeroPrefab_UsesNewFbxAndHasClearanceCollider(string prefabName, string modelName)
        {
            var prefabPath = $"{LastShiftSystemHeroImporter.PrefabFolder}/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            Assert.That(prefab, Is.Not.Null, prefabPath);
            Assert.That(prefab.GetComponent<BoxCollider>(), Is.Not.Null, "root clearance collider");
            var visual = prefab.transform.Find("Visual");
            Assert.That(visual, Is.Not.Null, "FBX visual root");
            var source = PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject);
            Assert.That(source, Is.Not.Null);
            Assert.That(source.name, Is.EqualTo(modelName));
            Assert.That(visual.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        }

        [TestCase("HeatExchangerCoil", 180f)]
        [TestCase("ScrubberStack", -90f)]
        public void WallMountedHero_FacesRoom(string id, float yaw)
        {
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            var prop = set.Props.Single(candidate => candidate.id == id);

            Assert.That(prop.prefab, Is.Not.Null);
            Assert.That(prop.eulerAngles.y, Is.EqualTo(yaw));
        }
    }
}
