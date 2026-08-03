using System.IO;
using System.Linq;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Editor
{
    public static class LastShiftSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP01.unity";
        public const string CockpitZoneName = "Zone_Cockpit";
        public const string UtilityZoneName = "Zone_UtilityCorridor";
        public const string LifeSupportZoneName = "Zone_LifeSupport";

        private static Material hullMaterial;
        private static Material floorMaterial;
        private static Material cockpitMaterial;
        private static Material utilityMaterial;
        private static Material lifeSupportMaterial;

        [MenuItem("Last Shift/SP-01/Rebuild Sandbox")]
        public static void RebuildSandbox()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[LAST_SHIFT_BUILD] cancelled=true reason=active-scene-not-saved");
                return;
            }

            BuildAndSaveSandbox();
        }

        public static void RebuildSandboxForAutomation()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
                throw new System.InvalidOperationException("Refusing to replace a dirty active scene during automated SP-01 rebuild.");

            BuildAndSaveSandbox();
        }

        public static bool HasUnsavedActiveSceneChanges()
        {
            var activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() && activeScene.isDirty;
        }

        private static void BuildAndSaveSandbox()
        {
            ResetCachedMaterials();
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "LAST_SHIFT_SP01";
            CreateLighting();
            CreateShipGraybox();
            var player = CreatePlayer();
            var items = CreateItems();
            var runtime = new GameObject("LAST_SHIFT_SP01_Runtime");
            runtime.AddComponent<LastShiftSandboxController>().Configure(player, items);
            CreateMeteorStimulus();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[LAST_SHIFT_BUILD] scene={ScenePath} zones=3 players=1 items={items.Length} buildScene=1 result=PASS");
        }

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FirstOrDefault(scene => scene.path == ScenePath);
            if (existing != null)
            {
                existing.enabled = true;
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void CreateShipGraybox()
        {
            var ship = new GameObject("ShipGraybox");
            CreateZone(CockpitZoneName, ship.transform, new Vector3(-4f, 0f, 0f), cockpitMaterial ??= CreateMaterial("LS_Cockpit", new Color(0.24f, 0.38f, 0.50f)));
            CreateZone(UtilityZoneName, ship.transform, Vector3.zero, utilityMaterial ??= CreateMaterial("LS_Utility", new Color(0.42f, 0.38f, 0.28f)));
            CreateZone(LifeSupportZoneName, ship.transform, new Vector3(4f, 0f, 0f), lifeSupportMaterial ??= CreateMaterial("LS_LifeSupport", new Color(0.26f, 0.48f, 0.36f)));
            CreateCube("OuterHull_Left", ship.transform, new Vector3(-6.15f, 1.5f, 0f), new Vector3(0.2f, 3f, 5f), hullMaterial ??= CreateMaterial("LS_Hull", new Color(0.18f, 0.20f, 0.23f)));
            CreateCube("OuterHull_Right", ship.transform, new Vector3(6.15f, 1.5f, 0f), new Vector3(0.2f, 3f, 5f), hullMaterial);
            CreateCube("OuterHull_Back", ship.transform, new Vector3(0f, 1.5f, 2.45f), new Vector3(12.5f, 3f, 0.2f), hullMaterial);
            CreateCube("OuterHull_FrontLower", ship.transform, new Vector3(0f, 0.3f, -2.45f), new Vector3(12.5f, 0.6f, 0.2f), hullMaterial);
            CreateCube("Bulkhead_Left", ship.transform, new Vector3(-2f, 1.5f, 0f), new Vector3(0.15f, 3f, 3.2f), hullMaterial);
            CreateCube("Bulkhead_Right", ship.transform, new Vector3(2f, 1.5f, 0f), new Vector3(0.15f, 3f, 3.2f), hullMaterial);
            CreateCube("CockpitConsole", ship.transform, new Vector3(-5.1f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 2.5f), cockpitMaterial);
            CreateCube("TetherRack", ship.transform, TetherRackPosition, TetherRackScale, cockpitMaterial);
            CreateCube("BusCabinet", ship.transform, new Vector3(0f, 0.65f, 1.8f), new Vector3(1.6f, 1.3f, 0.5f), utilityMaterial);
            CreateCube("LifeSupportRack", ship.transform, new Vector3(5.1f, 0.75f, 1.6f), new Vector3(0.8f, 1.5f, 0.8f), lifeSupportMaterial);
            CreateZoneLabel("COCKPIT", new Vector3(-4f, 2.25f, 2.25f), cockpitMaterial.color);
            CreateZoneLabel("UTILITY / BUS", new Vector3(0f, 2.25f, 2.25f), utilityMaterial.color);
            CreateZoneLabel("LIFE SUPPORT", new Vector3(4f, 2.25f, 2.25f), lifeSupportMaterial.color);
        }

        private static void CreateZone(string name, Transform parent, Vector3 position, Material material)
        {
            var zone = new GameObject(name);
            zone.transform.SetParent(parent, false);
            zone.transform.position = position;
            CreateCube("Floor", zone.transform, new Vector3(0f, -0.1f, 0f), new Vector3(4f, 0.2f, 5f), floorMaterial ??= CreateMaterial("LS_Floor", new Color(0.30f, 0.32f, 0.35f)));
            var strip = CreateCube("ZoneStrip", zone.transform, new Vector3(0f, 0.015f, 2.2f), new Vector3(3.7f, 0.03f, 0.25f), material);
            Object.DestroyImmediate(strip.GetComponent<Collider>());
        }

        private static LastShiftPlayerController CreatePlayer()
        {
            var player = new GameObject("PlayerOne");
            player.transform.position = LastShiftSandboxController.PlayerSpawn;
            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.28f;
            controller.height = 1.7f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            var cameraObject = new GameObject("PlayerOne Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            var socket = new GameObject("HoldSocket").transform;
            socket.SetParent(cameraObject.transform, false);
            socket.localPosition = new Vector3(0.45f, -0.30f, 1.1f);
            var playerController = player.AddComponent<LastShiftPlayerController>();
            playerController.Configure(camera, socket);
            CreatePlayerMarker(player.transform, new Color(0.2f, 0.65f, 1f));
            return playerController;
        }

        private static void CreatePlayerMarker(Transform player, Color identityColor)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = "PlayerOne_Identity";
            marker.transform.SetParent(player, false);
            marker.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            marker.transform.localScale = new Vector3(0.32f, 0.48f, 0.32f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial("LS_PlayerOne", identityColor);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        /// <summary>
        /// Tether 는 어떤 프리셋에서도 loose 로 유지되는 유일한 상시 grab 대상이므로 시작 위치에서
        /// 보이면서 GrabDistance(2.2m) 안이어야 한다. 예전 (-3.1, 0.25, 1.55) 는 spawn 에서 2.85m 로
        /// 사거리 밖이었고, 바닥 높이 아이템은 카메라(y≈1.65, 수직 FOV 72°) 기준 사거리 안으로
        /// 당길수록 화면 밖으로 내려가 조준 자체가 불가능하다. 그래서 받침대(TetherRack) 위에 올린다.
        /// loose 상태의 Rigidbody 는 kinematic 이 아니므로 공중 배치는 낙하한다.
        /// </summary>
        public static readonly Vector3 TetherRackPosition = new(-2.62f, 0.60f, -1.28f);

        public static readonly Vector3 TetherRackScale = new(0.5f, 1.2f, 0.9f);

        public static readonly Vector3 TetherSpawnPosition = new(-2.62f, 1.325f, -1.28f);

        private static LastShiftGrabbable[] CreateItems()
        {
            return new[]
            {
                CreateItem("Battery", LastShiftItemRole.Battery, new Vector3(0.6f, 0.38f, 0.8f), new Vector3(0.65f, 0.65f, 0.9f), new Color(0.95f, 0.65f, 0.12f), true),
                CreateItem("CoolingCanister", LastShiftItemRole.CoolingCanister, new Vector3(0f, 0.55f, -1.3f), new Vector3(0.55f, 1.1f, 0.55f), new Color(0.15f, 0.72f, 0.95f), true),
                CreateItem("PatchPlate", LastShiftItemRole.PatchPlate, new Vector3(4.5f, 0.65f, -1.6f), new Vector3(1.15f, 1.15f, 0.18f), new Color(0.78f, 0.82f, 0.88f), true),
                CreateItem("Tether", LastShiftItemRole.Tether, TetherSpawnPosition, new Vector3(0.25f, 0.25f, 1.2f), new Color(0.95f, 0.30f, 0.22f), true)
            };
        }

        private static LastShiftGrabbable CreateItem(string name, LastShiftItemRole role, Vector3 position, Vector3 scale, Color color, bool secured)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial($"LS_{name}", color);
            var body = item.AddComponent<Rigidbody>();
            body.mass = role == LastShiftItemRole.Battery ? 8f : 3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var grabbable = item.AddComponent<LastShiftGrabbable>();
            grabbable.Configure(role, secured);
            return grabbable;
        }

        private static void CreateMeteorStimulus()
        {
            var meteor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteor.name = "CanonicalMeteorStimulus";
            meteor.transform.position = LastShiftMeteorStimulus.Canonical.ImpactPoint - LastShiftMeteorStimulus.Canonical.ImpactVector * 2f;
            meteor.transform.localScale = Vector3.one * 0.65f;
            meteor.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial("LS_Meteor", new Color(0.82f, 0.22f, 0.08f));
            Object.DestroyImmediate(meteor.GetComponent<Collider>());
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            return cube;
        }

        private static void CreateZoneLabel(string text, Vector3 position, Color color)
        {
            var label = new GameObject($"Label_{text}");
            label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var textMesh = label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.08f;
            textMesh.color = color;
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.52f);
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        private static void ResetCachedMaterials()
        {
            hullMaterial = null;
            floorMaterial = null;
            cockpitMaterial = null;
            utilityMaterial = null;
            lifeSupportMaterial = null;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            return new Material(shader) { name = name, color = color };
        }
    }
}
