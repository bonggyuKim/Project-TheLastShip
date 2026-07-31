using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DoodleUp.Core;
using DoodleUp.Physics;
using UnityEngine;

namespace DoodleUp.Runtime
{
    public sealed class Du02RuntimeProbeRunner : MonoBehaviour
    {
        [SerializeField] private Du02RuntimeFrameProbe frameProbe;
        [SerializeField] private Du02CandidateSamplingSeam samplingSeam;
        [SerializeField] private Du02ResetCoordinator resetCoordinator;
        [SerializeField] private Du02TaskState taskState;
        [SerializeField] private Du02PlayerMotor playerMotor;
        [SerializeField] private Du02RuntimeController runtimeController;
        [SerializeField] private Transform handMarker;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Du03ARuntimeProbeRunner du03AProbeRunner;
        [SerializeField] private Du03BCRuntimeProbeRunner du03BCProbeRunner;

        private readonly List<string> rawRows = new();

        public static string RawPath => Path.Combine(Application.persistentDataPath, "DU02_Runtime_Raw.csv");
        public static string SummaryPath => Path.Combine(Application.persistentDataPath, "DU02_Runtime_Summary.txt");

        public void Configure(
            Du02RuntimeFrameProbe probe,
            Du02CandidateSamplingSeam seam,
            Du02ResetCoordinator reset,
            Du02TaskState state,
            Du02PlayerMotor motor,
            Du02RuntimeController controller,
            Transform marker,
            Camera cameraComponent,
            Du03ARuntimeProbeRunner du03AProbe = null,
            Du03BCRuntimeProbeRunner du03BCProbe = null)
        {
            frameProbe = probe;
            samplingSeam = seam;
            resetCoordinator = reset;
            taskState = state;
            playerMotor = motor;
            runtimeController = controller;
            handMarker = marker;
            targetCamera = cameraComponent;
            du03AProbeRunner = du03AProbe;
            du03BCProbeRunner = du03BCProbe;
        }

        private IEnumerator Start()
        {
            if (!Application.isBatchMode) yield break;

            rawRows.Add("record_type,requested_fps,observed_frames,observed_samples,duplicate_frames,missing_frames,elapsed_seconds,reset_generation,task,reset_path,baseline_hash,before_hash,after_hash,before_differs,after_equal,baseline_rotation,before_rotation,after_rotation,baseline_angular_velocity,before_angular_velocity,after_angular_velocity,baseline_phase,before_phase,after_phase,rotation_restored,angular_velocity_restored,phase_restored");
            yield return null;

            if (du03AProbeRunner != null)
            {
                yield return new WaitUntil(() => File.Exists(Du03ARuntimeProbeRunner.RawPath));
            }
            if (du03BCProbeRunner != null)
            {
                yield return new WaitUntil(() => du03BCProbeRunner.IsComplete && File.Exists(Du03BCRuntimeProbeRunner.RawPath));
            }

            foreach (var fps in new[] { 30, 60, 144 })
            {
                yield return RunSamplingProbe(fps, 10f);
            }

            foreach (Du02TaskId taskId in Enum.GetValues(typeof(Du02TaskId)))
            {
                yield return RunResetProbe(taskId, "R_KEY");
                yield return RunResetProbe(taskId, "LANE_SELECT");
            }

            yield return RunTaskStateProbe(Du02TaskId.T1Horizontal, false, false, true);
            yield return RunTaskStateProbe(Du02TaskId.T2Rising, false, false, true);
            yield return RunTaskStateProbe(Du02TaskId.T3Bridge, true, false, false);
            yield return RunTaskStateProbe(Du02TaskId.T3Bridge, true, true, true);

            File.WriteAllLines(RawPath, rawRows);
            var summary = AggregateRaw(rawRows);
            File.WriteAllText(SummaryPath, summary);
            Debug.Log($"[DU02_RUNTIME_PROBE_COMPLETE] raw={RawPath} summary={SummaryPath} result=PASS");
            Application.Quit(0);
        }

        private IEnumerator RunSamplingProbe(int requestedFps, float durationSeconds)
        {
            Application.targetFrameRate = requestedFps;
            QualitySettings.vSyncCount = 0;

            // Align both counters after every component has completed LateUpdate for the
            // preceding frame. The first observed frame is counted by both probes.
            yield return new WaitForEndOfFrame();
            frameProbe.ResetCounters();
            samplingSeam.ResetCounter(handMarker.position, Vector3.forward);
            var startTime = Time.realtimeSinceStartupAsDouble;

            while (Time.realtimeSinceStartupAsDouble - startTime < durationSeconds)
            {
                yield return null;
            }

            var elapsed = Time.realtimeSinceStartupAsDouble - startTime;
            var frames = frameProbe.ObservedFrameCount;
            var samples = samplingSeam.Sequence;
            if (frames != samples || frameProbe.DuplicateFrames != 0 || frameProbe.MissingFrames != 0)
                throw new InvalidOperationException($"Runtime sample mismatch fps={requestedFps} frames={frames} samples={samples} duplicate={frameProbe.DuplicateFrames} missing={frameProbe.MissingFrames}");

            rawRows.Add(FormattableString.Invariant($"sampling,{requestedFps},{frames},{samples},{frameProbe.DuplicateFrames},{frameProbe.MissingFrames},{elapsed:F6},{resetCoordinator.Generation},") + string.Join(",", new string[19]));
            Debug.Log($"[DU02_RUNTIME_SAMPLE] requested_fps={requestedFps} observed_frames={frames} observed_samples={samples} duplicate={frameProbe.DuplicateFrames} missing={frameProbe.MissingFrames} elapsed={elapsed.ToString("F6", CultureInfo.InvariantCulture)} reset_generation={resetCoordinator.Generation} result=PASS");
        }

        private IEnumerator RunResetProbe(Du02TaskId taskId, string path)
        {
            runtimeController.SelectLaneForProbe(taskId);
            var baseline = resetCoordinator.CaptureSnapshot(taskId);
            var baselineHash = StableHash(baseline);
            PerturbAll();
            var before = resetCoordinator.CaptureSnapshot(taskId);
            var beforeHash = StableHash(before);
            var beforeDiffers = !baseline.Equals(before) && baselineHash != beforeHash
                && before.PlayerRotation != Quaternion.identity
                && before.AngularVelocity != Vector3.zero
                && before.Phase == Du02ScaffoldPhase.ProbePerturbed;
            if (!beforeDiffers)
                throw new InvalidOperationException($"Reset perturbation missing task={taskId} path={path} baseline={baselineHash} before={beforeHash} rotation={before.PlayerRotation} angularVelocity={before.AngularVelocity} phase={before.Phase}");

            if (path == "R_KEY") runtimeController.ResetCurrentLaneForProbe();
            else runtimeController.SelectLaneForProbe(taskId);

            var after = resetCoordinator.CaptureSnapshot(taskId);
            var afterHash = StableHash(after);
            var rotationRestored = after.PlayerRotation == Quaternion.identity && after.PlayerRotation == baseline.PlayerRotation;
            var angularVelocityRestored = after.AngularVelocity == Vector3.zero && after.AngularVelocity == baseline.AngularVelocity;
            var phaseRestored = after.Phase == Du02ScaffoldPhase.Idle && after.Phase == baseline.Phase;
            var afterEqual = baseline.Equals(after) && baselineHash == afterHash
                && rotationRestored && angularVelocityRestored && phaseRestored;
            if (!afterEqual) throw new InvalidOperationException($"Reset mismatch task={taskId} path={path} baseline={baselineHash} before={beforeHash} after={afterHash}");

            rawRows.Add(FormattableString.Invariant($"reset,,,,,,,{resetCoordinator.Generation},{taskId},{path},{baselineHash},{beforeHash},{afterHash},{beforeDiffers},{afterEqual},{CsvQuaternion(baseline.PlayerRotation)},{CsvQuaternion(before.PlayerRotation)},{CsvQuaternion(after.PlayerRotation)},{CsvVector(baseline.AngularVelocity)},{CsvVector(before.AngularVelocity)},{CsvVector(after.AngularVelocity)},{baseline.Phase},{before.Phase},{after.Phase},{rotationRestored},{angularVelocityRestored},{phaseRestored}"));
            Debug.Log($"[DU02_RUNTIME_RESET] task={taskId} path={path} generation={resetCoordinator.Generation} baselineHash={baselineHash} beforeHash={beforeHash} afterHash={afterHash} beforeDiffers={beforeDiffers} afterEqual={afterEqual} baselineRotation={Du02LogFormat.Quaternion(baseline.PlayerRotation)} beforeRotation={Du02LogFormat.Quaternion(before.PlayerRotation)} afterRotation={Du02LogFormat.Quaternion(after.PlayerRotation)} baselineAngularVelocity={Du02LogFormat.Vector(baseline.AngularVelocity)} beforeAngularVelocity={Du02LogFormat.Vector(before.AngularVelocity)} afterAngularVelocity={Du02LogFormat.Vector(after.AngularVelocity)} baselinePhase={baseline.Phase} beforePhase={before.Phase} afterPhase={after.Phase} rotationRestored={rotationRestored} angularVelocityRestored={angularVelocityRestored} phaseRestored={phaseRestored} result=PASS");
            yield return null;
        }

        private IEnumerator RunTaskStateProbe(Du02TaskId taskId, bool startBand, bool goalBand, bool expectSuccess)
        {
            runtimeController.SelectLaneForProbe(taskId);
            if (!taskState.InputLocked || taskState.CountdownRemaining != Du02TaskState.CountdownDuration || taskState.TimerSeconds != 0f)
                throw new InvalidOperationException($"Task reset contract mismatch task={taskId}");

            taskState.Tick(Du02TaskState.CountdownDuration);
            if (taskState.InputLocked || taskState.TimerSeconds != 0f)
                throw new InvalidOperationException($"GO transition mismatch task={taskId} locked={taskState.InputLocked} timer={taskState.TimerSeconds}");

            taskState.NotifyCommittedStrokeContact(startBand, goalBand);
            taskState.SetInsideGoal(true);
            taskState.Tick(Du02TaskState.GoalHoldDuration);
            if (taskState.GoalReached != expectSuccess)
                throw new InvalidOperationException($"Success seam mismatch task={taskId} startBand={startBand} goalBand={goalBand} expected={expectSuccess} actual={taskState.GoalReached}");

            Debug.Log($"[DU02_RUNTIME_TASK_STATE] task={taskId} startBand={startBand} goalBand={goalBand} goalHold={taskState.GoalHoldSeconds.ToString("F6", CultureInfo.InvariantCulture)} goalReached={taskState.GoalReached} expected={expectSuccess} result=PASS");
            yield return null;
        }

        private void PerturbAll()
        {
            playerMotor.SetProbeState(
                playerMotor.transform.position + new Vector3(1.25f, 2.50f, 0f),
                Quaternion.Euler(17f, 23f, 31f),
                new Vector3(2f, 3f, 0f),
                new Vector3(1.5f, -2.25f, 3.75f));
            handMarker.localPosition = new Vector3(9f, 8f, 7f);
            handMarker.localRotation = Quaternion.Euler(20f, 30f, 40f);
            handMarker.localScale = new Vector3(2f, 3f, 4f);
            targetCamera.transform.SetPositionAndRotation(new Vector3(7f, 8f, 9f), Quaternion.Euler(1f, 2f, 3f));
            targetCamera.fieldOfView = 42f;
            taskState.PerturbForResetProbe();
            samplingSeam.ResetCounter(new Vector3(4f, 5f, 6f), Vector3.left);
        }

        private static string StableHash(Du02ResetSnapshot snapshot)
        {
            var text = FormattableString.Invariant($"{snapshot.TaskId}|{snapshot.PlayerPosition:F6}|{snapshot.PlayerRotation:F6}|{snapshot.Velocity:F6}|{snapshot.AngularVelocity:F6}|{snapshot.Grounded}|{snapshot.HandLocalPosition:F6}|{snapshot.HandLocalRotation:F6}|{snapshot.HandLocalScale:F6}|{snapshot.CameraPosition:F6}|{snapshot.CameraRotation:F6}|{snapshot.CameraFov:F6}|{snapshot.FixedDeltaTime:F6}|{snapshot.Phase}|{snapshot.CountdownRemaining:F6}|{snapshot.TimerSeconds:F6}|{snapshot.InputLocked}|{snapshot.GoalReached}|{snapshot.StrokeCount}|{snapshot.AvailableInk:F6}|{snapshot.SamplingSequence}");
            unchecked
            {
                uint hash = 2166136261;
                foreach (var character in text)
                {
                    hash ^= character;
                    hash *= 16777619;
                }
                return hash.ToString("x8");
            }
        }

        private static string CsvVector(Vector3 value)
        {
            return FormattableString.Invariant($"{value.x:F6}|{value.y:F6}|{value.z:F6}");
        }

        private static string CsvQuaternion(Quaternion value)
        {
            return FormattableString.Invariant($"{value.x:F6}|{value.y:F6}|{value.z:F6}|{value.w:F6}");
        }

        private static string AggregateRaw(IEnumerable<string> rows)
        {
            var lines = new List<string> { "DU-02 RUNTIME VERIFICATION", $"unity={Application.unityVersion}" };
            foreach (var row in rows)
            {
                if (row.StartsWith("sampling", StringComparison.Ordinal)) lines.Add(row);
                if (row.StartsWith("reset", StringComparison.Ordinal)) lines.Add(row);
            }
            lines.Add("result=PASS");
            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }
    }
}
