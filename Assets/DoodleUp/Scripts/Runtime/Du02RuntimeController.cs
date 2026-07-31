using DoodleUp.Core;
using DoodleUp.Input;
using DoodleUp.Physics;
using UnityEngine;

namespace DoodleUp.Runtime
{
    [DefaultExecutionOrder(0)]
    public sealed class Du02RuntimeController : MonoBehaviour
    {
        [SerializeField] private Du02InputReader inputReader;
        [SerializeField] private Du02PlayerMotor playerMotor;
        [SerializeField] private Transform handMarker;
        [SerializeField] private Du02ResetCoordinator resetCoordinator;
        [SerializeField] private Du02TaskState taskState;

        public Du02TaskId ActiveTask { get; private set; } = Du02TaskId.T1Horizontal;

        public void SelectLaneForProbe(Du02TaskId taskId)
        {
            ActiveTask = taskId;
            resetCoordinator.ResetToLane(ActiveTask, "LANE_SELECT");
        }

        public void ResetCurrentLaneForProbe()
        {
            resetCoordinator.ResetToLane(ActiveTask, "R_KEY");
        }

        public void Configure(
            Du02InputReader reader,
            Du02PlayerMotor motor,
            Transform marker,
            Du02ResetCoordinator coordinator,
            Du02TaskState state)
        {
            inputReader = reader;
            playerMotor = motor;
            handMarker = marker;
            resetCoordinator = coordinator;
            taskState = state;
        }

        private void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = Du02Profile.FixedDeltaTime;
            resetCoordinator.ResetToLane(ActiveTask, "SCENE_START");
        }

        private void Update()
        {
            taskState.Tick(Time.unscaledDeltaTime);
            var input = inputReader.Current;
            if (input.LaneSelection is >= 1 and <= 3)
            {
                SelectLaneForProbe((Du02TaskId)input.LaneSelection);
                return;
            }

            if (input.ResetPressed)
            {
                ResetCurrentLaneForProbe();
                return;
            }

            if (taskState.InputLocked)
            {
                playerMotor.SetInput(0f, false);
            }
            else
            {
                playerMotor.SetInput(input.Horizontal, input.JumpPressed);
            }
            ValidateDepth();
        }

        private void ValidateDepth()
        {
            var spawn = Du02CourseDefinition.Get(ActiveTask).SpawnPosition;
            var playerError = Mathf.Abs(playerMotor.transform.position.z - spawn.z);
            var handError = Mathf.Abs(handMarker.position.z - spawn.z);
            var depthError = Mathf.Max(playerError, handError);
            if (depthError > Du02Profile.DepthTolerance)
            {
                Debug.LogError($"[DU02_DEPTH_DRIFT] task={ActiveTask} playerError={Du02LogFormat.Float(playerError)} handError={Du02LogFormat.Float(handError)} tolerance={Du02LogFormat.Float(Du02Profile.DepthTolerance)}");
            }
        }
    }
}
