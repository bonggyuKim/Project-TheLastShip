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
        private const string MaterialFolder = "Assets/DoodleUp/Art/Materials";
        private const string MapPath = "Assets/DoodleUp/Data/LastShiftModularMap.json";
        private static readonly string[] Names =
        {
            "LPK_Wall_Straight_2m", "LPK_Wall_Straight_4m", "LPK_Wall_Window_4m", "LPK_Wall_Curve_45", "LPK_Corner_Outer_90", "LPK_Corner_Inner_90",
            "LPK_Floor_Square_2m", "LPK_Floor_Curve_45", "LPK_Ceiling_Straight_4m", "LPK_Support_Pillar", "LPK_Door_Airlock_2m", "LPK_Connector_Neck_2m",
            "LPK_CentralLift_4m", "LPK_Cockpit_ControlConsole", "LPK_LifeSupport_Scrubber", "LPK_Power_Switchgear", "LPK_Cooling_Exchanger", "LPK_Quarters_Bunk"
            ,"LPK_Hull_Exterior_Curve45", "LPK_Hull_Exterior_Curve90", "LPK_Hull_Exterior_Panel_4m", "LPK_Hull_WindowBay_4m", "LPK_Cockpit_ViewWindow_4m", "LPK_Ceiling_Curve45", "LPK_Floor_Transition_2m", "LPK_Airlock_Exterior_4m", "LPK_DeckHatch_2m", "LPK_OxygenLeakPipe_2m", "LPK_RepairConsole_1m", "LPK_DamagedPipe_2m", "LPK_SalvagePad_4m", "LPK_TetherRack_2m", "LPK_Cockpit_WallMirror_1m"
        };

        [MenuItem("Last Shift/SP-02A/Create Cockpit Mirror and Assemble")]
        public static void CreateCockpitMirrorAndAssemble()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(MaterialFolder);
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var name in Names)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
                if (prefab == null && name == "LPK_Cockpit_WallMirror_1m") prefab = CreateCockpitWallMirrorPrefab();
                if (prefab == null) throw new InvalidOperationException($"Required modular prefab missing: {name}");
                prefabs[name] = prefab;
            }
            Assemble(LastShiftSceneBuilder.RebuildShipPrefab(), prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log("[LAST_SHIFT_MODULAR_MAP] cockpitMirror=1 result=PASS");
        }

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
            if (name == "LPK_Cockpit_WallMirror_1m") return CreateCockpitWallMirrorPrefab();
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
            AddStructuralColliders(modelInstance, name);
            if (TryCreateAnimator(name, root, out var controller))
                root.AddComponent<Animator>().runtimeAnimatorController = controller;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null) throw new InvalidOperationException($"Could not create prefab for {name}");
            return prefab;
        }

        /// <summary>
        /// 통행을 막아야 하는 킷 조각. <b>이 목록에 없으면 통과된다</b> — 그것이 기본값이라
        /// 문·해치·거울은 여기 없고, 벽·바닥·천장·외피만 있다.
        ///
        /// 이 목록이 생기기 전까지 맵으로 깔린 기하는 <b>전부 보이기만 하고 통과됐다</b>.
        /// 충돌은 <c>CreateBypassDuct</c>/<c>CreateDiscHull</c> 이 세우는 원시 큐브가 혼자 지고
        /// 있었고, 그래서 그 둘을 지울 수가 없었다. 지우려면 여기가 먼저 서야 한다.
        /// </summary>
        private static readonly HashSet<string> StructuralNames = new()
        {
            "LPK_Wall_Straight_2m", "LPK_Wall_Straight_4m", "LPK_Wall_Window_4m", "LPK_Wall_Curve_45",
            "LPK_Corner_Outer_90", "LPK_Corner_Inner_90",
            "LPK_Floor_Square_2m", "LPK_Floor_Curve_45", "LPK_Floor_Transition_2m",
            "LPK_Ceiling_Straight_4m", "LPK_Ceiling_Curve45",
            "LPK_Support_Pillar", "LPK_Connector_Neck_2m", "LPK_CentralLift_4m",
            "LPK_Hull_Exterior_Panel_4m", "LPK_Hull_Exterior_Curve45", "LPK_Hull_Exterior_Curve90",
            "LPK_Hull_WindowBay_4m", "LPK_Cockpit_ViewWindow_4m"
        };

        /// <summary>
        /// 정적 레벨 기하라 <see cref="MeshCollider"/> 를 그대로 쓴다 — 볼록 근사를 하면 곡면 벽
        /// (<c>Curve_45</c>) 안쪽이 메워져 회랑이 좁아진다.
        ///
        /// FBX 임포터의 <c>addCollider</c> 를 켜지 않는 이유: 그 스위치는 <b>모델 전체</b>에
        /// 걸려서 문짝과 해치까지 막아 버린다. 어떤 조각이 막고 어떤 조각이 통하는지는 레벨
        /// 규칙이지 임포트 설정이 아니므로, 판단을 코드 쪽에 둔다.
        /// </summary>
        private static void AddStructuralColliders(GameObject modelInstance, string name)
        {
            if (!StructuralNames.Contains(name)) return;
            var filters = new List<MeshFilter>();
            foreach (var filter in modelInstance.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.GetComponent<Collider>() == null) filters.Add(filter);
            if (filters.Count == 0) return;

            // <b>장식 메시에는 안 붙인다.</b> 조각마다 구조 메시 하나와 장식 몇 개가 같이 들어
            // 있다 — 바닥의 <c>_Edge</c>, 벽의 <c>_InnerTrim</c>, 천장의 <c>_Light</c>,
            // 외피의 <c>ExteriorRib</c>/<c>Stripe</c> 같은 것들이다. 전부에 붙였더니 바닥 2m
            // 격자마다 <c>8cm</c> 턱이 생겼다. 걷는 데는 <c>stepOffset 0.3</c> 이 넘겨 주지만,
            // 이 게임은 저중력에서 물건이 뜨는 게 본체라 그 턱 격자가 뜬 물건을 잡아 둔다.
            //
            // 이름 규칙 대신 <b>부피</b>로 고른다. 아트가 파츠 이름을 바꿔도 따라오고,
            // 코너처럼 구조 메시가 둘인 조각도 같이 살아남는다.
            var largest = 0f;
            foreach (var filter in filters)
            {
                var size = filter.sharedMesh.bounds.size;
                largest = Mathf.Max(largest, size.x * size.y * size.z);
            }

            foreach (var filter in filters)
            {
                var size = filter.sharedMesh.bounds.size;
                if (size.x * size.y * size.z < largest * 0.25f) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
        }

        private static GameObject CreateCockpitWallMirrorPrefab()
        {
            var path = $"{PrefabFolder}/LPK_Cockpit_WallMirror_1m.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(MaterialFolder);
            var surface = GetOrCreateMaterial("LastShiftMirrorSurface", new Color(0.10f, 0.16f, 0.23f), 0.68f, 0.72f);
            var frame = GetOrCreateMaterial("LastShiftMirrorFrame", LastShiftUiTheme.Ivory, 0.1f, 0.28f);
            var root = new GameObject("LPK_Cockpit_WallMirror_1m");
            CreateMirrorPart(root.transform, "ReflectiveSurface", new Vector3(0f, 0.55f, 0f), new Vector3(0.66f, 0.96f, 0.04f), surface);
            CreateMirrorPart(root.transform, "FrameTop", new Vector3(0f, 1.065f, 0f), new Vector3(0.80f, 0.07f, 0.07f), frame);
            CreateMirrorPart(root.transform, "FrameBottom", new Vector3(0f, 0.035f, 0f), new Vector3(0.80f, 0.07f, 0.07f), frame);
            CreateMirrorPart(root.transform, "FrameLeft", new Vector3(-0.365f, 0.55f, 0f), new Vector3(0.07f, 1.10f, 0.07f), frame);
            CreateMirrorPart(root.transform, "FrameRight", new Vector3(0.365f, 0.55f, 0f), new Vector3(0.07f, 1.10f, 0.07f), frame);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateMirrorPart(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private static Material GetOrCreateMaterial(string name, Color color, float metallic, float smoothness)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = shader != null
                ? new Material(shader)
                : new Material(AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat"));
            material.name = name;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
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
            if (map.lights != null)
                foreach (var light in map.lights) PlaceLight(root, light);
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
                if (Divides(targets, (min + max) * 0.5f, zs[zi] - 0.01f, (min + max) * 0.5f, zs[zi] + 0.01f))
                    PlaceExteriorSegment(prefab, root, rule, map.spaces, min, max, zs[zi], 0f, true);
            }
            for (var xi = 0; xi < xs.Count; xi++)
            for (var zi = 0; zi < zs.Count - 1; zi++)
            {
                var min = zs[zi]; var max = zs[zi + 1]; if (max - min < 0.01f) continue;
                if (Divides(targets, xs[xi] - 0.01f, (min + max) * 0.5f, xs[xi] + 0.01f, (min + max) * 0.5f))
                    PlaceExteriorSegment(prefab, root, rule, map.spaces, min, max, xs[xi], 90f, false);
            }
        }

        /// <summary>
        /// 이 격자선이 <b>서로 다른 두 공간을 가르는가</b>.
        ///
        /// 예전에는 "양쪽이 다 어떤 공간 안" 이면 벽을 세웠다. 그러면 <b>한 공간 내부를 지나는
        /// 격자선</b>도 조건이 선다 — 다른 방의 발자국 모서리가 격자에 좌표를 하나 보태기만
        /// 하면, 그 좌표에서 광장이 통째로 잘렸다. 실제로 광장 <c>x=±4, z=±4</c> 에 벽이 서서
        /// 한가운데가 <c>8x8</c> 상자로 갇혔다. 판이 통과 가능한 장식이던 동안에는 안 드러났고,
        /// 킷에 콜라이더가 붙는 순간 진짜 벽이 됐다.
        /// </summary>
        private static bool Divides(List<MapSpace> spaces, float ax, float az, float bx, float bz)
        {
            var a = SpaceAt(spaces, ax, az);
            var b = SpaceAt(spaces, bx, bz);
            return a != null && b != null && !ReferenceEquals(a, b);
        }

        private static MapSpace SpaceAt(List<MapSpace> spaces, float x, float z)
        {
            foreach (var space in spaces)
                if (x > space.bounds[0] && x < space.bounds[1] && z > space.bounds[2] && z < space.bounds[3]) return space;
            return null;
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

        private static void PlaceLight(Transform root, MapLight spec)
        {
            var lightRoot = new GameObject(spec.id);
            lightRoot.transform.SetParent(root, false);
            lightRoot.transform.localPosition = Vector(spec.position);
            var light = lightRoot.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(spec.color[0], spec.color[1], spec.color[2]);
            light.intensity = spec.intensity;
            light.range = spec.range;
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

        /// <summary>
        /// 이 안쪽 벽을 외피가 <b>이미 덮고 있는가</b>.
        ///
        /// <b>닿는 것과 덮는 것은 다르다.</b> 예전에는 겹침이 <c>0.01m</c> 만 넘어도 지웠는데,
        /// 광장 경계처럼 외피와 안쪽 벽이 <b>같은 평면 위에서 이어 달리는</b> 자리에서는 두
        /// 판이 끝점에서 맞닿기만 해도 조건이 섰다. 그래서 압력 경계 벽이 통째로 지워졌고,
        /// 광장과 방 다섯이 형상으로는 열린 채 남았다 — 문은 서 있는데 옆이 뚫려 있었다.
        /// 그레이박스 벽이 같은 자리를 메우고 있어서 플레이로는 안 드러났다.
        ///
        /// 그래서 <see cref="Covers"/> 로 바꾼다. 외피가 그 벽의 구간을 <b>거의 전부</b> 덮을
        /// 때만 중복이다.
        /// </summary>
        private static bool SharesBoundaryFace(Bounds wall, Bounds shell)
        {
            var xFace = wall.size.x < 0.75f && shell.size.x < 0.75f && Mathf.Abs(wall.center.x - shell.center.x) < 0.6f && Covers(wall.min.z, wall.max.z, shell.min.z, shell.max.z);
            var zFace = wall.size.z < 0.75f && shell.size.z < 0.75f && Mathf.Abs(wall.center.z - shell.center.z) < 0.6f && Covers(wall.min.x, wall.max.x, shell.min.x, shell.max.x);
            return (xFace || zFace) && Overlaps(wall.min.y, wall.max.y, shell.min.y, shell.max.y);
        }

        private static bool Overlaps(float aMin, float aMax, float bMin, float bMax) => Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin) > 0.01f;

        /// <summary><c>b</c> 가 <c>a</c> 구간을 거의 전부(<c>0.05m</c> 여유) 덮는가.</summary>
        private static bool Covers(float aMin, float aMax, float bMin, float bMax) =>
            Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin) >= (aMax - aMin) - 0.05f;

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

        [Serializable] private sealed class ModularMap { public string schema; public MapCamera cockpitCamera; public MapPlaza plaza; public MapSpace[] spaces; public MapRule[] placementRules; public MapLight[] lights; public string[] excluded; }
        [Serializable] private sealed class MapCamera { public float[] spawn; public float[] lookAt; }
        [Serializable] private sealed class MapPlaza { public float[] bounds; public float ceiling; }
        [Serializable] private sealed class MapSpace { public string id; public float[] bounds; public float ceiling; public MapDoor door; public string feature; }
        [Serializable] private sealed class MapDoor { public float[] position; public float rotationY; }
        [Serializable] private sealed class MapLight { public string id; public float[] position; public float[] color; public float intensity; public float range; }
        [Serializable] private sealed class MapRule { public string id; public string assetId; public string[] target; public string operation; public float[] tile; public float positionY; public float gapWidth; public float[] position; public float rotationY; public float[] scale; public float radius; public int count; public float rotationStep; public float shellClearance; }
    }
}
