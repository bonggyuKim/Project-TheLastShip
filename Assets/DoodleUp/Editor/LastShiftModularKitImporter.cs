using System;
using System.Collections.Generic;
using System.IO;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>카탈로그의 bottom-center/meter 규약만 사용해 Last Shift 모듈 키트를 프리팹으로 만든다.</summary>
    public static class LastShiftModularKitImporter
    {
        private const string ModelFolder = "Assets/Art/LastShift/ModularKit";
        private const string PrefabFolder = "Assets/DoodleUp/Prefabs/LastShiftModularKit";
        private const string ControllerFolder = PrefabFolder + "/Animators";
        private const string MapPath = "Assets/DoodleUp/Data/LastShiftModularMap.json";
        private static readonly string[] Names =
        {
            "LPK_Wall_Straight_2m", "LPK_Wall_Straight_4m", "LPK_Wall_Window_4m", "LPK_Wall_Curve_45", "LPK_Corner_Outer_90", "LPK_Corner_Inner_90",
            "LPK_Floor_Square_2m", "LPK_Floor_Curve_45", "LPK_Ceiling_Straight_4m", "LPK_Support_Pillar", "LPK_Door_Airlock_2m", "LPK_Connector_Neck_2m",
            "LPK_CentralLift_4m", "LPK_Cockpit_ControlConsole", "LPK_LifeSupport_Scrubber", "LPK_Power_Switchgear", "LPK_Cooling_Exchanger", "LPK_Quarters_Bunk"
            ,"LPK_Hull_Exterior_Curve45", "LPK_Hull_Exterior_Curve90", "LPK_Hull_Exterior_Panel_4m", "LPK_Hull_WindowBay_4m", "LPK_Cockpit_ViewWindow_4m", "LPK_Ceiling_Curve45", "LPK_Floor_Transition_2m", "LPK_Airlock_Exterior_4m", "LPK_DeckHatch_2m", "LPK_OxygenLeakPipe_2m", "LPK_RepairConsole_1m", "LPK_DamagedPipe_2m", "LPK_SalvagePad_4m", "LPK_TetherRack_2m"
        };

        [MenuItem("Last Shift/SP-02A/Import Modular Kit and Assemble")]
        public static void ImportAndAssemble()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(ControllerFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var name in Names) prefabs[name] = CreatePrefab(name);
            Assemble(LastShiftSceneBuilder.RebuildShipPrefab(), prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LAST_SHIFT_MODULAR_KIT] prefabs={prefabs.Count} actions=3 assembly=central-plaza+4-rooms result=PASS");
        }

        private static GameObject CreatePrefab(string name)
        {
            var modelPath = $"{ModelFolder}/{name}.fbx";
            ConfigureModelAxisConversion(modelPath);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidOperationException($"Modular kit model missing: {modelPath}");
            var root = new GameObject(name);
            // Visual은 씬 배치가 참조하는 중립 래퍼다. FBX 모델 루트는 Importer가 관리하므로
            // 그 루트를 Visual로 이름만 바꾸면 축 회전을 래퍼에도 남긴 채 저장하게 된다.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            // Blender → Unity 축 처리는 FBX ModelImporter가 유일한 정본이다. 모델 루트가
            // 내보내기 과정에서 가진 회전은 모델 자식에만 남긴다. Visual은 항상 원점/무회전이다.
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            modelInstance.name = "Model";
            modelInstance.transform.SetParent(visual.transform, false);
            if (TryCreateAnimator(name, root, out var controller))
                root.AddComponent<Animator>().runtimeAnimatorController = controller;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null) throw new InvalidOperationException($"Could not create prefab for {name}");
            return prefab;
        }

        private static void ConfigureModelAxisConversion(string modelPath)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException($"Model importer missing: {modelPath}");

            // Unity의 ModelImporter가 좌표계 변환과 meter 단위를 한 번만 적용한다.
            // FBX 루트에 기록된 Blender→Unity 축 보정은 Importer가 유지한다. 이를 메시로
            // bake하면 이 키트의 기존 루트 규약(XZ 바닥)이 XY 평면으로 다시 눕는다.
            importer.bakeAxisConversion = false;
            importer.useFileUnits = true;
            importer.globalScale = 1f;
            AssetDatabase.WriteImportSettingsIfDirty(modelPath);
        }

        private static bool TryCreateAnimator(string name, GameObject root, out AnimatorController controller)
        {
            controller = null;
            var spec = name switch
            {
                "LPK_Door_Airlock_2m" => (pivot: "DoorLeafPivot", clip: "LP_Door_OpenClose", property: "m_LocalRotation.y", end: 0.7071f, loop: false),
                "LPK_CentralLift_4m" => (pivot: "LiftPlatformPivot", clip: "LP_CentralLift_UpDown", property: "m_LocalPosition.y", end: 0.8f, loop: true),
                "LPK_LifeSupport_Scrubber" => (pivot: "ScrubberFanPivot", clip: "LP_LifeSupportFan_Spin", property: "m_LocalRotation.y", end: 1f, loop: true),
                _ => default
            };
            if (string.IsNullOrEmpty(spec.pivot)) return false;
            var pivot = Find(root.transform, spec.pivot);
            if (pivot == null) throw new InvalidOperationException($"{name} has no required pivot {spec.pivot}");
            var clipPath = $"{ControllerFolder}/{spec.clip}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) ?? new AnimationClip { name = spec.clip };
            var binding = EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(pivot, root.transform), typeof(Transform), spec.property);
            clip.ClearCurves();
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Linear(0f, 0f, 1f, spec.end));
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = spec.loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (AssetDatabase.GetAssetPath(clip) == string.Empty) AssetDatabase.CreateAsset(clip, clipPath);
            var controllerPath = $"{ControllerFolder}/{name}.controller";
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            foreach (var existingState in stateMachine.states) stateMachine.RemoveState(existingState.state);
            var animationState = stateMachine.AddState(spec.clip); animationState.motion = clip; stateMachine.defaultState = animationState;
            EditorUtility.SetDirty(controller);
            return true;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            return null;
        }

        private static void Assemble(GameObject shipPrefab, IReadOnlyDictionary<string, GameObject> p)
        {
            var contents = PrefabUtility.LoadPrefabContents(LastShiftSceneBuilder.ShipPrefabPath);
            AppendAssembly(contents, p);
            PrefabUtility.SaveAsPrefabAsset(contents, LastShiftSceneBuilder.ShipPrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        /// <summary>프리팹이 이미 생성된 뒤에는 모든 표준 씬 재빌드가 같은 모듈 조립을 보존한다.</summary>
        internal static void AppendAssemblyIfAvailable(GameObject ship)
        {
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var name in Names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
                if (prefab == null) return;
                prefabs[name] = prefab;
            }
            AppendAssembly(ship, prefabs);
        }

        internal static bool TryGetCockpitCameraPose(out Vector3 spawn, out Vector3 lookAt)
        {
            spawn = Vector3.zero;
            lookAt = Vector3.forward;
            if (!File.Exists(MapPath)) return false;
            var map = JsonUtility.FromJson<ModularMap>(File.ReadAllText(MapPath));
            if (map?.cockpitCamera?.spawn == null || map.cockpitCamera.lookAt == null) return false;
            spawn = Vector(map.cockpitCamera.spawn);
            lookAt = Vector(map.cockpitCamera.lookAt);
            return true;
        }

        private static void AppendAssembly(GameObject ship, IReadOnlyDictionary<string, GameObject> p)
        {
            // Legacy hierarchy is retained until its runtime door references are
            // replaced.  Do not use Renderer.enabled as a visual cleanup path.
            var old = ship.transform.Find("ModularKitAssembly");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var map = LoadMap(p);
            RemoveExcludedObjects(ship, map.excluded);
            var root = new GameObject("ModularKitAssembly").transform; root.SetParent(ship.transform, false);
            BuildFromMap(map, p, root);
            Debug.Log($"[LAST_SHIFT_MODULAR_MAP] schema={map.schema} spaces={map.spaces.Length} rules={map.placementRules.Length} result=PASS");
        }

        private static ModularMap LoadMap(IReadOnlyDictionary<string, GameObject> prefabs)
        {
            if (!File.Exists(MapPath)) throw new FileNotFoundException("Canonical modular map missing", MapPath);
            var map = JsonUtility.FromJson<ModularMap>(File.ReadAllText(MapPath));
            if (map == null || map.schema != "lastshift-modular-map/v1" || map.plaza == null || map.spaces == null || map.placementRules == null)
                throw new InvalidOperationException($"Invalid canonical modular map: {MapPath}");
            foreach (var rule in map.placementRules)
                if (string.IsNullOrEmpty(rule.assetId) || !prefabs.ContainsKey(rule.assetId))
                    throw new InvalidOperationException($"Map rule {rule.id} references missing prefab {rule.assetId}");
            return map;
        }

        private static void BuildFromMap(ModularMap map, IReadOnlyDictionary<string, GameObject> p, Transform root)
        {
            var spaces = new Dictionary<string, MapSpace> { ["plaza"] = new MapSpace { id = "plaza", bounds = map.plaza.bounds, ceiling = map.plaza.ceiling } };
            foreach (var space in map.spaces) spaces.Add(space.id, space);
            foreach (var rule in map.placementRules)
            {
                if (rule.operation == "tileBounds")
                    foreach (var target in rule.target) TileBounds(p[rule.assetId], root, rule.id, spaces[target], rule.tile, rule.positionY);
                else if (rule.operation == "wallBoundsWithDoorGap")
                    InteriorBoundsWithDoorGap(p[rule.assetId], root, rule, map, spaces);
                else if (rule.operation == "spanBounds")
                    foreach (var target in rule.target) SpanBounds(p[rule.assetId], root, rule.id, spaces[target], rule.positionY);
                else if (rule.operation == "exteriorBoundsWithDoorGap") ExteriorBoundsWithDoorGap(p[rule.assetId], root, rule, map, spaces);
                else Place(p[rule.assetId], root, rule.id, Vector(rule.position), rule.rotationY, VectorOrOne(rule.scale));
            }
            foreach (var space in map.spaces)
            {
                Place(p["LPK_Door_Airlock_2m"], root, space.id + "Door", Vector(space.door.position), space.door.rotationY);
                Place(p[space.feature], root, space.id + "Feature", BoundsCenter(space.bounds));
            }
            // Exterior panels own the footprint boundary.  This is a final
            // manifest-derived guard for a wall segment that is emitted on an
            // outer edge because of a non-rectangular union split.
            RemoveInteriorWallsCoveredByShell(root);
        }

        private static void TileBounds(GameObject prefab, Transform root, string name, MapSpace space, float[] tile, float y)
        {
            for (var x = space.bounds[0] + tile[0] * 0.5f; x < space.bounds[1]; x += tile[0])
            for (var z = space.bounds[2] + tile[1] * 0.5f; z < space.bounds[3]; z += tile[1])
                Place(prefab, root, name, new Vector3(x, y, z));
        }

        private static void InteriorBoundsWithDoorGap(GameObject prefab, Transform root, MapRule rule, ModularMap map, IReadOnlyDictionary<string, MapSpace> spaces)
        {
            var targets = new List<MapSpace>(); foreach (var id in rule.target) targets.Add(spaces[id]);
            var xs = new List<float>(); var zs = new List<float>();
            foreach (var space in targets) { xs.Add(space.bounds[0]); xs.Add(space.bounds[1]); zs.Add(space.bounds[2]); zs.Add(space.bounds[3]); }
            xs = UniqueSorted(xs); zs = UniqueSorted(zs);
            for (var zi = 0; zi < zs.Count; zi++)
            for (var xi = 0; xi < xs.Count - 1; xi++)
            {
                var min = xs[xi]; var max = xs[xi + 1]; if (max - min < 0.01f) continue;
                if (Contains(targets, (min + max) * 0.5f, zs[zi] - 0.01f) && Contains(targets, (min + max) * 0.5f, zs[zi] + 0.01f))
                    PlaceExteriorSegment(prefab, root, rule, map.spaces, min, max, zs[zi], 0f, true);
            }
            for (var xi = 0; xi < xs.Count; xi++)
            for (var zi = 0; zi < zs.Count - 1; zi++)
            {
                var min = zs[zi]; var max = zs[zi + 1]; if (max - min < 0.01f) continue;
                if (Contains(targets, xs[xi] - 0.01f, (min + max) * 0.5f) && Contains(targets, xs[xi] + 0.01f, (min + max) * 0.5f))
                    PlaceExteriorSegment(prefab, root, rule, map.spaces, min, max, xs[xi], 90f, false);
            }
        }

        private static void SpanBounds(GameObject prefab, Transform root, string name, MapSpace space, float y)
        {
            var b = space.bounds;
            Place(prefab, root, name, new Vector3((b[0] + b[1]) * 0.5f, y, (b[2] + b[3]) * 0.5f), 0f,
                new Vector3((b[1] - b[0]) / 4f, 1f, (b[3] - b[2]) / 4f));
        }

        private static void ExteriorBoundsWithDoorGap(GameObject prefab, Transform root, MapRule rule, ModularMap map, IReadOnlyDictionary<string, MapSpace> spaces)
        {
            var targets = new List<MapSpace>();
            foreach (var id in rule.target) targets.Add(spaces[id]);
            var xs = new List<float>(); var zs = new List<float>();
            foreach (var space in targets)
            {
                xs.Add(space.bounds[0]); xs.Add(space.bounds[1]); zs.Add(space.bounds[2]); zs.Add(space.bounds[3]);
            }
            xs = UniqueSorted(xs); zs = UniqueSorted(zs);
            for (var zi = 0; zi < zs.Count; zi++)
            for (var xi = 0; xi < xs.Count - 1; xi++)
            {
                var min = xs[xi]; var max = xs[xi + 1]; if (max - min < 0.01f) continue;
                var insideBelow = Contains(targets, (min + max) * 0.5f, zs[zi] - 0.01f);
                var insideAbove = Contains(targets, (min + max) * 0.5f, zs[zi] + 0.01f);
                if (insideBelow == insideAbove) continue;
                var z = zs[zi] + (insideAbove ? -rule.shellClearance : rule.shellClearance);
                PlaceExteriorSegment(prefab, root, rule, map.spaces, min, max, z, 0f, true);
            }
            for (var xi = 0; xi < xs.Count; xi++)
            for (var zi = 0; zi < zs.Count - 1; zi++)
            {
                var min = zs[zi]; var max = zs[zi + 1]; if (max - min < 0.01f) continue;
                var insideLeft = Contains(targets, xs[xi] - 0.01f, (min + max) * 0.5f);
                var insideRight = Contains(targets, xs[xi] + 0.01f, (min + max) * 0.5f);
                if (insideLeft == insideRight) continue;
                var x = xs[xi] + (insideRight ? -rule.shellClearance : rule.shellClearance);
                PlaceExteriorSegment(prefab, root, rule, map.spaces, min, max, x, 90f, false);
            }
        }

        private static bool Contains(List<MapSpace> spaces, float x, float z)
        {
            foreach (var space in spaces)
                if (x > space.bounds[0] && x < space.bounds[1] && z > space.bounds[2] && z < space.bounds[3]) return true;
            return false;
        }

        private static List<float> UniqueSorted(List<float> values)
        {
            values.Sort();
            var unique = new List<float>();
            foreach (var value in values)
                if (unique.Count == 0 || Mathf.Abs(unique[unique.Count - 1] - value) > 0.001f) unique.Add(value);
            return unique;
        }

        private static void PlaceExteriorSegment(GameObject prefab, Transform root, MapRule rule, MapSpace[] spaces, float min, float max, float fixedAxis, float rotationY, bool alongX)
        {
            MapDoor door = null;
            foreach (var space in spaces)
            {
                if (space.door == null) continue;
                var onEdge = alongX ? Mathf.Abs(space.door.position[2] - fixedAxis) <= rule.shellClearance + 0.01f : Mathf.Abs(space.door.position[0] - fixedAxis) <= rule.shellClearance + 0.01f;
                var value = alongX ? space.door.position[0] : space.door.position[2];
                if (onEdge && value > min && value < max) { door = space.door; break; }
            }
            var gapCenter = door == null ? 0f : (alongX ? door.position[0] : door.position[2]);
            PlaceExteriorPiece(prefab, root, rule.id, min, door == null ? max : gapCenter - rule.gapWidth * 0.5f, fixedAxis, rotationY, alongX);
            if (door != null) PlaceExteriorPiece(prefab, root, rule.id, gapCenter + rule.gapWidth * 0.5f, max, fixedAxis, rotationY, alongX);
        }

        private static void PlaceExteriorPiece(GameObject prefab, Transform root, string name, float min, float max, float fixedAxis, float rotationY, bool alongX)
        {
            if (max - min < 0.01f) return;
            var position = alongX ? new Vector3((min + max) * 0.5f, 0f, fixedAxis) : new Vector3(fixedAxis, 0f, (min + max) * 0.5f);
            Place(prefab, root, name, position, rotationY, new Vector3((max - min) / 4f, 1f, 1f));
        }

        private static void RemoveExcludedObjects(GameObject ship, string[] excluded)
        {
            if (excluded == null) return;
            foreach (var transform in ship.GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(excluded, transform.name) >= 0) UnityEngine.Object.DestroyImmediate(transform.gameObject);
        }

        private static void RemoveInteriorWallsCoveredByShell(Transform root)
        {
            var walls = new List<Transform>();
            var shells = new List<Bounds>();
            foreach (Transform child in root)
            {
                if (child.name == "walls") walls.Add(child);
                else if (child.name == "outerShell")
                {
                    foreach (var renderer in child.GetComponentsInChildren<Renderer>(true)) shells.Add(renderer.bounds);
                }
            }

            var removed = 0;
            foreach (var wall in walls)
            {
                var renderers = wall.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if (!shells.Exists(shell => SharesBoundaryFace(bounds, shell))) continue;
                UnityEngine.Object.DestroyImmediate(wall.gameObject);
                removed++;
            }
            if (removed > 0) Debug.Log($"[LAST_SHIFT_MODULAR_MAP] boundary wall dedupe removed={removed}");
        }

        private static bool SharesBoundaryFace(Bounds wall, Bounds shell)
        {
            var xFace = wall.size.x < 0.75f && shell.size.x < 0.75f && Mathf.Abs(wall.center.x - shell.center.x) < 0.6f && Overlaps(wall.min.z, wall.max.z, shell.min.z, shell.max.z);
            var zFace = wall.size.z < 0.75f && shell.size.z < 0.75f && Mathf.Abs(wall.center.z - shell.center.z) < 0.6f && Overlaps(wall.min.x, wall.max.x, shell.min.x, shell.max.x);
            return (xFace || zFace) && Overlaps(wall.min.y, wall.max.y, shell.min.y, shell.max.y);
        }

        private static bool Overlaps(float aMin, float aMax, float bMin, float bMax) => Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin) > 0.01f;

        private static Vector3 BoundsCenter(float[] bounds) => new((bounds[0] + bounds[1]) * 0.5f, 0f, (bounds[2] + bounds[3]) * 0.5f);
        private static Vector3 Vector(float[] values) => new(values[0], values[1], values[2]);
        private static Vector3 VectorOrOne(float[] values) => values == null || values.Length == 0 ? Vector3.one : Vector(values);

        private static void Place(GameObject prefab, Transform parent, string name, Vector3 position, float rotationY = 0f, Vector3? scale = null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            instance.transform.localScale = scale ?? Vector3.one;
        }

        [Serializable] private sealed class ModularMap { public string schema; public MapCamera cockpitCamera; public MapPlaza plaza; public MapSpace[] spaces; public MapRule[] placementRules; public string[] excluded; }
        [Serializable] private sealed class MapCamera { public float[] spawn; public float[] lookAt; }
        [Serializable] private sealed class MapPlaza { public float[] bounds; public float ceiling; }
        [Serializable] private sealed class MapSpace { public string id; public float[] bounds; public float ceiling; public MapDoor door; public string feature; }
        [Serializable] private sealed class MapDoor { public float[] position; public float rotationY; }
        [Serializable] private sealed class MapRule { public string id; public string assetId; public string[] target; public string operation; public float[] tile; public float positionY; public float gapWidth; public float[] position; public float rotationY; public float[] scale; public float radius; public int count; public float rotationStep; public float shellClearance; }
    }
}
