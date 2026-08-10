using System;
using System.Collections.Generic;
using System.IO;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>Converts the curated D:/Assets export set into bottom-pivot dressing prefabs.</summary>
    public static class LastShiftRealPropImporter
    {
        private const string ModelFolder = "Assets/DoodleUp/Art/Props/LastShiftReal";
        private const string PrefabFolder = "Assets/DoodleUp/Prefabs/Dressing/RealProps";

        private static readonly string[] AssetNames =
        {
            "LSReal_ControlPanel", "LSReal_CargoCrate", "LSReal_OxygenTank",
            "LSReal_PortableBattery", "LSReal_Toolbox", "LSReal_WorkLamp"
        };

        private static readonly Dictionary<string, string> DressingLinks = new()
        {
            ["NavChartTable"] = "LSReal_ControlPanel",
            ["CrateStack_Aft"] = "LSReal_CargoCrate",
            ["CrateStack_Fore"] = "LSReal_CargoCrate",
            ["CrateStack_Mid"] = "LSReal_CargoCrate",
            ["O2TankBank_Fore"] = "LSReal_OxygenTank",
            ["O2TankBank_Aft"] = "LSReal_OxygenTank",
            ["PartsPallet"] = "LSReal_PortableBattery",
            ["ToolBoard_Port"] = "LSReal_Toolbox"
        };

        [MenuItem("Last Shift/SP-02A/Import Real Prop Prefabs")]
        public static void ImportAndPlace()
        {
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var prefabs = BuildPrefabs();
            LinkDressing(prefabs);
            LastShiftSceneBuilder.ForgetDressingSet();
            LastShiftNetworkSceneBuilder.RebuildSandboxForAutomation();
            AssetDatabase.SaveAssets();
            Debug.Log($"[LAST_SHIFT_REAL_PROPS] prefabs={prefabs.Count} linkedSlots={CountLinkedSlots()} scene={LastShiftNetworkSceneBuilder.ScenePath} result=PASS");
        }

        private static Dictionary<string, GameObject> BuildPrefabs()
        {
            var result = new Dictionary<string, GameObject>();
            foreach (var name in AssetNames)
            {
                var modelPath = $"{ModelFolder}/{name}.fbx";
                AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null) throw new InvalidOperationException($"Real prop model import failed: {modelPath}");

                var root = new GameObject(name);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                var prefabPath = $"{PrefabFolder}/{name}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
                if (prefab == null) throw new InvalidOperationException($"Real prop prefab save failed: {prefabPath}");
                result.Add(name, prefab);
            }
            return result;
        }

        private static void LinkDressing(IReadOnlyDictionary<string, GameObject> prefabs)
        {
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            if (set == null) throw new InvalidOperationException($"Dressing set missing: {LastShiftDressingSet.AssetPath}");

            var serialized = new SerializedObject(set);
            var props = serialized.FindProperty("props");
            var linked = 0;
            for (var i = 0; i < props.arraySize; i++)
            {
                var prop = props.GetArrayElementAtIndex(i);
                var id = prop.FindPropertyRelative("id").stringValue;
                if (!DressingLinks.TryGetValue(id, out var assetName)) continue;
                prop.FindPropertyRelative("prefab").objectReferenceValue = prefabs[assetName];
                linked++;
            }
            if (linked == 0) throw new InvalidOperationException("No dressing slots matched the real prop mapping.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
        }

        private static int CountLinkedSlots()
        {
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            var count = 0;
            foreach (var prop in set.Props)
                if (prop?.prefab != null && prop.prefab.name.StartsWith("LSReal_", StringComparison.Ordinal)) count++;
            return count;
        }
    }
}
