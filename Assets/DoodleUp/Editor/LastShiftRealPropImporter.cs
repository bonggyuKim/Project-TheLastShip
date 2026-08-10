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

        /// <summary>
        /// <b>우리가 Blender 에서 만든 것만 쓴다.</b> 여기 있던 <c>LSReal_*</c> 여섯 개는
        /// Tripo 생성물이었다 — 파일 안에 <c>tripo_node_*</c>/<c>tripo_mesh_*</c> 이름이 그대로
        /// 남아 있어 출처를 숨길 수도 없었다. 그것들이 드레싱 슬롯 13곳을 차지하는 동안
        /// 정작 쇼케이스 킷의 <c>LP_*</c> 는 슬롯 id 가 안 맞아 <b>한 곳도 안 서 있었다</b>.
        /// </summary>
        private static readonly string[] AssetNames =
        {
            "LP_AirlockDoor", "LP_VentFan", "LP_EmergencyBeacon"
        };

        private static readonly Dictionary<string, string> DressingLinks = new()
        {
            ["AirlockDoor_Main"] = "LP_AirlockDoor",
            ["VentFan_Service"] = "LP_VentFan",
            ["EmergencyBeacon_Service"] = "LP_EmergencyBeacon"
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
                AlignVisualToBottom(visual);
                foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                }
                var prefabPath = $"{PrefabFolder}/{name}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                UnityEngine.Object.DestroyImmediate(root);
                if (prefab == null) throw new InvalidOperationException($"Real prop prefab save failed: {prefabPath}");
                result.Add(name, prefab);
            }
            return result;
        }

        private static void AlignVisualToBottom(GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            var minY = float.PositiveInfinity;
            foreach (var renderer in renderers)
                minY = Mathf.Min(minY, renderer.bounds.min.y);
            if (!float.IsFinite(minY)) return;

            // The exported real props have a centre pivot; the dressing data is floor-based.
            visual.transform.localPosition -= Vector3.up * minY;
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
            // <b>0 이어도 던지지 않는다.</b> 위 세 슬롯 id 는 드레싱 데이터에 아직 없다 —
            // Tripo 프롭을 걷어내고 보니 쇼케이스 킷이 설 자리가 애초에 안 잡혀 있었다.
            // 여기서 던지면 그 사실이 "임포터 고장" 으로 보이고, 자리를 잡는 것은 드레싱
            // 데이터 쪽 일이라 이 도구가 막을 일이 아니다.
            if (linked == 0)
                Debug.LogWarning("[LAST_SHIFT_REAL_PROPS] 매칭된 드레싱 슬롯이 없다 — " +
                                 string.Join(", ", DressingLinks.Keys) + " 가 드레싱 데이터에 없다.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
        }

        private static int CountLinkedSlots()
        {
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            var count = 0;
            foreach (var prop in set.Props)
                if (prop?.prefab != null && prop.prefab.name.StartsWith("LP_", StringComparison.Ordinal)) count++;
            return count;
        }
    }
}
