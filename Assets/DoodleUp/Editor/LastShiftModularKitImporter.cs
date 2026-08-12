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
            ,"LPK_EVA_ConningTower_3m", "LPK_EVA_TopHatch_1p6m", "LPK_EVA_ConningTower_3m", "LPK_EVA_TopHatch_1p6m"
        };

        [MenuItem("Last Shift/SP-02A/Import EVA Kit Prefabs")]
        public static void ImportEvaKit()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(ControllerFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CreatePrefab("LPK_EVA_ConningTower_3m");
            CreatePrefab("LPK_EVA_TopHatch_1p6m");
            AssetDatabase.SaveAssets();
            Debug.Log("[LAST_SHIFT_EVA_KIT] prefabs=2 hatchAnimator=PASS result=PASS");
        }

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
            if (name is "LPK_EVA_ConningTower_3m" or "LPK_EVA_TopHatch_1p6m")
                AlignModelToBottom(modelInstance);
            AddStructuralColliders(modelInstance, name);
            AddSolidPropCollider(modelInstance, name);
            if (TryCreateAnimator(name, root, out var controller))
                root.AddComponent<Animator>().runtimeAnimatorController = controller;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null) throw new InvalidOperationException($"Could not create prefab for {name}");
            return prefab;
        }

        private static void AlignModelToBottom(GameObject modelInstance)
        {
            var renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            var minY = float.PositiveInfinity;
            foreach (var renderer in renderers)
                minY = Mathf.Min(minY, renderer.bounds.min.y);
            if (float.IsFinite(minY))
                modelInstance.transform.localPosition -= Vector3.up * minY;
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
            "LPK_Hull_WindowBay_4m", "LPK_Cockpit_ViewWindow_4m",
            // 탑은 밟고 서는 구조물이다. 해치 뚜껑(LPK_EVA_TopHatch_1p6m)은 여기 없다 -
            // 열려야 하는 것이라 압력문·갑판해치와 같은 규칙을 탄다.
            "LPK_EVA_ConningTower_3m"
        };

        /// <summary>
        /// 정적 레벨 기하라 <see cref="MeshCollider"/> 를 그대로 쓴다 — 볼록 근사를 하면 곡면 벽
        /// (<c>Curve_45</c>) 안쪽이 메워져 회랑이 좁아진다.
        ///
        /// FBX 임포터의 <c>addCollider</c> 를 켜지 않는 이유: 그 스위치는 <b>모델 전체</b>에
        /// 걸려서 문짝과 해치까지 막아 버린다. 어떤 조각이 막고 어떤 조각이 통하는지는 레벨
        /// 규칙이지 임포트 설정이 아니므로, 판단을 코드 쪽에 둔다.
        /// </summary>
        /// <summary>
        /// 벽은 아니지만 <b>몸으로 못 지나가야 하는 가구</b>. 구조물과 달리 <b>상자 하나</b>를
        /// 씌운다 — 형상이 오목하고 장식 요철이 많아 메시 콜라이더로 두면 비싸고, 승무원이
        /// 모서리에 걸린다. 막고 싶은 것은 "책상을 통과하지 않는다" 하나다.
        ///
        /// 조종석 콘솔이 여기 드는 이유는 <b>회색상자 시절에 그쪽에만 콜라이더가 있었기</b>
        /// 때문이다(2026-08-12 정리). 그 상자를 걷으면서 실물이 그 역할을 넘겨받는다.
        /// </summary>
        private static readonly HashSet<string> SolidPropNames = new()
        {
            "LPK_Cockpit_ControlConsole",
            // 냉각 교환기. 회색상자 더미(CoolingStack)를 걷으면서 <b>그 상자가 들고 있던
            // 충돌이 같이 사라졌다</b> — 교환기 자체에는 콜라이더가 없어서 그대로 두면
            // 방 한가운데 설비를 몸으로 통과한다. 걷어낸 쪽이 만든 구멍이라 같이 막는다.
            //
            // 나머지 셋(전력 배전반·산소 스크러버·숙소 침상)은 <b>아직 안 넣는다</b>: 그쪽은
            // 회색상자가 아직 살아 있어 충돌을 들고 있고, 지금 넣으면 한 방에 충돌이 둘이 된다.
            // 그 상자들을 걷는 날 같이 들어와야 한다.
            "LPK_Cooling_Exchanger"
        };

        /// <summary>
        /// 가구에 <b>본체 크기 그대로</b> 상자 하나를 씌운다. 가장 큰 렌더러가 본체다 —
        /// 조종석 콘솔이면 <c>_Body</c> 이고 <c>_Screen</c> 은 그 안에 들어간다.
        /// </summary>
        private static void AddSolidPropCollider(GameObject modelInstance, string name)
        {
            if (!SolidPropNames.Contains(name)) return;

            MeshRenderer body = null;
            var largest = 0f;
            foreach (var renderer in modelInstance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var size = renderer.bounds.size;
                var volume = size.x * size.y * size.z;
                if (volume <= largest) continue;
                largest = volume;
                body = renderer;
            }

            if (body == null || body.GetComponent<Collider>() != null) return;
            var box = body.gameObject.AddComponent<BoxCollider>();
            var local = body.transform.InverseTransformVector(body.bounds.size);
            box.center = body.transform.InverseTransformPoint(body.bounds.center);
            box.size = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        }

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
                "LPK_EVA_TopHatch_1p6m" => (pivot: "EVAHatchLidPivot", clip: "LP_EVA_Hatch_OpenClose", property: "m_LocalRotation.y", end: 0.3827f, loop: false),
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
                    foreach (var target in rule.target) TileBounds(p[rule.assetId], root, rule.id, spaces[target], rule.tile, rule.positionY, rule);
                else if (rule.operation == "wallBoundsWithDoorGap")
                    InteriorBoundsWithDoorGap(p[rule.assetId], root, rule, map, spaces);
                else if (rule.operation == "spanBounds")
                    foreach (var target in rule.target) SpanBounds(p[rule.assetId], root, rule.id, spaces[target], rule.positionY);
                else if (rule.operation == "exteriorBoundsWithDoorGap") ExteriorBoundsWithDoorGap(p[rule.assetId], root, rule, map, spaces);
                else
                {
                    var placed = Place(p[rule.assetId], root, rule.id,
                        Vector(rule.position), rule.rotationY, VectorOrOne(rule.scale));
                    if (rule.id == "evaTopHatch") AttachTopHatch(placed);
                    if (rule.id == "plazaLift") AttachLiftPlatform(placed);
                }
            }
            foreach (var space in map.spaces)
            {
                // <b>개구부에는 문을 안 세운다.</b> 정본이 kind 로 셋을 가르는데
                // (opening · pressure · plain) 임포터가 그 열을 아예 안 읽어서, 문짝이 없어야
                // 할 자리에 압력문 킷이 통째로 서 있었다 — 사용자가 "문이 보이는데 그냥
                // 통과된다" 로 지적한 그 자리다. AI_T_03 의 문안도 "문이 없는 개구부" 다.
                if (IsOpening(space.door))
                {
                    PlaceFeature(p[space.feature], root, space.id + "Feature", space);
                    continue;
                }

                var door = Place(p["LPK_Door_Airlock_2m"], root, space.id + "Door", Vector(space.door.position), space.door.rotationY);
                // 압력문이 붙는 자리가 아니면 열어 둔다. 안 그러면 아무도 애니메이터를 안
                // 건드려 닫힌 자세로 남고, 통행은 되므로 닫힌 문을 그대로 통과하게 된다.
                if (!HasPressureDoor(space.id) && door != null && door.GetComponent<Animator>() != null)
                    door.AddComponent<LastShiftPassageDoor>();
                PlaceFeature(p[space.feature], root, space.id + "Feature", space);
            }
            if (map.lights != null)
                foreach (var light in map.lights) PlaceLight(root, light);
            // Exterior panels own the footprint boundary.  This is a final
            // manifest-derived guard for a wall segment that is emitted on an
            // outer edge because of a non-rectangular union split.
            RemoveInteriorWallsCoveredByShell(root);
            BuildDeckCollision(map, spaces, p, root);
            ReportDressingInsideFeatures(root, PanelTopY(p["LPK_Floor_Square_2m"]));
        }

        /// <summary>
        /// 설비가 붙은 끝벽에 바닥 소품을 두지 않는다. 설비 <c>body bounds</c> 를 이만큼 부풀린
        /// 상자 안에 바닥 소품이 들어오면 어긋난 것이다(game-art 확정 2026-08-12).
        /// </summary>
        public const float DressingKeepOut = 0.10f;

        /// <summary>
        /// 바닥 소품으로 볼 높이. 갑판에서 이 높이 안에 밑면이 있으면 <b>바닥에 놓인 것</b>이다 —
        /// 벽에 거는 소품은 끝벽에 붙어도 설비와 다투지 않으므로 이 규칙 밖이다.
        /// </summary>
        public const float DressingFloorReach = 0.30f;

        /// <summary>
        /// 소품이 설비 자리를 침범하는가. <b>순수 함수다</b> — 씬 없이 규칙만 잰다.
        /// </summary>
        public static bool ViolatesFeatureKeepOut(Bounds feature, Bounds prop, float margin, float deckY, float floorReach)
        {
            if (prop.min.y > deckY + floorReach) return false;
            var grown = feature;
            grown.Expand(margin * 2f);
            return grown.Intersects(prop);
        }

        /// <summary>
        /// 조립이 끝난 배에서 그 규칙을 어긴 소품을 <b>소리 내어 적는다</b>.
        ///
        /// <b>고치지는 않는다.</b> 드레싱 좌표는 아트 몫이라 임포터가 말없이 옮기면 아트가
        /// 놓은 자리와 화면에 보이는 자리가 갈린다. 대신 어느 소품이 어느 설비에 박혔는지를
        /// 좌표까지 적어서, 옮기는 쪽이 바로 손댈 수 있게 한다.
        ///
        /// 설비 치수는 임포트 시점에만 알 수 있어서(아트 에셋 몫) 이 검사가 여기 있다 —
        /// <c>LastShiftDressingRules</c> 쪽 런타임 검사는 그 숫자를 못 본다.
        /// </summary>
        private static void ReportDressingInsideFeatures(Transform root, float deckY)
        {
            if (float.IsNaN(deckY)) return;

            var features = new List<(string name, Bounds box)>();
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.EndsWith("Feature")) continue;
                if (TryMeasure(child, out var box)) features.Add((child.name, box));
            }

            // <b>드레싱은 <paramref name="root"/> 밑에 없다.</b> 여기 root 는 킷 조립 노드이고
            // ZoneDressing 은 그 형제다 — 처음에 root 에서 찾다가 null 이 나와 검사가 통째로
            // 조용히 빠졌고, 로그가 한 줄도 안 찍혀서 그 사실조차 안 보였다. 배 루트에서 찾는다.
            var ship = root;
            while (ship.parent != null) ship = ship.parent;
            var dressing = ship.Find("ZoneDressing");

            if (dressing == null || features.Count == 0)
            {
                // 못 찾은 것도 적는다. 말없이 통과하면 "검사가 돌았고 깨끗했다" 와 구분이 안 된다.
                Debug.LogWarning($"[LAST_SHIFT_DRESSING_FEATURE] features={features.Count} " +
                                 $"dressing={(dressing == null ? "<없음>" : dressing.name)} result=SKIPPED");
                return;
            }

            var clashes = 0;
            foreach (var prop in dressing.GetComponentsInChildren<Transform>(true))
            {
                if (prop.GetComponent<Collider>() == null || !TryMeasure(prop, out var box)) continue;
                foreach (var feature in features)
                {
                    if (!ViolatesFeatureKeepOut(feature.box, box, DressingKeepOut, deckY, DressingFloorReach))
                        continue;
                    // <b>오류가 아니라 경고다.</b> 오류로 내면 씬을 다시 굽는 검사 셋이
                    // "처리 안 된 오류" 로 같이 붉어진다 — 소품 좌표는 아트·TA 몫이라, 그쪽이
                    // 옮기기 전까지 무관한 검사를 막게 된다. 규칙 자체는 EditMode 가 잠근다.
                    // 현재 위반이 정리되면 오류로 올려 하드 게이트로 만들 수 있다.
                    Debug.LogWarning(
                        $"[LAST_SHIFT_DRESSING_FEATURE] prop={prop.name} feature={feature.name} " +
                        $"propCenter={box.center:F2} featureCenter={feature.box.center:F2} " +
                        $"detail=설비 body bounds +{DressingKeepOut:0.##}m 안에 바닥 소품이 있다 — 측벽 쪽으로 옮긴다");
                    clashes++;
                    break;
                }
            }

            Debug.Log($"[LAST_SHIFT_DRESSING_FEATURE] features={features.Count} clashes={clashes}");
        }

        /// <summary>렌더러를 합친 세계 상자. 그릴 것이 없으면 거짓.</summary>
        private static bool TryMeasure(Transform target, out Bounds box)
        {
            var measured = false;
            box = new Bounds();
            foreach (var renderer in target.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (measured) box.Encapsulate(renderer.bounds);
                else { box = renderer.bounds; measured = true; }
            }

            return measured;
        }

        private static void TileBounds(GameObject prefab, Transform root, string name, MapSpace space, float[] tile, float y, MapRule rule = null)
        {
            // <b>판 모서리가 깎여 있다.</b> 네 장이 만나는 자리에 마름모꼴 구멍이 남고,
            // 갑판 아래가 빈 자리에서는 그 구멍으로 우주가 비친다(실측: 경계 표본 1382 중
            // 54 곳). 판을 키워 겹치는 것으로는 못 막는다 — 같은 높이의 두 면이 겹치면
            // 그 띠에서 z-fighting 이 난다.
            //
            // 그래서 <c>offset</c> 을 받는다. 반 칸 어긋난 두 번째 층을 조금 아래에 깔면
            // 그 층의 <b>한가운데</b>가 위층의 모서리에 오므로 구멍이 메워지고, 높이가
            // 달라 z-fighting 도 없다. 위층 밑에 가려 평소에는 안 보인다.
            var offsetX = rule?.offset != null && rule.offset.Length > 0 ? rule.offset[0] : 0f;
            var offsetZ = rule?.offset != null && rule.offset.Length > 1 ? rule.offset[1] : 0f;
            // 밑깔개는 <b>한 칸 넓게</b> 깐다. 반 칸 어긋난 격자라 방 경계에서 한 줄이 모자라고,
            // 경계 판은 마감을 외곽으로 돌리느라 깎인 모서리의 방향도 바뀐다 — 그 두 가지가
            // 겹쳐 경계 줄에만 구멍이 다시 났다(실측 20 곳). 넘치는 부분은 접지면 아래라
            // 벽·외피에 가려 안 보인다.
            var pad = y < 0f ? 1 : 0;
            var startX = space.bounds[0] + tile[0] * 0.5f + offsetX - pad * tile[0];
            var startZ = space.bounds[2] + tile[1] * 0.5f + offsetZ - pad * tile[1];
            var endX = space.bounds[1] + pad * tile[0];
            var endZ = space.bounds[3] + pad * tile[1];
            for (var x = startX; x < endX; x += tile[0])
            for (var z = startZ; z < endZ; z += tile[1])
            {
                if (CoversEvaShaft(x, z, tile, y)) continue;
                var facing = BoundaryFacing(space, x, z, tile);
                var instance = Place(prefab, root, name, new Vector3(x, y, z), facing ?? 0f);
                // 접지면 <b>아래</b>에 깔리는 층(밑깔개)에는 마감을 안 붙인다. 그 층은 반 칸
                // 어긋나 있어 판 경계가 방 외곽과 안 맞고, 마감 띠가 위층 바닥을 뚫고 올라온다.
                ApplyEdgeTrim(instance, facing.HasValue && y >= 0f);
            }
        }

        /// <summary>
        /// 승강 플랫폼을 <see cref="LastShiftEvaLift"/> 에 묶는다.
        ///
        /// 아트 프리팹이 <b>가동 자산</b>이라 별도 모듈을 만들지 않는다(game-art 확인
        /// 2026-08-11) — <c>LiftPlatformPivot</c> 이 이미 그 자리에 있다.
        /// </summary>
        private static void AttachLiftPlatform(GameObject lift)
        {
            if (lift == null) return;
            Transform pivot = null;
            foreach (var t in lift.GetComponentsInChildren<Transform>(true))
                if (t.name == "LiftPlatformPivot") pivot = t;
            if (pivot == null)
            {
                Debug.LogWarning("[LAST_SHIFT_LIFT] LiftPlatformPivot 이 없다 — 판이 안 움직인다.");
                return;
            }
            lift.AddComponent<LastShiftEvaLiftVisual>().Configure(pivot);
        }

        /// <summary>
        /// 상단 해치에 차단 콜라이더와 연동 컴포넌트를 붙인다.
        ///
        /// 뚜껑 자체에는 콜라이더가 없다(구조 메시 목록에 안 넣었다) — 열려야 하는 것이라
        /// 압력문·갑판해치와 같은 규칙을 탄다. 차단은 <b>뚫린 자리</b>를 메우는 별도 판이
        /// 맡고, 그 판은 완전히 닫혔을 때만 켜진다.
        /// </summary>
        private static void AttachTopHatch(GameObject hatch)
        {
            if (hatch == null || hatch.GetComponent<Animator>() == null) return;

            var blockerObject = new GameObject("EvaTopHatch_Blocker");
            blockerObject.transform.SetParent(hatch.transform, false);
            blockerObject.transform.localPosition = Vector3.zero;
            var blocker = blockerObject.AddComponent<BoxCollider>();
            var opening = LastShiftEvaShaft.HatchOpening;
            blocker.size = new Vector3(opening, LastShiftCompartments.PanelThickness, opening);
            hatch.AddComponent<LastShiftEvaTopHatch>().Configure(blocker);
        }

        /// <summary>
        /// <b>걷는 면을 평평하게 만든다.</b> 바닥 판은 가장자리가 파인 패널이라, 2m 간격으로
        /// 깔면 갑판이 <c>12cm</c> 깊이 홈 격자가 된다(실측: 판 윗면 <c>0.12</c>, 테두리 홈
        /// <c>0.00</c>). 승무원은 그 홈에 계속 걸리고, 스폰 높이 <c>0.1</c> 은 판 윗면보다
        /// 아래라 스폰하자마자 판 옆면에 끼어 밀린다 — PlayMode 슬롯 복귀 검사 둘이
        /// <c>0.149m</c> 어긋난 것이 이것이다. 소품이 땅에 박혀 보이는 것도 같은 이유고,
        /// 드레싱 좌표는 <c>y=0</c>(홈 바닥) 기준인데 걷는 면은 <c>0.12</c> 다.
        ///
        /// 홈은 <b>보이는 채로 둔다</b> — 아트가 만든 마감이고 형상은 지도 소관이다. 대신
        /// 판 윗면 높이에 보이지 않는 충돌면을 한 장 깔아 통행만 평평하게 만든다.
        /// 승강구 자리는 비운다. 안 비우면 이 판이 해치를 다시 막는다.
        /// </summary>
        private static void BuildDeckCollision(ModularMap map, IReadOnlyDictionary<string, MapSpace> spaces,
            IReadOnlyDictionary<string, GameObject> prefabs, Transform root)
        {
            var deckY = PanelTopY(prefabs["LPK_Floor_Square_2m"]);
            if (float.IsNaN(deckY)) return;

            var deck = new GameObject("DeckCollision");
            deck.transform.SetParent(root, false);
            // 갑판에는 이제 구멍이 없다. 승강구 둘을 피해 발자국을 쪼개던 자리인데,
            // EVA 가 위로 뒤집히면서 아래로 뚫린 곳이 사라졌다 - 방마다 한 장이면 된다.
            foreach (var space in spaces.Values)
            {
                var b = space.bounds;
                var piece = new GameObject("DeckSlab");
                piece.transform.SetParent(deck.transform, false);
                var box = piece.AddComponent<BoxCollider>();
                box.center = new Vector3((b[0] + b[1]) * 0.5f, deckY - 0.1f, (b[2] + b[3]) * 0.5f);
                box.size = new Vector3(b[1] - b[0], 0.2f, b[3] - b[2]);
            }
            Debug.Log($"[LAST_SHIFT_DECK_COLLISION] deckY={deckY:F3} slabs={deck.transform.childCount}");
        }

        /// <summary>
        /// 판 윗면의 높이. 리터럴로 박지 않는다 — 아트가 판을 바꾸면 따라와야 한다.
        ///
        /// <b>구조 메시만 본다.</b> 전부를 재면 <c>_Edge</c> 마감 띠가 잡혀 <c>0.19</c> 가
        /// 나오는데, 그건 밟는 면이 아니라 테두리 장식이라 승무원이 <c>7cm</c> 떠서 걷게 된다.
        /// 콜라이더를 붙일 때와 같은 기준(부피가 가장 큰 메시)을 쓴다.
        /// </summary>
        private static float PanelTopY(GameObject floorPrefab)
        {
            MeshFilter structural = null;
            var largest = 0f;
            foreach (var filter in floorPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var size = filter.sharedMesh.bounds.size;
                var volume = size.x * size.y * size.z;
                if (structural != null && volume <= largest) continue;
                structural = filter;
                largest = volume;
            }
            return structural == null
                ? float.NaN
                : structural.transform.localToWorldMatrix.MultiplyPoint(structural.sharedMesh.bounds.max).y;
        }


        /// <summary>
        /// 이 판이 EVA 샤프트를 막는가. 막으면 깔지 않는다.
        ///
        /// <b>구멍이 갑판에서 천장으로 옮겨 갔다</b>(기획 확정 2026-08-11). 예전에는 갑판에
        /// 승강구 둘이 뚫려 아래로 내려갔고, 지금은 광장 코어가 위로 올라가므로 <b>천장</b>이
        /// 열려야 한다. 바닥은 도로 막는다 - 리프트 플랫폼이 갑판 높이에 서 있고 승무원이
        /// 그 위에 서므로, 여기를 비우면 광장 한가운데가 구덩이가 된다.
        ///
        /// 코어는 <c>4x4</c> 라 <c>2m</c> 판 넉 장이 정확히 그 자리를 덮는다.
        /// </summary>
        private static bool CoversEvaShaft(float x, float z, float[] tile, float y)
        {
            if (y < 0.5f) return false;                       // 갑판은 안 뚫는다
            var halfX = tile[0] * 0.5f;
            var halfZ = tile[1] * 0.5f;
            var half = LastShiftEvaShaft.HalfExtent;
            return Mathf.Abs(x) < halfX + half - 0.01f && Mathf.Abs(z) < halfZ + half - 0.01f;
        }

        /// <summary>
        /// 이 판이 공간 <b>외곽</b>에 닿는가. 닿으면 그 방향으로 돌릴 <c>rotationY</c> 를,
        /// 안쪽 판이면 <c>null</c> 을 준다.
        ///
        /// <c>_Edge</c> 마감은 판의 <b>+z 면 한 곳에만</b> 들어 있다. 그래서 "외곽에 남긴다" 를
        /// 하려면 켜고 끄는 것만으로는 안 되고 그 면이 외곽을 보도록 돌려야 한다. 정사각형
        /// 판이라 돌려도 발자국과 이음매는 그대로다.
        ///
        /// <b>모서리 판은 한 면만 마감된다.</b> 두 면이 동시에 외곽인 자리에서 한쪽을 고를
        /// 수밖에 없다 — 두 면을 마감하려면 부재가 따로 있거나 판을 두 장 겹쳐야 하고,
        /// 그건 아트 쪽 결정이다.
        /// </summary>
        private static float? BoundaryFacing(MapSpace space, float x, float z, float[] tile)
        {
            const float epsilon = 0.01f;
            var halfX = tile[0] * 0.5f;
            var halfZ = tile[1] * 0.5f;
            if (z + halfZ >= space.bounds[3] - epsilon) return 0f;      // 로컬 +z 가 그대로 +z
            if (z - halfZ <= space.bounds[2] + epsilon) return 180f;
            if (x + halfX >= space.bounds[1] - epsilon) return 90f;     // +90 이 +z 를 +x 로 보낸다
            if (x - halfX <= space.bounds[0] + epsilon) return -90f;
            return null;
        }

        /// <summary>
        /// 외곽이 아닌 판에서는 <c>_Edge</c> 마감을 지운다. 남겨 두면 <c>2m</c> 격자 무늬가
        /// 바닥 전체에 깔리는데, 그건 아트가 의도한 모습이 아니다(2026-08-11 결정).
        ///
        /// 렌더러를 끄지 않고 <b>오브젝트를 지운다</b>. 꺼 두면 프리팹 오버라이드로 남아
        /// 나중에 누군가 켰을 때 조용히 돌아온다.
        /// </summary>
        private static void ApplyEdgeTrim(GameObject instance, bool keep)
        {
            if (instance == null || keep) return;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.name.EndsWith("_Edge", StringComparison.Ordinal)) continue;
                UnityEngine.Object.DestroyImmediate(renderer.gameObject);
            }
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
        /// <summary>
        /// 이 자리가 <b>개구부</b>인가 — 문짝이 없는 구멍이다. 정본 <c>kind</c> 를 그대로 읽고,
        /// 값이 없으면 문으로 본다: 모르는 자리에 구멍을 뚫는 것보다 문을 세우는 쪽이 안전하다.
        /// </summary>
        private static bool IsOpening(MapDoor door) =>
            door != null && string.Equals(door.kind, "opening", StringComparison.OrdinalIgnoreCase);

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

        /// <summary>
        /// 대형 설비를 끝벽에서 띄우는 간격(m). <b>0 이 아니다</b> — 벽 메시와 설비 메시가
        /// 정확히 같은 평면에 있으면 z-fighting 이 나고, 벽 안쪽 마감 띠가 있는 조각에서는
        /// 설비가 그 띠를 뚫고 들어간 것처럼 보인다(game-art 확정 2026-08-12).
        /// </summary>
        public const float FeatureWallInset = 0.05f;

        /// <summary>
        /// 방 설비를 <b>문 맞은편 끝벽</b>에 붙이기 위해 방 중앙에서 밀어야 할 거리.
        ///
        /// <b>왜 중앙에 두면 안 되는가.</b> 설비는 콜라이더를 가진 덩어리라, 방 한가운데
        /// 있으면 그 방의 통행이 설비를 도는 고리가 된다 — 냉각실에서 승무원이 방 중앙에서
        /// 막혔고(4인 통행 검사), 그 방에 볼일이 있는 사람이 목표 지점에 못 갔다.
        ///
        /// <b>문 맞은편을 고르는 이유.</b> 끝벽 넷 중 아무 데나 붙이면 어떤 방에서는 설비가
        /// 문 바로 옆에 서서 개구부·접근면을 먹는다. 문이 붙은 변을 찾아 그 반대편에 두면
        /// 문에서 가장 먼 벽이 되므로 개구부·접근면·통행폭 셋 다 원리적으로 안 건드린다.
        ///
        /// 문 좌표가 없으면 밀지 않는다 — 개구부만 있고 문짝이 없는 방도 문 좌표는 들고 있다.
        /// </summary>
        public static Vector3 EndWallShift(float[] bounds, float[] doorPosition, Bounds feature, float inset)
        {
            if (bounds == null || bounds.Length < 4 || doorPosition == null || doorPosition.Length < 3)
                return Vector3.zero;

            float minX = bounds[0], maxX = bounds[1], minZ = bounds[2], maxZ = bounds[3];

            // 문이 붙은 변 — 네 변 중 문 좌표가 가장 가까운 것.
            var gap = new[]
            {
                Mathf.Abs(doorPosition[0] - minX), Mathf.Abs(doorPosition[0] - maxX),
                Mathf.Abs(doorPosition[2] - minZ), Mathf.Abs(doorPosition[2] - maxZ),
            };
            var side = 0;
            for (var i = 1; i < gap.Length; i++) if (gap[i] < gap[side]) side = i;

            return side switch
            {
                0 => new Vector3(maxX - inset - feature.max.x, 0f, 0f),
                1 => new Vector3(minX + inset - feature.min.x, 0f, 0f),
                2 => new Vector3(0f, 0f, maxZ - inset - feature.max.z),
                _ => new Vector3(0f, 0f, minZ + inset - feature.min.z),
            };
        }

        /// <summary>
        /// 방 설비 하나를 놓는다. 중앙에 한 번 놓고 <b>실제 렌더러 크기를 재서</b> 끝벽으로
        /// 민다 — 설비 치수는 아트 에셋 몫이라 코드가 미리 알 수 없다.
        /// </summary>
        private static void PlaceFeature(GameObject prefab, Transform root, string name, MapSpace space)
        {
            var placed = Place(prefab, root, name, BoundsCenter(space.bounds));
            if (placed == null || space.door == null) return;

            if (!TryMeasure(placed.transform, out var box)) return;
            placed.transform.position += EndWallShift(space.bounds, space.door.position, box, FeatureWallInset);
        }
        private static Vector3 Vector(float[] values) => new(values[0], values[1], values[2]);
        private static Vector3 VectorOrOne(float[] values) => values == null || values.Length == 0 ? Vector3.one : Vector(values);

        private static GameObject Place(GameObject prefab, Transform parent, string name, Vector3 position, float rotationY = 0f, Vector3? scale = null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            instance.transform.localScale = scale ?? Vector3.one;
            return instance;
        }

        /// <summary>
        /// 이 공간이 압력 경계 문을 갖는가. 경계는 셋뿐이고(전력·냉각·산소) 조종석·숙소는
        /// 광장과 한 구역이라 압력문이 없다 — 정본은 <see cref="LastShiftPlazaLayout"/> 쪽이라
        /// 여기 이름을 박지 않고 그쪽에 묻는다.
        /// </summary>
        private static bool HasPressureDoor(string spaceId)
        {
            for (var boundary = 0; boundary < LastShiftPlazaLayout.PressureBoundaryCount; boundary++)
            {
                var space = LastShiftPlazaLayout.RoomOf(LastShiftPlazaLayout.HighZoneOf(boundary));
                if (string.Equals(SpaceIdOf(space), spaceId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string SpaceIdOf(LastShiftPlazaSpace space) => space switch
        {
            LastShiftPlazaSpace.CockpitRoom => "cockpit",
            LastShiftPlazaSpace.PowerRoom => "power",
            LastShiftPlazaSpace.CoolingRoom => "cooling",
            LastShiftPlazaSpace.LifeSupportRoom => "lifeSupport",
            _ => "quarters"
        };

        [Serializable] private sealed class ModularMap { public string schema; public MapCamera cockpitCamera; public MapPlaza plaza; public MapSpace[] spaces; public MapRule[] placementRules; public MapLight[] lights; public string[] excluded; }
        [Serializable] private sealed class MapCamera { public float[] spawn; public float[] lookAt; }
        [Serializable] private sealed class MapPlaza { public float[] bounds; public float ceiling; }
        [Serializable] private sealed class MapSpace { public string id; public float[] bounds; public float ceiling; public MapDoor door; public string feature; }
        [Serializable] private sealed class MapDoor { public float[] position; public float rotationY; public string kind; }
        [Serializable] private sealed class MapLight { public string id; public float[] position; public float[] color; public float intensity; public float range; }
        [Serializable] private sealed class MapRule { public string id; public string assetId; public string[] target; public string operation; public float[] tile; public float positionY; public float gapWidth; public float[] position; public float rotationY; public float[] scale; public float radius; public int count; public float rotationStep; public float shellClearance; public float[] offset; }
    }
}
