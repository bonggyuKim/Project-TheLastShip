using DoodleUp.Core;
using DoodleUp.Input;
using DoodleUp.Physics;
using DoodleUp.Stroke;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    [DefaultExecutionOrder(200)]
    public sealed class DuSandboxController : MonoBehaviour
    {
        public const string ProfileId = "PRETEST_DEPTH_LOCOMOTION_V1";
        public const float DepthTolerance = 0.001f;
        public static readonly Vector3 SpawnPosition = new(0f, 0.1f, 0f);

        [SerializeField] private Du02InputReader inputReader;
        [SerializeField] private Du02PlayerMotor playerMotor;
        [SerializeField] private Transform handMarker;
        [SerializeField] private Du02CameraRig cameraRig;
        [SerializeField] private Du03AStrokeDriver strokeDriver;
        [SerializeField] private Du03BCAdapterRouter adapterRouter;
        [SerializeField] private Du03BCInputEdgeLatch inputLatch;

        private bool strokeDepthCaptured;
        private float strokeRootDepth;
        private float strokeHandDepth;
        private Du03AStrokeSessionState previousStrokeState;
        private bool depthDriftReported;
        private bool wasDepthMovementLocked;

        public int ResetGeneration { get; private set; }
        public int InkResetGeneration { get; private set; }
        public bool DepthMovementLocked => adapterRouter != null
            && adapterRouter.ActiveRoute != Du03BCAdapterRoute.ArmDirect
            && strokeDriver != null
            && strokeDriver.Session != null
            && strokeDriver.Session.State != Du03AStrokeSessionState.Idle;
        public float StrokeRootDepth => strokeRootDepth;
        public float StrokeHandDepth => strokeHandDepth;
        public string ActiveProfileId => adapterRouter != null && adapterRouter.ActiveRoute == Du03BCAdapterRoute.ArmDirect
            ? Du03BCArmDirectInputAdapter.ProfileId
            : ProfileId;

        private void OnEnable()
        {
            if (strokeDriver != null)
                strokeDriver.LateUpdateProcessed += OnStrokeLateUpdateProcessed;
            if (adapterRouter != null)
                adapterRouter.RouteChanged += OnRouteChanged;
        }

        private void OnDisable()
        {
            if (strokeDriver != null)
                strokeDriver.LateUpdateProcessed -= OnStrokeLateUpdateProcessed;
            if (adapterRouter != null)
                adapterRouter.RouteChanged -= OnRouteChanged;
        }

        public void Configure(
            Du02InputReader reader,
            Du02PlayerMotor motor,
            Transform marker,
            Du02CameraRig rig,
            Du03AStrokeDriver driver,
            Du03BCAdapterRouter router,
            Du03BCInputEdgeLatch latch)
        {
            if (strokeDriver != null)
                strokeDriver.LateUpdateProcessed -= OnStrokeLateUpdateProcessed;
            if (adapterRouter != null)
                adapterRouter.RouteChanged -= OnRouteChanged;
            inputReader = reader;
            playerMotor = motor;
            handMarker = marker;
            cameraRig = rig;
            strokeDriver = driver;
            adapterRouter = router;
            inputLatch = latch;
            previousStrokeState = strokeDriver.Session.State;
            if (isActiveAndEnabled)
            {
                strokeDriver.LateUpdateProcessed += OnStrokeLateUpdateProcessed;
                adapterRouter.RouteChanged += OnRouteChanged;
            }
            ApplyRouteProfile(adapterRouter.ActiveRoute);
        }

        public void ApplyMovementForProbe(float horizontal, float forward, bool jumpPressed)
        {
            ApplyMovement(horizontal, forward, jumpPressed);
        }

        public void ResetInk(string reason)
        {
            InkResetGeneration++;
            strokeDriver.ResetSession();
            adapterRouter.ResetActiveAdapter();
            inputLatch.ClearLatchedEdges("INK_RESET");
            strokeDepthCaptured = false;
            strokeRootDepth = 0f;
            strokeHandDepth = 0f;
            previousStrokeState = strokeDriver.Session.State;
            depthDriftReported = false;
            wasDepthMovementLocked = false;
            playerMotor.SetDepthLocomotionAllowed(true);
            Debug.Log(
                $"[DU_SANDBOX_INK_RESET] generation={InkResetGeneration} reason={reason} " +
                $"profile={ActiveProfileId} position={playerMotor.transform.position} " +
                $"ink={strokeDriver.Session.AvailableInk:F2} committedColliders={strokeDriver.CommittedColliderCount}");
        }

        public void ResetSandbox(string reason)
        {
            ResetGeneration++;
            Time.fixedDeltaTime = Du02Profile.FixedDeltaTime;
            playerMotor.ResetState(SpawnPosition);
            cameraRig.ResetPose(SpawnPosition);
            strokeDriver.ResetSession();
            adapterRouter.ResetActiveAdapter();
            ApplyRouteProfile(adapterRouter.ActiveRoute);
            inputLatch.ClearLatchedEdges("SANDBOX_RESET");
            strokeDepthCaptured = false;
            strokeRootDepth = 0f;
            strokeHandDepth = 0f;
            previousStrokeState = strokeDriver.Session.State;
            depthDriftReported = false;
            wasDepthMovementLocked = false;
            Debug.Log(
                $"[DU_SANDBOX_RESET] generation={ResetGeneration} reason={reason} " +
                $"profile={ActiveProfileId} spawn={SpawnPosition} ink={strokeDriver.Session.AvailableInk:F2} " +
                $"committedColliders={strokeDriver.CommittedColliderCount}");
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            ResetSandbox("SCENE_START");
        }

        private void Update()
        {
            var input = inputReader.Current;
            var resetPressed = input.ResetPressed || inputLatch.ConsumeResetPressed();
            if (resetPressed)
            {
                ResetSandbox("R_KEY");
                return;
            }

            if (inputLatch.ConsumeInkResetPressed())
            {
                ResetInk("Q_KEY");
                return;
            }

            var keyboard = Keyboard.current;
            var forward = keyboard == null
                ? 0f
                : (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            ApplyMovement(input.Horizontal, forward, input.JumpPressed);
        }

        private void ApplyMovement(float horizontal, float forward, bool jumpPressed)
        {
            var depthMovementLocked = DepthMovementLocked;
            if (depthMovementLocked && !wasDepthMovementLocked)
                playerMotor.SetDepthLocomotionAllowed(false);
            var movement = depthMovementLocked
                ? new Vector2(Mathf.Clamp(horizontal, -1f, 1f), 0f)
                : cameraRig.TransformMovementForProbe(horizontal, forward);
            playerMotor.SetInput(movement.x, movement.y, jumpPressed, !depthMovementLocked);
            wasDepthMovementLocked = depthMovementLocked;
            TrackStrokeDepth();
        }

        private void OnStrokeLateUpdateProcessed(Du03ALateUpdateEvidence evidence)
        {
            var depthMovementLocked = DepthMovementLocked;
            playerMotor.SetDepthLocomotionAllowed(!depthMovementLocked);
            wasDepthMovementLocked = depthMovementLocked;
            TrackStrokeDepth();
        }

        private void OnRouteChanged(Du03BCAdapterRoute route)
        {
            ApplyRouteProfile(route);
        }

        private void ApplyRouteProfile(Du03BCAdapterRoute route)
        {
            var armDirect = route == Du03BCAdapterRoute.ArmDirect;
            cameraRig.SetArmDirectProfile(armDirect);
            if (armDirect)
            {
                handMarker.SetLocalPositionAndRotation(
                    Du03BCArmDirectInputAdapter.GetNeutralHandLocalPosition(handMarker),
                    Quaternion.identity);
                handMarker.localScale = Vector3.one;
            }
            else
            {
                var localPosition = handMarker.parent != null
                    && handMarker.parent.name == Du02CameraRig.ArmPitchAnchorName
                    && handMarker.parent.parent != null
                    ? handMarker.parent.InverseTransformPoint(
                        handMarker.parent.parent.TransformPoint(Du02Profile.HandLocalPosition))
                    : Du02Profile.HandLocalPosition;
                handMarker.SetLocalPositionAndRotation(localPosition, Quaternion.identity);
                handMarker.localScale = Vector3.one;
            }
        }

        private void TrackStrokeDepth()
        {
            var state = strokeDriver.Session.State;
            if (adapterRouter.ActiveRoute == Du03BCAdapterRoute.ArmDirect)
            {
                strokeDepthCaptured = false;
                previousStrokeState = state;
                return;
            }

            if (previousStrokeState == Du03AStrokeSessionState.Idle
                && state == Du03AStrokeSessionState.Drawing)
            {
                strokeRootDepth = Vector3.Dot(playerMotor.transform.position - SpawnPosition, Vector3.forward);
                strokeHandDepth = Vector3.Dot(handMarker.position - SpawnPosition, Vector3.forward);
                strokeDepthCaptured = true;
                depthDriftReported = false;
                Debug.Log(
                    $"[DU_SANDBOX_STROKE_DEPTH] profile={ActiveProfileId} rootDepth={strokeRootDepth:F6} " +
                    $"handDepth={strokeHandDepth:F6} planeN0={Vector3.forward}");
            }

            if (strokeDepthCaptured && state != Du03AStrokeSessionState.Idle)
            {
                var rootDepth = Vector3.Dot(playerMotor.transform.position - SpawnPosition, Vector3.forward);
                var rootError = Mathf.Abs(rootDepth - strokeRootDepth);
                var handError = adapterRouter.ActiveRoute == Du03BCAdapterRoute.ArmDirect
                    ? 0f
                    : Mathf.Abs(Vector3.Dot(handMarker.position - SpawnPosition, Vector3.forward) - strokeHandDepth);
                if (!depthDriftReported && Mathf.Max(rootError, handError) > DepthTolerance)
                {
                    depthDriftReported = true;
                    Debug.LogError(
                        $"[DU_SANDBOX_INVALID] reason=TECH_INVALID/DRAW_DEPTH_DRIFT profile={ActiveProfileId} " +
                        $"rootError={rootError:F6} handError={handError:F6} tolerance={DepthTolerance:F6}");
                }
            }

            if (state == Du03AStrokeSessionState.Idle)
                strokeDepthCaptured = false;
            previousStrokeState = state;
        }
    }
}
