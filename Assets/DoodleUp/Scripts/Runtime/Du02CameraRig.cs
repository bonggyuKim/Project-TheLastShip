using DoodleUp.Core;
using DoodleUp.Input;
using DoodleUp.Stroke;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    [DefaultExecutionOrder(50)]
    public sealed class Du02CameraRig : MonoBehaviour
    {
        public const string PretestProfileId = "PRETEST_FIRST_PERSON_V2";
        public const float PretestYawSpeed = 60f;
        public const float PretestYawLimit = 30f;
        public const float FirstPersonLookSensitivity = 0.12f;
        public const float FirstPersonPitchLimit = 80f;
        public const string BodyYawAnchorName = "BodyYawAnchor";
        public const string ArmPitchAnchorName = "ArmPitchAnchor";
        public static Vector3 PretestEyeOffset => Du02Profile.PretestCameraLocalPosition;
        public static readonly Vector3 ArmDirectEyeOffset = new(0f, 1.20f, 0f);
        public static readonly Vector3 ArmPitchAnchorLocalPosition = new(0.34f, 0.92f, 0.18f);

        private bool armDirectProfileEnabled;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform bodyYawAnchor;
        [SerializeField] private Transform armPitchAnchor;
        [SerializeField] private Du03AStrokeDriver strokeDriver;
        [SerializeField] private Du03BCInputEdgeLatch inputLatch;
        [SerializeField] private bool pretestOrbitEnabled = true;

        private Vector3 orbitPivot;
        private float yawOffset;
        private float pitchOffset;
        private bool yawInputLogged;

        public bool PretestOrbitEnabled => pretestOrbitEnabled && Application.isEditor && !Application.isBatchMode;
        public bool FirstPersonLookEnabled => armDirectProfileEnabled && Application.isPlaying && Application.isEditor && !Application.isBatchMode;
        public bool LookInputAvailable => armDirectProfileEnabled && CanAcceptLookInput();
        public float VisualYawOffset => yawOffset;
        public float VisualPitchOffset => pitchOffset;
        public Vector3 GameplayNormal => Vector3.forward;
        public Vector3 PlanarForward => Quaternion.Euler(0f, yawOffset, 0f) * Vector3.forward;
        public Vector3 PlanarRight => Quaternion.Euler(0f, yawOffset, 0f) * Vector3.right;
        public Transform BodyYawAnchor => bodyYawAnchor;
        public Transform ArmPitchAnchor => armPitchAnchor;
        public string ActiveProfileId => armDirectProfileEnabled
            ? Du03BCArmDirectInputAdapter.ProfileId
            : PretestOrbitEnabled ? PretestProfileId : Du02Profile.ProfileId;

        public void SetArmDirectProfile(bool enabled)
        {
            armDirectProfileEnabled = enabled;
            yawOffset = 0f;
            pitchOffset = 0f;
            ApplyBodyLook();
            ApplyVisualPose();
            targetCamera.fieldOfView = Du02Profile.CameraVerticalFov;
            if (enabled) UpdateCursorLock();
            else ReleaseCursor();
        }

        public void Configure(
            Camera cameraComponent,
            Transform root,
            Transform yawAnchor = null,
            Transform pitchAnchor = null)
        {
            targetCamera = cameraComponent;
            playerRoot = root;
            bodyYawAnchor = yawAnchor;
            armPitchAnchor = pitchAnchor;
        }

        public void ConfigurePretestOrbit(Du03AStrokeDriver driver, Du03BCInputEdgeLatch latch, bool enabled)
        {
            strokeDriver = driver;
            inputLatch = latch;
            pretestOrbitEnabled = enabled;
        }

        public void ResetPose(Vector3 playerPosition)
        {
            orbitPivot = playerPosition;
            yawOffset = 0f;
            pitchOffset = 0f;
            yawInputLogged = false;
            ApplyBodyLook();
            ApplyVisualPose();
            targetCamera.fieldOfView = Du02Profile.CameraVerticalFov;
            UpdateCursorLock();
        }

        public void TickFirstPersonLookForProbe(Vector2 delta)
        {
            TickFirstPersonLook(delta, false);
        }

        public Vector2 TransformMovementForProbe(float horizontal, float forward)
        {
            var world = PlanarRight * horizontal + PlanarForward * forward;
            var planar = Vector2.ClampMagnitude(new Vector2(world.x, world.z), 1f);
            return planar;
        }

        public void TickPretestOrbit(float horizontal, float deltaTime)
        {
            if (!CanOrbit() || Mathf.Approximately(horizontal, 0f)) return;

            ApplyYaw(horizontal, deltaTime, true);
        }

        public void TickPretestOrbitForProbe(float horizontal, float deltaTime)
        {
            if (!CanOrbitForProbe() || Mathf.Approximately(horizontal, 0f)) return;

            ApplyYaw(horizontal, deltaTime, false);
        }

        private void ApplyYaw(float horizontal, float deltaTime, bool emitEvidence)
        {
            yawOffset = Mathf.Clamp(
                yawOffset + Mathf.Clamp(horizontal, -1f, 1f) * PretestYawSpeed * deltaTime,
                -PretestYawLimit,
                PretestYawLimit);
            ApplyVisualPose(!emitEvidence);

            if (emitEvidence && !yawInputLogged)
            {
                yawInputLogged = true;
                Debug.Log(
                    $"[DU02_PROVENANCE] event=CAMERA_ORBIT profile_id={PretestProfileId} " +
                    $"camera_visual_yaw={Du02LogFormat.Float(yawOffset)} gameplay_n0={Du02LogFormat.Vector(GameplayNormal)} " +
                    $"state={strokeDriver.Session.State}");
                Debug.LogWarning(
                    $"[DU02_PROVENANCE_INVALID] reason=TECH_INVALID/CAMERA_YAW_INPUT_ENABLED " +
                    $"profile_id={PretestProfileId} camera_visual_yaw={Du02LogFormat.Float(yawOffset)} " +
                    $"gameplay_n0={Du02LogFormat.Vector(GameplayNormal)}");
            }
        }

        private void Update()
        {
            if (FirstPersonLookEnabled)
            {
                if (Mouse.current != null)
                    TickFirstPersonLook(Mouse.current.delta.ReadValue(), true);
                return;
            }

            if (!PretestOrbitEnabled || Keyboard.current == null) return;
            var horizontal = (Keyboard.current.rightArrowKey.isPressed ? 1f : 0f)
                - (Keyboard.current.leftArrowKey.isPressed ? 1f : 0f);
            TickPretestOrbit(horizontal, Time.unscaledDeltaTime);
        }

        private void TickFirstPersonLook(Vector2 delta, bool requirePlayableProfile)
        {
            if ((requirePlayableProfile && !FirstPersonLookEnabled)
                || !CanAcceptLookInput()
                || delta == Vector2.zero)
                return;

            yawOffset = Mathf.Repeat(yawOffset + delta.x * FirstPersonLookSensitivity + 180f, 360f) - 180f;
            pitchOffset = Mathf.Clamp(
                pitchOffset - delta.y * FirstPersonLookSensitivity,
                -FirstPersonPitchLimit,
                FirstPersonPitchLimit);
            ApplyBodyLook();
            ApplyVisualPose();
        }

        private void LateUpdate()
        {
            FollowPlayer();
        }

        public void FollowPlayerForProbe()
        {
            FollowPlayer();
        }

        private void FollowPlayer()
        {
            if (playerRoot == null) return;
            orbitPivot = playerRoot.position;
            ApplyVisualPose();
        }

        private bool CanOrbit()
        {
            return PretestOrbitEnabled && CanOrbitForProbe();
        }

        private bool CanOrbitForProbe()
        {
            return pretestOrbitEnabled && CanAcceptLookInput();
        }

        private bool CanAcceptLookInput()
        {
            if (strokeDriver == null) strokeDriver = FindFirstObjectByType<Du03AStrokeDriver>();
            if (inputLatch == null) inputLatch = FindFirstObjectByType<Du03BCInputEdgeLatch>();
            if (strokeDriver == null || strokeDriver.Session == null) return false;
            if (armDirectProfileEnabled)
                return strokeDriver.Session.State == Du03AStrokeSessionState.Idle
                    || strokeDriver.Session.State == Du03AStrokeSessionState.Drawing;
            return strokeDriver.Session.State == Du03AStrokeSessionState.Idle
                && (inputLatch == null || !inputLatch.DrawHeld);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) UpdateCursorLock();
            else ReleaseCursor();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) ReleaseCursor();
            else UpdateCursorLock();
        }

        private void OnDisable()
        {
            ReleaseCursor();
        }

        private void UpdateCursorLock()
        {
            if (!FirstPersonLookEnabled) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ApplyBodyLook()
        {
            if (bodyYawAnchor != null)
                bodyYawAnchor.localRotation = Quaternion.Euler(0f, yawOffset, 0f);
            if (armPitchAnchor != null)
                armPitchAnchor.localRotation = Quaternion.Euler(pitchOffset, 0f, 0f);
        }

        private void ApplyVisualPose(bool forcePretest = false)
        {
            if (armDirectProfileEnabled)
            {
                var rotation = Quaternion.Euler(pitchOffset, yawOffset, 0f);
                transform.SetPositionAndRotation(orbitPivot + ArmDirectEyeOffset, rotation);
                return;
            }

            if (forcePretest || PretestOrbitEnabled)
            {
                var rotation = Quaternion.Euler(Du02Profile.PretestCameraPitch, yawOffset, 0f);
                transform.SetPositionAndRotation(orbitPivot + PretestEyeOffset, rotation);
                return;
            }

            var offset = new Vector3(0f, Du02Profile.CameraHeight, -Du02Profile.CameraDistance);
            var fixedRotation = Quaternion.Euler(Du02Profile.CameraPitch, 0f, 0f);
            transform.SetPositionAndRotation(orbitPivot + offset, fixedRotation);
        }
    }
}
