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
        public static readonly Vector3 FloorSize = new(40f, 0.2f, 40f);

        [MenuItem("Doodle Up/DU-03BC/Rebuild Sandbox")]
        public static void RebuildSandbox()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DU_Sandbox";

            CreateLighting();
            CreateFloor();
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
