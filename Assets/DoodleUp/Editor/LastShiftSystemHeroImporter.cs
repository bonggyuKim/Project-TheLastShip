using System;
using System.Collections.Generic;
using System.IO;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>기능실의 영웅 설비 FBX를 기존 드레싱 프리팹 슬롯에 배선한다.</summary>
    public static class LastShiftSystemHeroImporter
    {
        public const string ModelFolder = "Assets/DoodleUp/Art/Props/LastShiftSystemHeroes";
        public const string PrefabFolder = "Assets/DoodleUp/Prefabs/Dressing";
        public const string BusPanelPrefabPath = PrefabFolder + "/LSDress_BusPanel.prefab";

        private readonly struct HeroSpec
        {
            public readonly string ModelName;
            public readonly string PrefabName;

            public HeroSpec(string modelName, string prefabName)
            {
                ModelName = modelName;
                PrefabName = prefabName;
            }
        }

        private static readonly IReadOnlyList<HeroSpec> Heroes = new[]
        {
            new HeroSpec("LPK_Power_BusPanel", "LSDress_BusPanel"),
            new HeroSpec("LPK_Cooling_HeatExchanger", "LSDress_HeatExchangerCoil"),
            new HeroSpec("LPK_LifeSupport_ScrubberHero", "LSDress_ScrubberStack")
        };

        [MenuItem("Last Shift/SP-02A/Import System Hero Prefabs")]
        public static void ImportAndPlace()
        {
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var hero in Heroes)
                BuildPrefab(hero);

            LinkDressingSlots();
            LastShiftSceneBuilder.ForgetDressingSet();
            LastShiftNetworkSceneBuilder.RebuildSandboxForAutomation();
            AssetDatabase.SaveAssets();
            Debug.Log($"[LAST_SHIFT_SYSTEM_HEROES] prefabs={Heroes.Count} scene={LastShiftNetworkSceneBuilder.ScenePath} result=PASS");
        }

        private static void LinkDressingSlots()
        {
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            if (set == null)
                throw new InvalidOperationException($"Dressing set missing: {LastShiftDressingSet.AssetPath}");

            var links = new Dictionary<string, (string path, float yaw)>
            {
                ["HeatExchangerCoil"] = ($"{PrefabFolder}/LSDress_HeatExchangerCoil.prefab", 180f),
                ["ScrubberStack"] = ($"{PrefabFolder}/LSDress_ScrubberStack.prefab", -90f)
            };
            var serialized = new SerializedObject(set);
            var props = serialized.FindProperty("props");
            var linked = 0;
            for (var index = 0; index < props.arraySize; index++)
            {
                var prop = props.GetArrayElementAtIndex(index);
                if (!links.TryGetValue(prop.FindPropertyRelative("id").stringValue, out var link)) continue;
                prop.FindPropertyRelative("prefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(link.path);
                prop.FindPropertyRelative("eulerAngles").vector3Value = new Vector3(0f, link.yaw, 0f);
                linked++;
            }

            if (linked != links.Count)
                throw new InvalidOperationException($"System hero dressing slots linked {linked}/{links.Count}");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
        }

        private static void BuildPrefab(HeroSpec hero)
        {
            var modelPath = $"{ModelFolder}/{hero.ModelName}.fbx";
            AssetDatabase.ImportAsset(modelPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                throw new InvalidOperationException($"System hero model import failed: {modelPath}");

            var root = new GameObject(hero.PrefabName);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                AlignVisualToBottom(visual);
                AddBoundsCollider(root, visual);

                var prefabPath = $"{PrefabFolder}/{hero.PrefabName}.prefab";
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    throw new InvalidOperationException($"System hero prefab save failed: {prefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AlignVisualToBottom(GameObject visual)
        {
            if (!TryGetBounds(visual, out var bounds)) return;
            visual.transform.position -= Vector3.up * bounds.min.y;
        }

        private static void AddBoundsCollider(GameObject root, GameObject visual)
        {
            if (!TryGetBounds(visual, out var bounds)) return;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = root.transform.InverseTransformVector(bounds.size);
        }

        private static bool TryGetBounds(GameObject target, out Bounds bounds)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return true;
        }
    }
}
