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
        public const string PretestProfileId = "PRETEST_CAMERA_ORBIT_V1";
        public const float PretestYawSpeed = 60f;
        public const float PretestYawLimit = 30f;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Du03AStrokeDriver strokeDriver;
        [SerializeField] private Du03BCInputEdgeLatch inputLatch;
        [SerializeField] private bool pretestOrbitEnabled = true;

        private Vector3 orbitPivot;
        private float yawOffset;
        private bool yawInputLogged;

        public bool PretestOrbitEnabled => pretestOrbitEnabled && Application.isEditor && !Application.isBatchMode;
        public float VisualYawOffset => yawOffset;
        public Vector3 GameplayNormal => Vector3.forward;
        public string ActiveProfileId => PretestOrbitEnabled ? PretestProfileId : Du02Profile.ProfileId;

        public void Configure(Camera cameraComponent, Transform root)
        {
            targetCamera = cameraComponent;
            playerRoot = root;
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
            yawInputLogged = false;
            ApplyVisualPose();
            targetCamera.fieldOfView = Du02Profile.CameraVerticalFov;
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
            ApplyVisualPose();

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
            if (!PretestOrbitEnabled || Keyboard.current == null) return;
            var horizontal = (Keyboard.current.rightArrowKey.isPressed ? 1f : 0f)
                - (Keyboard.current.leftArrowKey.isPressed ? 1f : 0f);
            TickPretestOrbit(horizontal, Time.unscaledDeltaTime);
        }

        private bool CanOrbit()
        {
            return PretestOrbitEnabled && CanOrbitForProbe();
        }

        private bool CanOrbitForProbe()
        {
            if (strokeDriver == null) strokeDriver = FindFirstObjectByType<Du03AStrokeDriver>();
            if (inputLatch == null) inputLatch = FindFirstObjectByType<Du03BCInputEdgeLatch>();
            return pretestOrbitEnabled
                && strokeDriver != null
                && strokeDriver.Session != null
                && strokeDriver.Session.State == Du03AStrokeSessionState.Idle
                && (inputLatch == null || !inputLatch.DrawHeld);
        }

        private void ApplyVisualPose()
        {
            var yaw = Quaternion.Euler(0f, yawOffset, 0f);
            var offset = yaw * new Vector3(0f, Du02Profile.CameraHeight, -Du02Profile.CameraDistance);
            var rotation = Quaternion.Euler(Du02Profile.CameraPitch, yawOffset, 0f);
            transform.SetPositionAndRotation(orbitPivot + offset, rotation);
        }
    }
}
