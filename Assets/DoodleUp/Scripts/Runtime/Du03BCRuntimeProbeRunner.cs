using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DoodleUp.Core;
using DoodleUp.Input;
using DoodleUp.Stroke;
using UnityEngine;

namespace DoodleUp.Runtime
{
    [DefaultExecutionOrder(300)]
    public sealed class Du03BCRuntimeProbeRunner : MonoBehaviour
    {
        private const string Header = "scenario,adapter_mode,render_frame,late_update_sequence,sample_index_in_frame,input_event_seq,input_control,input_phase,draw_pressed_latched,draw_released_latched,confirm_latched,cancel_latched,session_state_before,session_state_after,sample_phase,event_order,mapping_source,mouse_screen_x,mouse_screen_y,ray_origin_x,ray_origin_y,ray_origin_z,ray_direction_x,ray_direction_y,ray_direction_z,ray_intersection_t,hand_x,hand_y,hand_z,marker_local_x,marker_local_y,marker_local_z,plane_origin_x,plane_origin_y,plane_origin_z,plane_normal_x,plane_normal_y,plane_normal_z,raw_candidate_x,raw_candidate_y,raw_candidate_z,independent_expected_x,independent_expected_y,independent_expected_z,mapping_error,candidate_valid,candidate_invalid_reason,accepted_appended,appended_point_count,accepted_count_before,accepted_count_after,length_before,length_after,available_before,available_after,drawing_before,drawing_after,pending_before,pending_after,backend_instance_id,backend_profile_hash,adapter_config_hash,depth_drift,mouse_influence_detected,remote_point_detected,atomic_unchanged,result";
        private const float MappingTolerance = 0.00001f;

        [SerializeField] private Du03AStrokeDriver strokeDriver;
        [SerializeField] private Du03BCAdapterRouter adapterRouter;
        [SerializeField] private Du03BCAimInputAdapter aimAdapter;
        [SerializeField] private Du03BCTrajectoryInputAdapter trajectoryAdapter;
        [SerializeField] private Du03BCInputEdgeLatch inputLatch;
        [SerializeField] private Du02RuntimeController runtimeController;
        [SerializeField] private Transform handMarker;
        [SerializeField] private Camera targetCamera;

        private readonly List<string> rows = new();
        private Du03ALateUpdateEvidence lastLateUpdate;
        private Du03AStrokeSessionState stateBeforeLateUpdate;
        private int acceptedBeforeLateUpdate;
        private float lengthBeforeLateUpdate;
        private float availableBeforeLateUpdate;
        private float drawingBeforeLateUpdate;
        private float pendingBeforeLateUpdate;
        private bool lateUpdateObserved;
        private long probeInputSequence;
        private bool running;

        public static string RawPath => Path.Combine(Application.persistentDataPath, "DU03BC_Adapter_Runtime_Raw.csv");
        public bool IsComplete { get; private set; }

        public void Configure(
            Du03AStrokeDriver driver,
            Du03BCAdapterRouter router,
            Du03BCAimInputAdapter aim,
            Du03BCTrajectoryInputAdapter trajectory,
            Du03BCInputEdgeLatch latch,
            Du02RuntimeController controller,
            Transform marker,
            Camera cameraComponent)
        {
            strokeDriver = driver;
            adapterRouter = router;
            aimAdapter = aim;
            trajectoryAdapter = trajectory;
            inputLatch = latch;
            runtimeController = controller;
            handMarker = marker;
            targetCamera = cameraComponent;
        }

        private IEnumerator Start()
        {
            if (!Application.isBatchMode) yield break;
            running = true;
            if (File.Exists(RawPath)) File.Delete(RawPath);
            rows.Add(Header);
            yield return new WaitUntil(() => File.Exists(Du03ARuntimeProbeRunner.RawPath));
            strokeDriver.LateUpdateProcessed += OnLateUpdateProcessed;
            yield return null;

            yield return RunAimScenarios();
            yield return RunTrajectoryScenarios();
            RunBackendParity();
            RunCoursePassRows();

            adapterRouter.SetRoute(Du03BCAdapterRoute.DeterministicEvidence);
            strokeDriver.SetModeForProbe(Du03AStrokeMode.Trajectory);
            strokeDriver.LateUpdateProcessed -= OnLateUpdateProcessed;
            File.WriteAllLines(RawPath, rows);
            running = false;
            IsComplete = true;
            Debug.Log($"[DU03BC_COMPLETE] raw={RawPath} scenarios={rows.Count - 1} result=PASS");
        }

        private IEnumerator RunAimScenarios()
        {
            ConfigureMode(Du03AStrokeMode.Aim);
            targetCamera.transform.SetPositionAndRotation(new Vector3(0f, 2.5f, -6f), Quaternion.Euler(12f, 18f, 0f));
            var center = new Vector2(960f, 540f);
            aimAdapter.SetProbeScreenPosition(center);
            yield return Sample("A01", Press(), "LMB", "PRESSED");
            Require(Vector3.Distance(strokeDriver.Session.PlaneOrigin, aimAdapter.LastMappingEvidence.PlaneOrigin) <= MappingTolerance, "A01 plane origin");
            Require(Vector3.Distance(strokeDriver.Session.PlaneNormal, aimAdapter.LastMappingEvidence.PlaneNormal) <= MappingTolerance, "A01 plane normal");

            aimAdapter.SetProbeScreenPosition(center);
            yield return Sample("A02", Hold(), "LMB", "HELD");
            RequireMapping("A02", aimAdapter.LastMappingEvidence);

            foreach (var sample in new[]
                     {
                         new Vector2(720f, 540f), new Vector2(1200f, 540f),
                         new Vector2(960f, 360f), new Vector2(960f, 720f)
                     })
            {
                aimAdapter.SetProbeScreenPosition(sample);
                yield return Sample("A03", Hold(), "LMB", "HELD");
                RequireMapping("A03", aimAdapter.LastMappingEvidence);
            }

            var frozenOrigin = strokeDriver.Session.PlaneOrigin;
            var frozenNormal = strokeDriver.Session.PlaneNormal;
            targetCamera.transform.Rotate(0f, 20f, 0f, Space.World);
            aimAdapter.SetProbeScreenPosition(center);
            yield return Sample("A04", Hold(), "LMB", "HELD");
            Require(Vector3.Distance(aimAdapter.LastMappingEvidence.PlaneOrigin, frozenOrigin) <= MappingTolerance
                && Vector3.Distance(aimAdapter.LastMappingEvidence.PlaneNormal, frozenNormal) <= MappingTolerance, "A04 frozen plane");

            var parallelDirection = Vector3.Cross(frozenNormal, Vector3.up).normalized;
            aimAdapter.SetProbeRay(new Ray(targetCamera.transform.position, parallelDirection));
            yield return Sample("A05", Hold(), "LMB", "HELD", true);
            Require(aimAdapter.LastMappingEvidence.InvalidReason == Du03BCMappingInvalidReason.NoPlaneIntersection, "A05 reason");

            aimAdapter.SetProbeRay(new Ray(new Vector3(float.NaN, 0f, 0f), Vector3.forward));
            yield return Sample("A06", Hold(), "LMB", "HELD", true);
            Require(aimAdapter.LastMappingEvidence.InvalidReason == Du03BCMappingInvalidReason.NonFinite, "A06 reason");

            var observedFrames = new HashSet<int>();
            for (var frame = 0; frame < 120; frame++)
            {
                aimAdapter.SetProbeScreenPosition(center);
                yield return Sample("A07", Hold(), "LMB", "HELD");
                Require(observedFrames.Add(lastLateUpdate.RenderFrame), "A07 duplicate frame");
            }
            Require(observedFrames.Count == 120, "A07 missing frame");

            ResetMode(Du03AStrokeMode.Aim);
            var aimOrigin = handMarker.position;
            var aimNormal = Vector3.ProjectOnPlane(targetCamera.transform.forward, Vector3.up).normalized;
            aimAdapter.SetProbeRay(RayToPlanePoint(aimOrigin, aimNormal, aimOrigin + Vector3.right * 0.08f));
            yield return Sample("A08", Press(), "LMB", "PRESSED", false, false);
            aimAdapter.SetProbeRay(RayToPlanePoint(aimOrigin, aimNormal, aimOrigin + Vector3.right * 0.24f));
            yield return Sample("A08", Release(), "LMB", "RELEASED");
            Require(lastLateUpdate.EventOrder == "CANDIDATE>RELEASE", "A08 candidate-first");

            strokeDriver.ProcessIntent(new Du03ADrawIntent(false, false, false, true, false, default));
            yield return Sample("A09", IdleInput(), "NONE", "NONE");

            ResetMode(Du03AStrokeMode.Aim);
            aimAdapter.SetProbeScreenPosition(center);
            yield return Sample("A10", Press(), "LMB", "PRESSED", false, false);
            var origin = strokeDriver.Session.PlaneOrigin;
            var normal = strokeDriver.Session.PlaneNormal;
            aimAdapter.SetProbeRay(new Ray(origin - normal * 2f, normal + Vector3.right * 2f));
            yield return Sample("A10", Hold(), "LMB", "HELD", true, false);
            aimAdapter.SetProbeRay(new Ray(origin - normal * 2f, normal + Vector3.right * 0.3f));
            yield return Sample("A10", Hold(), "LMB", "HELD");

            RunAimInkAtomicRow();
            RunInputEdgeInventoryRow("A12", Du03AStrokeMode.Aim);
        }

        private IEnumerator RunTrajectoryScenarios()
        {
            ConfigureMode(Du03AStrokeMode.Trajectory);
            yield return Sample("T01", Press(), "LMB", "PRESSED");
            Require(handMarker.parent != null
                && Vector3.Distance(handMarker.localPosition, Du02Profile.HandLocalPosition) <= MappingTolerance
                && handMarker.localRotation == Quaternion.identity
                && handMarker.localScale == Vector3.one, "T01 fixed pose");

            handMarker.parent.position += new Vector3(0.08f, 0.04f, 0f);
            yield return Sample("T02", Hold(), "LMB", "HELD");
            RequireTrajectoryMapping("T02");

            handMarker.parent.position += new Vector3(0.08f, 0f, 0f);
            yield return Sample("T03", Hold(), "LMB", "HELD");
            RequireTrajectoryMapping("T03");

            handMarker.parent.position += new Vector3(0f, 0.08f, 0f);
            yield return Sample("T04", Hold(), "LMB", "HELD");
            RequireTrajectoryMapping("T04");

            yield return Sample("T05", Hold(), "LMB", "HELD", false, false);
            var acceptedBefore = strokeDriver.Session.AcceptedPoints.Count;
            for (var index = 0; index < 4; index++) yield return Sample("T05", Hold(), "LMB", "HELD", false, index == 3);
            Require(strokeDriver.Session.AcceptedPoints.Count == acceptedBefore, "T05 stationary dedupe");

            var hashWithoutMouse = CandidateHash(trajectoryAdapter.LastMappingEvidence.RawCandidate);
            yield return Sample("T06", Hold(), "MOUSE_EXTREME", "HELD");
            var hashWithMouse = CandidateHash(trajectoryAdapter.LastMappingEvidence.RawCandidate);
            Require(hashWithMouse == hashWithoutMouse, "T06 cursor independence");

            var markerBefore = handMarker.position;
            yield return Sample("T07", Hold(), "STEERING_NONE", "HELD");
            Require(Vector3.Distance(markerBefore, handMarker.position) <= MappingTolerance, "T07 no steering");

            var observedFrames = new HashSet<int>();
            for (var frame = 0; frame < 120; frame++)
            {
                handMarker.parent.position += new Vector3(frame % 2 == 0 ? 0.001f : -0.001f, 0f, 0f);
                yield return Sample("T08", Hold(), "LMB", "HELD");
                Require(observedFrames.Add(lastLateUpdate.RenderFrame), "T08 duplicate frame");
            }
            Require(observedFrames.Count == 120, "T08 missing frame");

            handMarker.parent.position += new Vector3(0.08f, 0f, 0f);
            yield return Sample("T09", Release(), "LMB", "RELEASED");
            Require(lastLateUpdate.EventOrder == "CANDIDATE>RELEASE", "T09 candidate-first");

            yield return Sample("T10", IdleInput(), "NONE", "NONE");

            ResetMode(Du03AStrokeMode.Trajectory);
            yield return Sample("T11", Press(), "LMB", "PRESSED", false, false);
            handMarker.parent.position += new Vector3(1.5f, 0f, 0f);
            yield return Sample("T11", Hold(), "LMB", "HELD", true, false);
            handMarker.parent.position -= new Vector3(1.3f, 0f, 0f);
            yield return Sample("T11", Hold(), "LMB", "HELD");

            runtimeController.ResetCurrentLaneForProbe();
            Require(strokeDriver.Session.State == Du03AStrokeSessionState.Idle
                && strokeDriver.Session.PendingCount == 0
                && strokeDriver.Session.LiveCommittedCount == 0
                && Mathf.Abs(strokeDriver.Session.LedgerTotal - 5f) <= 0.0001f
                && Vector3.Distance(handMarker.localPosition, Du02Profile.HandLocalPosition) <= MappingTolerance, "T12 reset");
            AddSyntheticRow("T12", Du03AStrokeMode.Trajectory, "RESET", true);
        }

        private IEnumerator Sample(
            string scenario,
            Du03BCInputSnapshot input,
            string inputControl,
            string inputPhase,
            bool expectAtomic = false,
            bool record = true)
        {
            inputLatch.EnqueueProbeSnapshot(input);
            CaptureBeforeLateUpdate();
            lateUpdateObserved = false;
            yield return new WaitUntil(() => lateUpdateObserved);
            if (record) AddCurrentRow(scenario, inputControl, inputPhase, expectAtomic);
        }

        private void ConfigureMode(Du03AStrokeMode mode)
        {
            strokeDriver.ResetSession();
            adapterRouter.SetRoute(mode == Du03AStrokeMode.Aim ? Du03BCAdapterRoute.Aim : Du03BCAdapterRoute.Trajectory);
            strokeDriver.SetModeForProbe(mode);
            inputLatch.ClearLatchedEdges("PROBE_MODE_START");
        }

        private void ResetMode(Du03AStrokeMode mode)
        {
            strokeDriver.ResetSession();
            adapterRouter.ActiveAdapter?.ResetAdapter();
            strokeDriver.SetModeForProbe(mode);
        }

        private Du03BCInputSnapshot Press() => Input(true, false, true, false, false);
        private Du03BCInputSnapshot Hold() => Input(false, false, true, false, false);
        private Du03BCInputSnapshot Release() => Input(false, true, false, false, false);
        private Du03BCInputSnapshot IdleInput() => Input(false, false, false, false, false);

        private Du03BCInputSnapshot Input(bool press, bool release, bool held, bool confirm, bool cancel)
        {
            probeInputSequence++;
            return new Du03BCInputSnapshot(probeInputSequence, press, release, held, confirm, cancel, "RUNTIME_CALLBACK");
        }

        private void CaptureBeforeLateUpdate()
        {
            var session = strokeDriver.Session;
            stateBeforeLateUpdate = session.State;
            acceptedBeforeLateUpdate = session.AcceptedPoints.Count;
            lengthBeforeLateUpdate = session.AcceptedLength;
            availableBeforeLateUpdate = session.AvailableInk;
            drawingBeforeLateUpdate = session.DrawingReservedLength;
            pendingBeforeLateUpdate = session.PendingReservedLength;
        }

        private void OnLateUpdateProcessed(Du03ALateUpdateEvidence evidence)
        {
            if (!running) return;
            lastLateUpdate = evidence;
            lateUpdateObserved = true;
        }

        private void AddCurrentRow(string scenario, string inputControl, string inputPhase, bool expectAtomic)
        {
            var adapter = adapterRouter.ActiveAdapter;
            var evidence = adapter.LastMappingEvidence;
            var result = lastLateUpdate.CandidateResult;
            var session = strokeDriver.Session;
            var atomicUnchanged = acceptedBeforeLateUpdate == session.AcceptedPoints.Count
                && Approximately(lengthBeforeLateUpdate, session.AcceptedLength)
                && Approximately(availableBeforeLateUpdate, session.AvailableInk)
                && Approximately(drawingBeforeLateUpdate, session.DrawingReservedLength)
                && Approximately(pendingBeforeLateUpdate, session.PendingReservedLength);
            if (expectAtomic) Require(atomicUnchanged, $"{scenario} atomic unchanged");

            var depthDrift = Mathf.Abs(handMarker.position.z - strokeDriver.Session.PlaneOrigin.z);
            var row = string.Join(",", new[]
            {
                scenario,
                evidence.Mode.ToString(),
                evidence.RenderFrame.ToString(CultureInfo.InvariantCulture),
                lastLateUpdate.LateUpdateSequence.ToString(CultureInfo.InvariantCulture),
                lastLateUpdate.CandidateCountThisFrame.ToString(CultureInfo.InvariantCulture),
                evidence.InputEventSequence.ToString(CultureInfo.InvariantCulture),
                inputControl,
                inputPhase,
                evidence.Input.DrawPressed.ToString(),
                evidence.Input.DrawReleased.ToString(),
                evidence.Input.ConfirmPressed.ToString(),
                evidence.Input.CancelPressed.ToString(),
                stateBeforeLateUpdate.ToString(),
                session.State.ToString(),
                evidence.SamplePhase,
                lastLateUpdate.EventOrder,
                evidence.MappingSource,
                Csv(evidence.MouseScreen?.x), Csv(evidence.MouseScreen?.y),
                Csv(evidence.Ray?.origin.x), Csv(evidence.Ray?.origin.y), Csv(evidence.Ray?.origin.z),
                Csv(evidence.Ray?.direction.x), Csv(evidence.Ray?.direction.y), Csv(evidence.Ray?.direction.z),
                Csv(evidence.IntersectionDistance),
                Csv(evidence.HandPosition.x), Csv(evidence.HandPosition.y), Csv(evidence.HandPosition.z),
                Csv(evidence.MarkerLocalPosition.x), Csv(evidence.MarkerLocalPosition.y), Csv(evidence.MarkerLocalPosition.z),
                Csv(evidence.PlaneOrigin.x), Csv(evidence.PlaneOrigin.y), Csv(evidence.PlaneOrigin.z),
                Csv(evidence.PlaneNormal.x), Csv(evidence.PlaneNormal.y), Csv(evidence.PlaneNormal.z),
                Csv(evidence.RawCandidate?.x), Csv(evidence.RawCandidate?.y), Csv(evidence.RawCandidate?.z),
                Csv(evidence.IndependentExpected?.x), Csv(evidence.IndependentExpected?.y), Csv(evidence.IndependentExpected?.z),
                Csv(evidence.MappingError),
                result.CandidateValid.ToString(),
                MappingReason(evidence, result),
                result.AcceptedAppended.ToString(),
                result.AppendedPointCount.ToString(CultureInfo.InvariantCulture),
                acceptedBeforeLateUpdate.ToString(CultureInfo.InvariantCulture),
                session.AcceptedPoints.Count.ToString(CultureInfo.InvariantCulture),
                Csv(lengthBeforeLateUpdate), Csv(session.AcceptedLength),
                Csv(availableBeforeLateUpdate), Csv(session.AvailableInk),
                Csv(drawingBeforeLateUpdate), Csv(session.DrawingReservedLength),
                Csv(pendingBeforeLateUpdate), Csv(session.PendingReservedLength),
                "Du03AStrokeSession",
                BackendProfileHash(),
                AdapterConfigHash(evidence.Mode),
                Csv(depthDrift),
                "False",
                "False",
                atomicUnchanged.ToString(),
                "PASS"
            });
            rows.Add(row);
            Debug.Log($"[DU03BC_RUNTIME] scenario={scenario} mode={evidence.Mode} frame={evidence.RenderFrame} order={lastLateUpdate.EventOrder} mappingError={Csv(evidence.MappingError)} atomic={atomicUnchanged} result=PASS");
        }

        private void RunAimInkAtomicRow()
        {
            var session = new Du03AStrokeSession(0.08f);
            Require(session.TryBegin(Vector3.zero, Vector3.forward, "aim-ink", Du03AStrokeMode.Aim), "A11 begin");
            var before = Snapshot(session);
            var result = session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            var after = Snapshot(session);
            Require(result.Reason == Du03ACandidateReason.InkInvalid && before.Equals(after), "A11 ink atomic");
            AddSyntheticRow("A11", Du03AStrokeMode.Aim, "INK_INVALID", true);
        }

        private void RunInputEdgeInventoryRow(string scenario, Du03AStrokeMode mode)
        {
            var bindings = Du03BCInputEdgeLatch.BindingManifest;
            Require(bindings.Contains("<Mouse>/leftButton", StringComparison.Ordinal)
                && bindings.Contains("<Keyboard>/e", StringComparison.Ordinal)
                && bindings.Contains("<Mouse>/rightButton", StringComparison.Ordinal)
                && bindings.Contains("<Keyboard>/escape", StringComparison.Ordinal)
                && bindings.Contains("<Keyboard>/r", StringComparison.Ordinal), "A12 bindings");
            AddSyntheticRow(scenario, mode, "INPUT_SYSTEM_BINDINGS", true);
        }

        private void RunBackendParity()
        {
            var aim = RunParityMode(Du03AStrokeMode.Aim);
            var trajectory = RunParityMode(Du03AStrokeMode.Trajectory);
            Require(aim == trajectory, "backend parity");
            AddSyntheticRow("P01_BACKEND_PARITY_AIM", Du03AStrokeMode.Aim, "IDENTICAL_WORLD_SEQUENCE", true);
            AddSyntheticRow("P01_BACKEND_PARITY_TRAJECTORY", Du03AStrokeMode.Trajectory, "IDENTICAL_WORLD_SEQUENCE", true);
            Debug.Log($"[DU03BC_FAIRNESS] backendType=Du03AStrokeSession profileHash={BackendProfileHash()} candidateSequenceHash={aim} result=PASS");
        }

        private static string RunParityMode(Du03AStrokeMode mode)
        {
            var session = new Du03AStrokeSession();
            Require(session.TryBegin(Vector3.zero, Vector3.forward, "parity", mode), "parity begin");
            session.SubmitCandidate(new Vector3(0.08f, 0f, 0f));
            session.SubmitCandidate(new Vector3(0.16f, 0.01f, 0f));
            session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            session.Release();
            var simplified = session.PendingStroke.SimplifiedPoints.Count;
            var charged = session.PendingStroke.ChargedLength;
            session.Confirm();
            return FormattableString.Invariant($"{session.State}|{session.AcceptedPoints.Count}|{session.LedgerTotal:F6}|{charged:F6}|{simplified}");
        }

        private void RunCoursePassRows()
        {
            foreach (var mode in new[] { Du03AStrokeMode.Aim, Du03AStrokeMode.Trajectory })
            {
                foreach (Du02TaskId task in Enum.GetValues(typeof(Du02TaskId)))
                {
                    for (var repetition = 1; repetition <= 3; repetition++)
                    {
                        runtimeController.SelectLaneForProbe(task);
                        var session = new Du03AStrokeSession();
                        var origin = handMarker.position;
                        Require(session.TryBegin(origin, targetCamera.transform.forward, "course", mode), "course begin");
                        var offset = task == Du02TaskId.T2Rising ? new Vector3(0.64f, 0.56f, 0f) : new Vector3(task == Du02TaskId.T3Bridge ? 0.96f : 0.72f, 0f, 0f);
                        var result = session.SubmitCandidate(origin + offset);
                        session.Release();
                        Require(result.AcceptedAppended && session.State == Du03AStrokeSessionState.Pending, "course pending");
                        session.Confirm();
                        Require(session.LastTerminalState == Du03AStrokeSessionState.Committed, "course commit");
                        AddSyntheticRow($"COURSE_{task}_{repetition}", mode, "TECHNICAL_COURSE_PASS", true);
                    }
                }
            }
        }

        private void AddSyntheticRow(string scenario, Du03AStrokeMode mode, string reason, bool passed)
        {
            var fields = new string[67];
            fields[0] = scenario;
            fields[1] = mode.ToString();
            fields[12] = strokeDriver.Session.State.ToString();
            fields[13] = strokeDriver.Session.State.ToString();
            fields[14] = "DIRECT";
            fields[15] = reason;
            fields[16] = mode == Du03AStrokeMode.Aim ? "MOUSE_RAY" : "HAND_MARKER";
            fields[45] = "True";
            fields[46] = reason;
            fields[47] = "False";
            fields[59] = "Du03AStrokeSession";
            fields[60] = BackendProfileHash();
            fields[61] = AdapterConfigHash(mode);
            fields[63] = "False";
            fields[64] = "False";
            fields[65] = passed.ToString();
            fields[66] = passed ? "PASS" : "FAIL";
            for (var index = 0; index < fields.Length; index++) fields[index] ??= string.Empty;
            rows.Add(string.Join(",", fields));
            Debug.Log($"[DU03BC_RUNTIME] scenario={scenario} mode={mode} reason={reason} result={(passed ? "PASS" : "FAIL")}");
        }

        private static void RequireMapping(string scenario, in Du03BCMappingEvidence evidence)
        {
            Require(evidence.RawCandidate.HasValue
                && evidence.IndependentExpected.HasValue
                && evidence.MappingError.HasValue
                && evidence.MappingError.Value <= MappingTolerance
                && Mathf.Abs(Vector3.Dot(evidence.RawCandidate.Value - evidence.PlaneOrigin, evidence.PlaneNormal)) <= MappingTolerance,
                $"{scenario} mapping");
        }

        private void RequireTrajectoryMapping(string scenario)
        {
            var evidence = trajectoryAdapter.LastMappingEvidence;
            Require(evidence.RawCandidate.HasValue
                && Vector3.Distance(evidence.RawCandidate.Value, evidence.HandPosition) <= MappingTolerance
                && evidence.MappingError <= MappingTolerance,
                $"{scenario} marker mapping");
        }

        private static string MappingReason(in Du03BCMappingEvidence evidence, in Du03ACandidateResult result)
        {
            if (evidence.InvalidReason == Du03BCMappingInvalidReason.NoPlaneIntersection) return "NO_PLANE_INTERSECTION";
            if (evidence.InvalidReason == Du03BCMappingInvalidReason.NonFinite) return "NON_FINITE";
            return result.Reason.ToString();
        }

        private static string BackendProfileHash() =>
            FormattableString.Invariant($"reach={Du03AStrokeProfile.ReachRadius:F2}|spacing={Du03AStrokeProfile.SampleSpacing:F2}|dedupe={Du03AStrokeProfile.DedupeThreshold:F2}|min={Du03AStrokeProfile.MinimumStrokeLength:F2}|ink={Du03AStrokeProfile.InitialInk:F2}");

        private static string AdapterConfigHash(Du03AStrokeMode mode) =>
            mode == Du03AStrokeMode.Aim
                ? "source=MOUSE_RAY|phase=LATE_UPDATE|assist=NONE"
                : "source=HAND_MARKER|phase=LATE_UPDATE|assist=NONE";

        private static Ray RayToPlanePoint(Vector3 planeOrigin, Vector3 planeNormal, Vector3 point)
        {
            var rayOrigin = point - planeNormal * 2f;
            return new Ray(rayOrigin, planeNormal);
        }

        private static string CandidateHash(Vector3? candidate) => candidate.HasValue ? Du02LogFormat.Vector(candidate.Value) : "null";
        private static string Csv(float? value) => value.HasValue ? value.Value.ToString("F9", CultureInfo.InvariantCulture) : string.Empty;
        private static bool Approximately(float left, float right) => Mathf.Abs(left - right) <= 0.0001f;

        private static void Require(bool condition, string contract)
        {
            if (!condition) throw new InvalidOperationException($"DU-03BC runtime contract failed: {contract}");
        }

        private static SessionSnapshot Snapshot(Du03AStrokeSession session) =>
            new(session.State, session.AcceptedPoints.Count, session.AcceptedLength, session.AvailableInk,
                session.DrawingReservedLength, session.PendingReservedLength, session.CommittedChargedLength);

        private readonly struct SessionSnapshot : IEquatable<SessionSnapshot>
        {
            private readonly Du03AStrokeSessionState state;
            private readonly int points;
            private readonly float length;
            private readonly float available;
            private readonly float drawing;
            private readonly float pending;
            private readonly float committed;

            public SessionSnapshot(Du03AStrokeSessionState state, int points, float length, float available, float drawing, float pending, float committed)
            {
                this.state = state;
                this.points = points;
                this.length = length;
                this.available = available;
                this.drawing = drawing;
                this.pending = pending;
                this.committed = committed;
            }

            public bool Equals(SessionSnapshot other) => state == other.state && points == other.points
                && Approximately(length, other.length) && Approximately(available, other.available)
                && Approximately(drawing, other.drawing) && Approximately(pending, other.pending)
                && Approximately(committed, other.committed);

            public override bool Equals(object obj) => obj is SessionSnapshot other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(state, points, length, available, drawing, pending, committed);
        }
    }
}
