using System.IO;
using DoodleUp.Core;
using DoodleUp.Input;
using DoodleUp.Physics;
using DoodleUp.Runtime;
using DoodleUp.Stroke;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Editor
{
    public static class DuSandboxSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/DU_Sandbox.unity";
        public const string FloorName = "SandboxFloor";
        public const string BridgeTaskName = "FunTask_Bridge";
        public const string RampTaskName = "FunTask_Ramp";
        public const string CurvedRailTaskName = "FunTask_CurvedRail";
        public const string StartMarkerName = "StartMarker";
        public const string DestinationMarkerName = "DestinationMarker";
        public static readonly Vector3 FloorSize = new(40f, 0.2f, 40f);

        [MenuItem("Doodle Up/DU-03BC/Rebuild Sandbox")]
        public static void RebuildSandbox()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DU_Sandbox";

            CreateLighting();
            CreateFloor();
            CreateFunBlockout();
            var player = CreatePlayer();
            var bodyYawAnchor = CreateBodyYawAnchor(player.transform);
            CreatePlayerVisual(bodyYawAnchor);
            var armPitchAnchor = CreateArmPitchAnchor(bodyYawAnchor);
            var handMarker = CreateHandMarker(armPitchAnchor);
            var camera = CreateCamera();
            var runtime = new GameObject("DU_Sandbox_Runtime");

            var inputReader = runtime.AddComponent<Du02InputReader>();
            var inputLatch = runtime.AddComponent<Du03BCInputEdgeLatch>();
            var deterministicSource = runtime.AddComponent<Du03ADeterministicIntentSource>();
            var aimAdapter = runtime.AddComponent<Du03BCAimInputAdapter>();
            aimAdapter.Configure(inputLatch, handMarker, camera);
            var trajectoryAdapter = runtime.AddComponent<Du03BCTrajectoryInputAdapter>();
            trajectoryAdapter.Configure(inputLatch, handMarker, camera);
            var armDirectAdapter = runtime.AddComponent<Du03BCArmDirectInputAdapter>();
            armDirectAdapter.Configure(inputLatch, handMarker, camera);
            var router = runtime.AddComponent<Du03BCAdapterRouter>();
            router.Configure(deterministicSource, aimAdapter, trajectoryAdapter, armDirect: armDirectAdapter);
            router.ConfigurePlayableStartRoute(Du03BCAdapterRoute.ArmDirect);
            router.SetRoute(Du03BCAdapterRoute.ArmDirect);

            var previewObject = new GameObject("DU03A_PendingGhost");
            previewObject.transform.SetParent(runtime.transform, false);
            var previewLine = previewObject.AddComponent<LineRenderer>();
            previewLine.useWorldSpace = true;
            previewLine.widthMultiplier = 0.12f;
            previewLine.sharedMaterial = CreateMaterial("DU03AStrokePreviewMaterial", Color.white);
            previewLine.startColor = new Color(0.15f, 0.95f, 1f, 1f);
            previewLine.endColor = new Color(0.15f, 0.95f, 1f, 1f);
            previewLine.numCapVertices = 6;
            previewLine.numCornerVertices = 4;
            previewLine.positionCount = 0;
            previewLine.enabled = false;

            var committedRoot = new GameObject("DU03A_CommittedStrokes");
            committedRoot.transform.SetParent(runtime.transform, false);
            var strokeDriver = runtime.AddComponent<Du03AStrokeDriver>();
            strokeDriver.Configure(
                handMarker,
                camera,
                router,
                "sandbox-player",
                Du03AStrokeMode.Spatial,
                previewLine,
                committedRoot.transform);
            router.SetStrokeDriver(strokeDriver);

            var cameraRig = camera.gameObject.AddComponent<Du02CameraRig>();
            cameraRig.Configure(camera, player.transform, bodyYawAnchor, armPitchAnchor);
            cameraRig.ConfigurePretestOrbit(strokeDriver, inputLatch, true);

            var reachObject = new GameObject("DU03BC_ReachIndicator");
            reachObject.transform.SetParent(runtime.transform, false);
            var reachLine = reachObject.AddComponent<LineRenderer>();
            reachLine.sharedMaterial = CreateMaterial("DU03BCReachMaterial", Color.white);
            runtime.AddComponent<Du03BCPlayabilityVisuals>().Configure(handMarker, strokeDriver, reachLine);

            var controller = runtime.AddComponent<DuSandboxController>();
            controller.Configure(
                inputReader,
                player.GetComponent<Du02PlayerMotor>(),
                handMarker,
                cameraRig,
                strokeDriver,
                router,
                inputLatch);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DU_SANDBOX_BUILD] scene={ScenePath} profile={DuSandboxController.ProfileId} result=PASS");
        }

        private static void CreateFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = FloorName;
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.transform.localScale = FloorSize;
            floor.layer = LayerMask.NameToLayer("Course");
            floor.GetComponent<MeshRenderer>().sharedMaterial =
                CreateMaterial("SandboxFloorMaterial", new Color(0.32f, 0.34f, 0.38f));
        }

        private static void CreateFunBlockout()
        {
            CreateBridgeTask();
            CreateRampTask();
            CreateCurvedRailTask();
        }

        private static void CreateBridgeTask()
        {
            var root = new GameObject(BridgeTaskName);
            root.transform.position = new Vector3(-3.2f, 0f, 3.2f);
            CreateCourseCube("StartPlatform", root.transform, new Vector3(-1.35f, 0.35f, 0f), new Vector3(1.5f, 0.7f, 1.8f));
            CreateCourseCube("DestinationPlatform", root.transform, new Vector3(1.35f, 0.35f, 0f), new Vector3(1.5f, 0.7f, 1.8f));
            CreateMarker(StartMarkerName, root.transform, new Vector3(-1.35f, 0.76f, 0f), new Color(0.20f, 0.95f, 0.35f));
            CreateMarker(DestinationMarkerName, root.transform, new Vector3(1.35f, 0.76f, 0f), new Color(1f, 0.65f, 0.10f));
        }

        private static void CreateRampTask()
        {
            var root = new GameObject(RampTaskName);
            root.transform.position = new Vector3(0f, 0f, 5.2f);
            CreateCourseCube("StartDeck", root.transform, new Vector3(0f, 0.25f, -1f), new Vector3(1.8f, 0.5f, 1.4f));
            CreateCourseCube("HighPlatform", root.transform, new Vector3(0f, 1.15f, 1f), new Vector3(1.8f, 0.35f, 1.6f));
            CreateMarker(StartMarkerName, root.transform, new Vector3(0f, 0.61f, -1f), new Color(0.20f, 0.95f, 0.35f));
            CreateMarker(DestinationMarkerName, root.transform, new Vector3(0f, 1.41f, 1f), new Color(1f, 0.65f, 0.10f));
        }

        private static void CreateCurvedRailTask()
        {
            var root = new GameObject(CurvedRailTaskName);
            root.transform.position = new Vector3(3.5f, 0f, 3.1f);
            var points = new[]
            {
                new Vector3(-1.25f, 0.75f, -0.65f),
                new Vector3(-0.65f, 0.75f, -0.15f),
                new Vector3(-0.15f, 0.75f, 0.50f),
                new Vector3(0.15f, 0.75f, 1.25f),
                new Vector3(0.20f, 0.75f, 2.05f)
            };
            var yaws = new[] { -40f, -35f, -20f, -5f, 0f };
            for (var index = 0; index < points.Length; index++)
            {
                CreateCourseCube(
                    $"CurveWalkway_{index}",
                    root.transform,
                    points[index],
                    new Vector3(1.05f, 0.25f, 1.05f),
                    Quaternion.Euler(0f, yaws[index], 0f));
            }

            for (var index = 0; index < points.Length; index++)
            {
                var innerRailPosition = points[index] + new Vector3(-0.48f, 0.48f, 0f);
                CreateCourseCube(
                    $"InnerRail_{index}",
                    root.transform,
                    innerRailPosition,
                    new Vector3(0.10f, 0.75f, 1f),
                    Quaternion.Euler(0f, yaws[index], 0f));
            }

            CreateMarker(StartMarkerName, root.transform, points[0] + Vector3.up * 0.24f, new Color(0.20f, 0.95f, 0.35f));
            CreateMarker(DestinationMarkerName, root.transform, points[^1] + Vector3.up * 0.24f, new Color(1f, 0.65f, 0.10f));
        }

        private static GameObject CreateCourseCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion? localRotation = null)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.SetLocalPositionAndRotation(localPosition, localRotation ?? Quaternion.identity);
            cube.transform.localScale = localScale;
            cube.layer = LayerMask.NameToLayer("Course");
            cube.GetComponent<MeshRenderer>().sharedMaterial =
                CreateMaterial("SandboxTaskMaterial", new Color(0.42f, 0.45f, 0.52f));
            return cube;
        }

        private static void CreateMarker(string name, Transform parent, Vector3 localPosition, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.SetLocalPositionAndRotation(localPosition, Quaternion.identity);
            marker.transform.localScale = new Vector3(0.34f, 0.06f, 0.34f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<MeshRenderer>().sharedMaterial =
                CreateMaterial($"Sandbox{name}Material", color);
        }

        private static GameObject CreatePlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = DuSandboxController.SpawnPosition;
            player.layer = LayerMask.NameToLayer("Player");
            var body = player.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = true;
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.radius = 0.25f;
            capsule.height = 1f;
            capsule.center = new Vector3(0f, 0.5f, 0f);
            player.AddComponent<Du02PlayerMotor>();
            return player;
        }

        private static Transform CreateBodyYawAnchor(Transform player)
        {
            var anchor = new GameObject(Du02CameraRig.BodyYawAnchorName);
            anchor.transform.SetParent(player, false);
            anchor.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            anchor.transform.localScale = Vector3.one;
            return anchor.transform;
        }

        private static Transform CreateArmPitchAnchor(Transform bodyYawAnchor)
        {
            var anchor = new GameObject(Du02CameraRig.ArmPitchAnchorName);
            anchor.transform.SetParent(bodyYawAnchor, false);
            anchor.transform.SetLocalPositionAndRotation(
                Du02CameraRig.ArmPitchAnchorLocalPosition,
                Quaternion.identity);
            anchor.transform.localScale = Vector3.one;
            return anchor.transform;
        }

        private static void CreatePlayerVisual(Transform player)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "BodyVisual";
            body.transform.SetParent(player, false);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().sharedMaterial =
                CreateMaterial("SandboxPlayerBodyMaterial", new Color(0.30f, 0.42f, 0.95f));
        }

        private static Transform CreateHandMarker(Transform player)
        {
            var marker = new GameObject("HandMarker");
            marker.transform.SetParent(player, false);
            marker.transform.localPosition = Du03BCArmDirectInputAdapter.NeutralHandPitchLocalPosition;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;
            return marker.transform;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = Du02Profile.CameraVerticalFov;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            return camera;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            return new Material(Shader.Find("Standard")) { name = name, color = color };
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.68f);
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
