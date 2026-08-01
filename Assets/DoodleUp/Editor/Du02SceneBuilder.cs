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
    public static class Du02SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/DU02_SoloCourse.unity";

        [MenuItem("Doodle Up/DU-02/Rebuild Solo Course")]
        public static void RebuildSoloCourse()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = Du02Profile.SceneId;

            CreateLighting();
            CreateCourse();

            var player = CreatePlayer();
            CreatePlayerVisual(player.transform);
            var handMarker = CreateHandMarker(player.transform);
            var camera = CreateCamera();
            var runtime = new GameObject("DU02_Runtime");

            var inputReader = runtime.AddComponent<Du02InputReader>();
            var taskState = runtime.AddComponent<Du02TaskState>();
            var frameProbe = runtime.AddComponent<Du02RuntimeFrameProbe>();
            var samplingSeam = runtime.AddComponent<Du02CandidateSamplingSeam>();
            samplingSeam.Configure(handMarker, handMarker.position, Vector3.forward);

            var cameraRig = camera.gameObject.AddComponent<Du02CameraRig>();
            cameraRig.Configure(camera, player.transform);

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

            var inputEdgeLatch = runtime.AddComponent<Du03BCInputEdgeLatch>();
            var deterministicIntentSource = runtime.AddComponent<Du03ADeterministicIntentSource>();
            var aimAdapter = runtime.AddComponent<Du03BCAimInputAdapter>();
            aimAdapter.Configure(inputEdgeLatch, handMarker, camera);
            var trajectoryAdapter = runtime.AddComponent<Du03BCTrajectoryInputAdapter>();
            trajectoryAdapter.Configure(inputEdgeLatch, handMarker, camera);
            var adapterRouter = runtime.AddComponent<Du03BCAdapterRouter>();
            adapterRouter.Configure(deterministicIntentSource, aimAdapter, trajectoryAdapter);
            adapterRouter.SetRoute(Du03BCAdapterRoute.Aim);
            var committedStrokeRoot = new GameObject("DU03A_CommittedStrokes");
            committedStrokeRoot.transform.SetParent(runtime.transform, false);
            committedStrokeRoot.transform.localScale = Vector3.one;

            var strokeDriver = runtime.AddComponent<Du03AStrokeDriver>();
            strokeDriver.Configure(
                handMarker,
                camera,
                adapterRouter,
                "player-1",
                Du03AStrokeMode.Aim,
                previewLine,
                committedStrokeRoot.transform);
            adapterRouter.SetStrokeDriver(strokeDriver);
            cameraRig.ConfigurePretestOrbit(strokeDriver, inputEdgeLatch, true);

            var reachObject = new GameObject("DU03BC_ReachIndicator");
            reachObject.transform.SetParent(runtime.transform, false);
            var reachLine = reachObject.AddComponent<LineRenderer>();
            reachLine.sharedMaterial = CreateMaterial("DU03BCReachMaterial", Color.white);
            var playabilityVisuals = runtime.AddComponent<Du03BCPlayabilityVisuals>();
            playabilityVisuals.Configure(handMarker, strokeDriver, reachLine);

            var motor = player.GetComponent<Du02PlayerMotor>();
            var reset = runtime.AddComponent<Du02ResetCoordinator>();
            reset.Configure(motor, handMarker, cameraRig, camera, samplingSeam, taskState, strokeDriver, adapterRouter);

            var controller = runtime.AddComponent<Du02RuntimeController>();
            controller.Configure(inputReader, motor, handMarker, reset, taskState);
            var resetInputBridge = runtime.AddComponent<Du03BCResetInputBridge>();
            resetInputBridge.Configure(inputEdgeLatch, controller);
            runtime.AddComponent<Du02ProvenanceLogger>();
            var du03AProbe = runtime.AddComponent<Du03ARuntimeProbeRunner>();
            du03AProbe.Configure(strokeDriver, controller, deterministicIntentSource);
            var du03BCProbe = runtime.AddComponent<Du03BCRuntimeProbeRunner>();
            du03BCProbe.Configure(strokeDriver, adapterRouter, aimAdapter, trajectoryAdapter, inputEdgeLatch, controller, handMarker, camera);
            var probeRunner = runtime.AddComponent<Du02RuntimeProbeRunner>();
            probeRunner.Configure(frameProbe, samplingSeam, reset, taskState, motor, controller, handMarker, camera, du03AProbe, du03BCProbe);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[DU02_VERIFY] sceneBuilt={ScenePath} course={Du02Profile.CourseId} profile={Du02Profile.ProfileId}");
        }

        private static GameObject CreatePlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = Du02CourseDefinition.Get(Du02TaskId.T1Horizontal).SpawnPosition;
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

        private static void CreatePlayerVisual(Transform player)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "BodyVisual";
            body.transform.SetParent(player, false);
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial("PlayerBodyMaterial", new Color(0.30f, 0.42f, 0.95f));
        }

        private static Transform CreateHandMarker(Transform player)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "HandMarker";
            marker.transform.SetParent(player, false);
            marker.transform.localPosition = Du02Profile.HandLocalPosition;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial("HandMarkerMaterial", new Color(1f, 0.55f, 0.05f));

            var handCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handCore.name = "HandVisual";
            handCore.transform.SetParent(marker.transform, false);
            handCore.transform.localPosition = Vector3.zero;
            handCore.transform.localRotation = Quaternion.identity;
            handCore.transform.localScale = Vector3.one * 0.22f;
            Object.DestroyImmediate(handCore.GetComponent<Collider>());
            handCore.GetComponent<MeshRenderer>().sharedMaterial = marker.GetComponent<MeshRenderer>().sharedMaterial;
            marker.GetComponent<MeshRenderer>().enabled = false;
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

        private static void CreateCourse()
        {
            foreach (Du02TaskId taskId in System.Enum.GetValues(typeof(Du02TaskId)))
            {
                var lane = Du02CourseDefinition.Get(taskId);
                var root = new GameObject(taskId.ToString());
                CreateBox("StartLedge", lane.StartCenter, lane.StartSize, root.transform, new Color(0.45f, 0.45f, 0.48f));
                CreateBox("GoalLedge", lane.GoalCenter, lane.GoalSize, root.transform, new Color(0.58f, 0.58f, 0.62f));

                root.layer = LayerMask.NameToLayer("Course");
                var goal = CreateBox("GoalZone", lane.GoalCenter + new Vector3(0f, 0.65f, 0f), new Vector3(0.50f, 1.00f, 1.50f), root.transform, new Color(0.15f, 0.75f, 0.30f, 0.25f));
                goal.layer = LayerMask.NameToLayer("Goal");
                goal.GetComponent<BoxCollider>().isTrigger = true;
                goal.AddComponent<Du02GoalZone>().Configure(taskId);

                if (taskId == Du02TaskId.T3Bridge)
                {
                    var startEdge = lane.StartCenter.x + lane.StartSize.x * 0.5f;
                    var goalEdge = lane.GoalCenter.x - lane.GoalSize.x * 0.5f;
                    CreateContactBand("StartContactBand", new Vector3(startEdge, 0.12f, lane.Origin.z), root.transform);
                    CreateContactBand("GoalContactBand", new Vector3(goalEdge, 0.12f, lane.Origin.z), root.transform);
                }
            }
        }

        private static GameObject CreateBox(string name, Vector3 position, Vector3 scale, Transform parent, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent);
            box.transform.SetPositionAndRotation(position, Quaternion.identity);
            box.transform.localScale = scale;
            box.layer = LayerMask.NameToLayer("Course");
            box.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial($"{name}Material", color);
            return box;
        }

        private static void CreateContactBand(string name, Vector3 position, Transform parent)
        {
            var band = CreateBox(name, position, new Vector3(Du02Profile.T3ContactBandWidth, 0.04f, 2.02f), parent, new Color(0.25f, 0.70f, 1f));
            Object.DestroyImmediate(band.GetComponent<BoxCollider>());
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            return material;
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
