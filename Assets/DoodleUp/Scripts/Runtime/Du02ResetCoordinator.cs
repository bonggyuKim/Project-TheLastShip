using DoodleUp.Core;
using DoodleUp.Physics;
using DoodleUp.Stroke;
using UnityEngine;

namespace DoodleUp.Runtime
{
    public sealed class Du02ResetCoordinator : MonoBehaviour
    {
        [SerializeField] private Du02PlayerMotor playerMotor;
        [SerializeField] private Transform handMarker;
        [SerializeField] private Du02CameraRig cameraRig;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Du02CandidateSamplingSeam samplingSeam;
        [SerializeField] private Du02TaskState taskState;
        [SerializeField] private Du03AStrokeDriver strokeDriver;

        public int Generation { get; private set; }
        public Du02TaskId ActiveTask { get; private set; }

        public void Configure(
            Du02PlayerMotor motor,
            Transform marker,
            Du02CameraRig rig,
            Camera cameraComponent,
            Du02CandidateSamplingSeam seam,
            Du02TaskState state,
            Du03AStrokeDriver stroke = null)
        {
            playerMotor = motor;
            handMarker = marker;
            cameraRig = rig;
            targetCamera = cameraComponent;
            samplingSeam = seam;
            taskState = state;
            strokeDriver = stroke;
        }

        public Du02ResetSnapshot ResetToLane(Du02TaskId taskId, string reason)
        {
            var lane = Du02CourseDefinition.Get(taskId);
            ActiveTask = taskId;
            Generation++;

            Time.fixedDeltaTime = Du02Profile.FixedDeltaTime;
            playerMotor.ResetState(lane.SpawnPosition);
            handMarker.SetLocalPositionAndRotation(Du02Profile.HandLocalPosition, Quaternion.identity);
            handMarker.localScale = Vector3.one;
            cameraRig.ResetPose(lane.SpawnPosition);
            taskState.ResetState(taskId);
            strokeDriver?.ResetSession();

            var planeOrigin = handMarker.position;
            var planeNormal = Vector3.forward;
            samplingSeam.ResetCounter(planeOrigin, planeNormal);

            var snapshot = CaptureSnapshot(taskId);
            var depthError = Mathf.Abs(playerMotor.transform.position.z - lane.SpawnPosition.z);
            Debug.Log($"[DU02_RESET] generation={Generation} reason={reason} task={taskId} stateHash={snapshot.GetHashCode()} player={Du02LogFormat.Vector(snapshot.PlayerPosition)} rotation={Du02LogFormat.Quaternion(snapshot.PlayerRotation)} velocity={Du02LogFormat.Vector(snapshot.Velocity)} angularVelocity={Du02LogFormat.Vector(snapshot.AngularVelocity)} handLocal={Du02LogFormat.Vector(snapshot.HandLocalPosition)} handScale={Du02LogFormat.Vector(snapshot.HandLocalScale)} camera={Du02LogFormat.Vector(snapshot.CameraPosition)} fov={Du02LogFormat.Float(snapshot.CameraFov)} phase={snapshot.Phase} countdown={Du02LogFormat.Float(snapshot.CountdownRemaining)} timer={Du02LogFormat.Float(snapshot.TimerSeconds)} inputLocked={snapshot.InputLocked} goal={snapshot.GoalReached} strokeCount={snapshot.StrokeCount} ink={Du02LogFormat.Float(snapshot.AvailableInk)} samplingSeq={snapshot.SamplingSequence} fixedDeltaTime={Du02LogFormat.Float(snapshot.FixedDeltaTime)} depthError={Du02LogFormat.Float(depthError)}");
            return snapshot;
        }

        public Du02ResetSnapshot CaptureSnapshot(Du02TaskId taskId)
        {
            return new Du02ResetSnapshot(
                taskId,
                playerMotor.transform.position,
                playerMotor.transform.rotation,
                playerMotor.Velocity,
                playerMotor.IsGrounded,
                handMarker.localPosition,
                handMarker.localRotation,
                targetCamera.transform.position,
                targetCamera.transform.rotation,
                targetCamera.fieldOfView,
                Time.fixedDeltaTime,
                handMarker.localScale,
                taskState.Phase,
                taskState.CountdownRemaining,
                taskState.TimerSeconds,
                taskState.InputLocked,
                taskState.GoalReached,
                taskState.StrokeCount,
                taskState.AvailableInk,
                samplingSeam.Sequence,
                playerMotor.AngularVelocity);
        }
    }
}
