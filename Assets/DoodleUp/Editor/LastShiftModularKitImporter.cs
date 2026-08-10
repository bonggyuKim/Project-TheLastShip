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
        private static readonly string[] Names =
        {
            "LPK_Wall_Straight_2m", "LPK_Wall_Straight_4m", "LPK_Wall_Window_4m", "LPK_Wall_Curve_45", "LPK_Corner_Outer_90", "LPK_Corner_Inner_90",
            "LPK_Floor_Square_2m", "LPK_Floor_Curve_45", "LPK_Ceiling_Straight_4m", "LPK_Support_Pillar", "LPK_Door_Airlock_2m", "LPK_Connector_Neck_2m",
            "LPK_CentralLift_4m", "LPK_Cockpit_ControlConsole", "LPK_LifeSupport_Scrubber", "LPK_Power_Switchgear", "LPK_Cooling_Exchanger", "LPK_Quarters_Bunk"
            ,"LPK_Hull_Exterior_Curve45", "LPK_Hull_Exterior_Curve90", "LPK_Hull_WindowBay_4m", "LPK_Cockpit_ViewWindow_4m", "LPK_Ceiling_Curve45", "LPK_Floor_Transition_2m", "LPK_Airlock_Exterior_4m", "LPK_DeckHatch_2m", "LPK_OxygenLeakPipe_2m", "LPK_RepairConsole_1m", "LPK_DamagedPipe_2m", "LPK_SalvagePad_4m", "LPK_TetherRack_2m"
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
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidOperationException($"Modular kit model missing: {modelPath}");
            var root = new GameObject(name);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            if (TryCreateAnimator(name, root, out var controller))
                root.AddComponent<Animator>().runtimeAnimatorController = controller;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null) throw new InvalidOperationException($"Could not create prefab for {name}");
            return prefab;
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

        private static void AppendAssembly(GameObject ship, IReadOnlyDictionary<string, GameObject> p)
        {
            // 물리 경계와 네트워크 오브젝트는 남기고, 기존 graybox의 시각 렌더러만 끈다.
            // 새 키트 루트는 그 뒤에 세워지므로 이 필터가 새 프리팹을 건드리지 않는다.
            foreach (var renderer in ship.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var old = ship.transform.Find("ModularKitAssembly");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("ModularKitAssembly").transform; root.SetParent(ship.transform, false);
            // 중앙광장: 2m 바닥 3x3, 중앙 리프트, 4개 접속 목.
            for (var x = -2; x <= 2; x += 2)
            for (var z = -2; z <= 2; z += 2) Place(p["LPK_Floor_Square_2m"], root, "PlazaFloor", new Vector3(x, 0.01f, z));
            Place(p["LPK_CentralLift_4m"], root, "CentralLift", Vector3.zero);
            Place(p["LPK_Support_Pillar"], root, "PlazaPillar", new Vector3(-2f, 0f, -2f));
            Place(p["LPK_Support_Pillar"], root, "PlazaPillar", new Vector3(2f, 0f, -2f));
            Place(p["LPK_Support_Pillar"], root, "PlazaPillar", new Vector3(-2f, 0f, 2f));
            Place(p["LPK_Support_Pillar"], root, "PlazaPillar", new Vector3(2f, 0f, 2f));
            AddRoom(p, root, LastShiftZone.Cockpit, "Cockpit", "LPK_Cockpit_ControlConsole", 0f);
            AddRoom(p, root, LastShiftZone.Power, "Power", "LPK_Power_Switchgear", 180f);
            AddRoom(p, root, LastShiftZone.Cooling, "Cooling", "LPK_Cooling_Exchanger", 0f);
            AddRoom(p, root, LastShiftZone.LifeSupport, "LifeSupport", "LPK_LifeSupport_Scrubber", 180f);
            AddExterior(p, root);
            Place(p["LPK_OxygenLeakPipe_2m"], root, "OxygenLeakPipe", new Vector3(LastShiftShipDimensions.RoomCenterX(LastShiftZone.LifeSupport) - 1.4f, 0f, 1.9f), 180f);
            Place(p["LPK_RepairConsole_1m"], root, "RepairConsole", new Vector3(LastShiftShipDimensions.RoomCenterX(LastShiftZone.Power) + 1.4f, 0f, -1.6f));
            Place(p["LPK_DamagedPipe_2m"], root, "DamagedPipe", new Vector3(LastShiftShipDimensions.RoomCenterX(LastShiftZone.Power) - 1.1f, 0f, -2.0f));
            Place(p["LPK_TetherRack_2m"], root, "TetherRackKit", new Vector3(LastShiftShipDimensions.RoomCenterX(LastShiftZone.Cockpit) - 1.6f, 0f, 1.6f));
            Place(p["LPK_SalvagePad_4m"], root, "SalvagePadKit", new Vector3(-12f, 0f, 0f), 90f);
            Place(p["LPK_Airlock_Exterior_4m"], root, "ExteriorAirlockKit", new Vector3(-10f, 0f, 0f), 90f);
            Place(p["LPK_DeckHatch_2m"], root, "DeckHatchKit", new Vector3(0f, 0.01f, -6f));
        }

        private static void AddRoom(IReadOnlyDictionary<string, GameObject> p, Transform root, LastShiftZone zone, string label, string feature, float rotationY)
        {
            var center = new Vector3(LastShiftShipDimensions.RoomCenterX(zone), 0f, LastShiftShipDimensions.RoomCenterZ(zone));
            for (var x = -2; x <= 2; x += 2)
            for (var z = -2; z <= 2; z += 2)
                Place(p["LPK_Floor_Square_2m"], root, label + "Floor", center + new Vector3(x, 0.01f, z), rotationY);
            Place(p["LPK_Connector_Neck_2m"], root, label + "Connector", center + new Vector3(0f, 0f, rotationY == 0f ? -2.8f : 2.8f), rotationY);
            Place(p["LPK_Door_Airlock_2m"], root, label + "Door", center + new Vector3(0f, 0f, rotationY == 0f ? -1.9f : 1.9f), rotationY);
            Place(p[feature], root, label + "Feature", center + new Vector3(0f, 0f, rotationY == 0f ? 1.55f : -1.55f), rotationY);
            Place(p["LPK_Wall_Straight_4m"], root, label + "BackWall", center + new Vector3(0f, 0f, rotationY == 0f ? 2.35f : -2.35f), rotationY);
            Place(p["LPK_Ceiling_Straight_4m"], root, label + "Ceiling", center + Vector3.up * 2.45f, rotationY);
        }

        private static void AddExterior(IReadOnlyDictionary<string, GameObject> p, Transform root)
        {
            // 원반 선체를 45° 외피 조각으로 두르고, 대각 전환에는 90° 조각을 사용한다.
            for (var i = 0; i < 8; i++)
            {
                var angle = i * 45f * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(angle) * 13.5f, 0f, Mathf.Sin(angle) * 13.5f);
                Place(p["LPK_Hull_Exterior_Curve45"], root, "HullExterior45", pos, -i * 45f);
            }
            for (var i = 0; i < 4; i++)
                Place(p["LPK_Hull_Exterior_Curve90"], root, "HullExterior90", new Vector3((i < 2 ? -1f : 1f) * 15f, 0f, (i % 2 == 0 ? -1f : 1f) * 15f), i * 90f);
            for (var x = -4; x <= 4; x += 4)
                Place(p["LPK_Hull_WindowBay_4m"], root, "CockpitWindowBay", new Vector3(LastShiftShipDimensions.RoomCenterX(LastShiftZone.Cockpit) + x, 0f, -4.3f));
            Place(p["LPK_Cockpit_ViewWindow_4m"], root, "CockpitViewWindow", new Vector3(LastShiftShipDimensions.RoomCenterX(LastShiftZone.Cockpit), 0f, -4.35f));
        }

        private static void Place(GameObject prefab, Transform parent, string name, Vector3 position, float rotationY = 0f)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
        }
    }
}
