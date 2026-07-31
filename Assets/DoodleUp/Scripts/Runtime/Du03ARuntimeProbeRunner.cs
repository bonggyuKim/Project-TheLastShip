using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using DoodleUp.Stroke;
using UnityEngine;

namespace DoodleUp.Runtime
{
    [DefaultExecutionOrder(200)]
    public sealed class Du03ARuntimeProbeRunner : MonoBehaviour
    {
        private const string Header = "scenario,mode,state_before,state_after,candidate_valid,accepted_appended,reason,candidate_points_before,candidate_points_after,candidate_length_before,candidate_length_after,candidate_available_before,candidate_available_after,candidate_drawing_before,candidate_drawing_after,candidate_pending_before,candidate_pending_after,final_available,final_drawing_reserved,final_pending_reserved,final_committed_charged,final_ledger_total,final_accepted_points,final_pending_count,final_committed_count,terminal_state,charged_length,simplified_points,pending_colliders,segment_count,collider_count,degenerate_skipped,capsule_direction,capsule_radius,capsule_height,expected_capsule_height,capsule_center_zero,capsule_non_trigger,root_scale_one,child_scale_one,midpoint_aligned,y_axis_aligned,max_shared_endpoint_gap,render_frame,late_update_sequence,sample_phase,candidate_count_this_frame,event_order,atomic_unchanged,result";

        [SerializeField] private Du03AStrokeDriver strokeDriver;
        [SerializeField] private Du02RuntimeController runtimeController;
        [SerializeField] private Du03ADeterministicIntentSource intentSource;

        private readonly List<string> rows = new();
        private Du03ALateUpdateEvidence lastLateUpdate;
        private bool lateUpdateObserved;

        public static string RawPath => Path.Combine(Application.persistentDataPath, "DU03A_Runtime_Raw.csv");

        public void Configure(
            Du03AStrokeDriver driver,
            Du02RuntimeController controller,
            Du03ADeterministicIntentSource source)
        {
            strokeDriver = driver;
            runtimeController = controller;
            intentSource = source;
        }

        private IEnumerator Start()
        {
            if (!Application.isBatchMode) yield break;
            if (File.Exists(RawPath)) File.Delete(RawPath);
            rows.Add(Header);
            strokeDriver.LateUpdateProcessed += OnLateUpdateProcessed;
            yield return null;

            RunShortCancel();
            yield return RunPendingConfirmLateUpdate();
            RunPendingCancel();
            RunReachAtomicity();
            RunInkAtomicity();
            RunResetRegression();
            RunInvalidReleaseUnderMinimum();
            RunInvalidReleaseOverMinimum();
            RunExplicitDrawingCancel();
            RunPendingNewDrawReject();
            RunOutOfStateConfirm();
            RunConfirmReleaseSameFrame();
            RunModeParity();

            strokeDriver.LateUpdateProcessed -= OnLateUpdateProcessed;
            File.WriteAllLines(RawPath, rows);
            Debug.Log($"[DU03A_RUNTIME_PROBE_COMPLETE] raw={RawPath} scenarios={rows.Count - 1} result=PASS");
        }

        private void RunShortCancel()
        {
            var session = Begin();
            var before = Snapshot(session);
            var candidate = session.SubmitCandidate(new Vector3(0.16f, 0f, 0f));
            var candidateAfter = Snapshot(session);
            session.Release();
            RequireCanonicalCancelled(session, "short cancel");
            AddRow("short_cancel", Du03AStrokeMode.Trajectory, before, candidateAfter, session, candidate, default, 0, 0, "DIRECT", true);
        }

        private IEnumerator RunPendingConfirmLateUpdate()
        {
            strokeDriver.ResetSession();
            intentSource.Clear();
            var session = strokeDriver.Session;

            intentSource.Enqueue(new Du03ADrawIntent(true, false, false, false, false, default));
            yield return WaitForIntent();
            var before = Snapshot(session);

            intentSource.Enqueue(new Du03ADrawIntent(false, false, false, false, true, session.PlaneOrigin + Vector3.right * 0.16f));
            yield return WaitForIntent();

            intentSource.Enqueue(new Du03ADrawIntent(false, true, false, false, true, session.PlaneOrigin + Vector3.right * 0.24f));
            yield return WaitForIntent();
            var releaseEvidence = lastLateUpdate;
            var candidateAfter = Snapshot(session);
            var pendingColliders = strokeDriver.CommittedColliderCount;
            Require(session.State == Du03AStrokeSessionState.Pending
                && session.PendingCount == 1
                && session.LiveCommittedCount == 0
                && pendingColliders == 0
                && strokeDriver.PreviewVisible
                && releaseEvidence.CandidateCountThisFrame == 1
                && releaseEvidence.EventOrder == "CANDIDATE>RELEASE"
                && releaseEvidence.CandidateResult.AcceptedAppended, "pending LateUpdate release");

            intentSource.Enqueue(new Du03ADrawIntent(false, false, true, false, false, default));
            yield return WaitForIntent();
            var committed = session.CommittedStrokes[0];
            var geometry = InspectGeometry(strokeDriver.LastGeometryResult, committed.SimplifiedPoints.Count);
            Require(session.State == Du03AStrokeSessionState.Idle
                && session.LastTerminalState == Du03AStrokeSessionState.Committed
                && session.PendingCount == 0
                && session.LiveCommittedCount == 1
                && !strokeDriver.PreviewVisible
                && Approximately(committed.ChargedLength, 0.24f)
                && geometry.Valid, "explicit confirm geometry");
            AddRow("pending_confirm", Du03AStrokeMode.Trajectory, before, candidateAfter, session, releaseEvidence.CandidateResult, geometry, pendingColliders, committed.SimplifiedPoints.Count, releaseEvidence, true);
        }

        private void RunPendingCancel()
        {
            var session = Begin();
            var before = Snapshot(session);
            var candidate = session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            var candidateAfter = Snapshot(session);
            session.Release();
            session.Cancel();
            RequireCanonicalCancelled(session, "pending cancel");
            AddRow("pending_cancel", Du03AStrokeMode.Trajectory, before, candidateAfter, session, candidate, default, 0, 0, "DIRECT", true);
        }

        private void RunReachAtomicity()
        {
            var session = Begin();
            session.SubmitCandidate(new Vector3(0.16f, 0f, 0f));
            var before = Snapshot(session);
            var candidate = session.SubmitCandidate(new Vector3(2f, 0f, 0f));
            var after = Snapshot(session);
            var unchanged = SnapshotEqual(before, after);
            Require(!candidate.CandidateValid && !candidate.AcceptedAppended
                && candidate.Reason == Du03ACandidateReason.ReachInvalid && unchanged, "reach atomicity");
            AddRow("reach_atomic", Du03AStrokeMode.Trajectory, before, after, session, candidate, default, 0, 0, "DIRECT", unchanged);
        }

        private void RunInkAtomicity()
        {
            var session = new Du03AStrokeSession();
            for (var strokeIndex = 0; strokeIndex < 4; strokeIndex++)
            {
                Require(session.TryBegin(Vector3.zero, Vector3.forward, "probe-owner", Du03AStrokeMode.Trajectory), "ink drain begin");
                var drain = session.SubmitCandidate(new Vector3(1.20f, 0f, 0f));
                Require(drain.AcceptedAppended && Approximately(session.AcceptedLength, 1.20f), "ink drain candidate");
                session.Release();
                Require(session.Confirm() != null, "ink drain confirm");
            }
            Require(session.TryBegin(Vector3.zero, Vector3.forward, "probe-owner", Du03AStrokeMode.Trajectory), "ink begin");
            var before = Snapshot(session);
            var candidate = session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            var after = Snapshot(session);
            var unchanged = SnapshotEqual(before, after);
            Require(!candidate.CandidateValid && !candidate.AcceptedAppended
                && candidate.Reason == Du03ACandidateReason.InkInvalid
                && candidate.RequiredInk > before.Available && unchanged, "ink atomicity");
            AddRow("ink_atomic", Du03AStrokeMode.Trajectory, before, after, session, candidate, default, 0, 0, "DIRECT", unchanged);
        }

        private void RunResetRegression()
        {
            strokeDriver.ResetSession();
            var session = strokeDriver.Session;
            Require(session.TryBegin(Vector3.zero, Vector3.forward, "probe-owner", Du03AStrokeMode.Trajectory), "reset begin");
            var before = Snapshot(session);
            var candidate = session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            var candidateAfter = Snapshot(session);
            session.Release();
            Require(session.State == Du03AStrokeSessionState.Pending, "reset precondition");
            runtimeController.ResetCurrentLaneForProbe();
            RequireCanonicalReset(session, "R reset regression");
            AddRow("r_reset_pending", Du03AStrokeMode.Trajectory, before, candidateAfter, session, candidate, default, 0, 0, "DIRECT", true);
        }

        private void RunInvalidReleaseUnderMinimum()
        {
            var session = Begin();
            session.SubmitCandidate(new Vector3(0.16f, 0f, 0f));
            var before = Snapshot(session);
            var invalid = session.SubmitCandidate(new Vector3(2f, 0f, 0f));
            var after = Snapshot(session);
            session.Release();
            RequireCanonicalCancelled(session, "invalid release under minimum");
            AddRow("invalid_release_under_min", Du03AStrokeMode.Trajectory, before, after, session, invalid, default, 0, 0, "CANDIDATE>RELEASE", SnapshotEqual(before, after));
        }

        private void RunInvalidReleaseOverMinimum()
        {
            var session = Begin();
            session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            var before = Snapshot(session);
            var invalid = session.SubmitCandidate(new Vector3(2f, 0f, 0f));
            var after = Snapshot(session);
            session.Release();
            Require(session.State == Du03AStrokeSessionState.Pending
                && session.PendingReservedLength >= Du03AStrokeProfile.MinimumStrokeLength, "invalid release over minimum");
            AddRow("invalid_release_over_min", Du03AStrokeMode.Trajectory, before, after, session, invalid, default, 0, 0, "CANDIDATE>RELEASE", SnapshotEqual(before, after));
        }

        private void RunExplicitDrawingCancel()
        {
            var session = Begin();
            var before = Snapshot(session);
            var candidate = session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            var after = Snapshot(session);
            session.Cancel();
            RequireCanonicalCancelled(session, "explicit Drawing cancel");
            AddRow("drawing_cancel", Du03AStrokeMode.Trajectory, before, after, session, candidate, default, 0, 0, "CANCEL", true);
        }

        private void RunPendingNewDrawReject()
        {
            var session = Begin();
            var candidate = session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            session.Release();
            var before = Snapshot(session);
            var rejected = session.TryBegin(Vector3.zero, Vector3.forward, "other", Du03AStrokeMode.Aim);
            var after = Snapshot(session);
            Require(!rejected && session.State == Du03AStrokeSessionState.Pending && SnapshotEqual(before, after), "Pending new Draw reject");
            AddRow("pending_new_draw_reject", Du03AStrokeMode.Trajectory, before, after, session, candidate, default, 0, 0, "PRESS_REJECTED", true);
        }

        private void RunOutOfStateConfirm()
        {
            strokeDriver.ResetSession();
            var session = strokeDriver.Session;
            var before = Snapshot(session);
            strokeDriver.ProcessIntent(new Du03ADrawIntent(false, false, true, false, false, default));
            var after = Snapshot(session);
            Require(session.State == Du03AStrokeSessionState.Idle
                && session.LiveCommittedCount == 0
                && strokeDriver.CommittedColliderCount == 0
                && SnapshotEqual(before, after), "out-of-state Confirm");
            AddRow("out_of_state_confirm", Du03AStrokeMode.Trajectory, before, after, session, default, default, 0, 0, "CONFIRM_REJECTED", true);
        }

        private void RunConfirmReleaseSameFrame()
        {
            strokeDriver.ResetSession();
            var session = strokeDriver.Session;
            strokeDriver.ProcessIntent(new Du03ADrawIntent(true, false, false, false, false, default));
            strokeDriver.ProcessIntent(new Du03ADrawIntent(false, false, false, false, true, session.PlaneOrigin + Vector3.right * 0.16f));
            var before = Snapshot(session);
            var candidate = strokeDriver.ProcessIntent(new Du03ADrawIntent(false, true, true, false, true, session.PlaneOrigin + Vector3.right * 0.24f));
            var after = Snapshot(session);
            Require(candidate.AcceptedAppended && session.State == Du03AStrokeSessionState.Pending
                && session.LiveCommittedCount == 0 && strokeDriver.CommittedColliderCount == 0, "Confirm release same frame");
            AddRow("confirm_release_same_frame", Du03AStrokeMode.Trajectory, before, after, session, candidate, default, 0, 0, "CONFIRM_REJECTED>CANDIDATE>RELEASE", true);
        }

        private void RunModeParity()
        {
            var aim = RunModeSequence(Du03AStrokeMode.Aim);
            var trajectory = RunModeSequence(Du03AStrokeMode.Trajectory);
            Require(aim.State == trajectory.State
                && aim.Points == trajectory.Points
                && Approximately(aim.Available, trajectory.Available)
                && Approximately(aim.Drawing, trajectory.Drawing)
                && Approximately(aim.Pending, trajectory.Pending)
                && Approximately(aim.Committed, trajectory.Committed)
                && Approximately(aim.Charged, trajectory.Charged), "Aim Trajectory parity");
            AddRow("mode_parity_aim", Du03AStrokeMode.Aim, aim.Before, aim.CandidateAfter, aim.Session, aim.Candidate, default, 0, aim.SimplifiedPoints, "IDENTICAL_SEQUENCE", true);
            AddRow("mode_parity_trajectory", Du03AStrokeMode.Trajectory, trajectory.Before, trajectory.CandidateAfter, trajectory.Session, trajectory.Candidate, default, 0, trajectory.SimplifiedPoints, "IDENTICAL_SEQUENCE", true);
        }

        private static ModeResult RunModeSequence(Du03AStrokeMode mode)
        {
            var session = new Du03AStrokeSession();
            Require(session.TryBegin(Vector3.zero, Vector3.forward, "parity-owner", mode), "mode begin");
            var before = Snapshot(session);
            session.SubmitCandidate(new Vector3(0.08f, 0.01f, 0f));
            session.SubmitCandidate(new Vector3(0.16f, 0f, 0f));
            var candidate = session.SubmitCandidate(new Vector3(0.24f, 0.01f, 0f));
            var after = Snapshot(session);
            session.Release();
            var simplified = session.PendingStroke.SimplifiedPoints.Count;
            var stroke = session.Confirm();
            return new ModeResult(session, before, after, candidate, simplified, stroke.ChargedLength);
        }

        private IEnumerator WaitForIntent()
        {
            lateUpdateObserved = false;
            var initialReadCount = intentSource.ReadCount;
            yield return new WaitUntil(() => intentSource.ReadCount > initialReadCount && lateUpdateObserved);
        }

        private void OnLateUpdateProcessed(Du03ALateUpdateEvidence evidence)
        {
            lastLateUpdate = evidence;
            lateUpdateObserved = true;
        }

        private static Du03AStrokeSession Begin()
        {
            var session = new Du03AStrokeSession();
            Require(session.TryBegin(Vector3.zero, Vector3.forward, "probe-owner", Du03AStrokeMode.Trajectory), "begin");
            return session;
        }

        private static SessionSnapshot Snapshot(Du03AStrokeSession session)
        {
            return new SessionSnapshot(session.State, session.AcceptedPoints.Count, session.AcceptedLength,
                session.AvailableInk, session.DrawingReservedLength, session.PendingReservedLength,
                session.CommittedChargedLength);
        }

        private static bool SnapshotEqual(SessionSnapshot left, SessionSnapshot right)
        {
            return left.State == right.State
                && left.Points == right.Points
                && Approximately(left.Length, right.Length)
                && Approximately(left.Available, right.Available)
                && Approximately(left.Drawing, right.Drawing)
                && Approximately(left.Pending, right.Pending)
                && Approximately(left.Committed, right.Committed);
        }

        private void AddRow(
            string scenario,
            Du03AStrokeMode mode,
            SessionSnapshot before,
            SessionSnapshot candidateAfter,
            Du03AStrokeSession session,
            Du03ACandidateResult candidate,
            GeometrySnapshot geometry,
            int pendingColliders,
            int simplifiedPoints,
            string eventOrder,
            bool atomicUnchanged)
        {
            AddRow(scenario, mode, before, candidateAfter, session, candidate, geometry, pendingColliders,
                simplifiedPoints, new Du03ALateUpdateEvidence(0, 0,
                    eventOrder.Contains("CANDIDATE") ? 1 : 0, eventOrder, candidate), atomicUnchanged);
        }

        private void AddRow(
            string scenario,
            Du03AStrokeMode mode,
            SessionSnapshot before,
            SessionSnapshot candidateAfter,
            Du03AStrokeSession session,
            Du03ACandidateResult candidate,
            GeometrySnapshot geometry,
            int pendingColliders,
            int simplifiedPoints,
            Du03ALateUpdateEvidence evidence,
            bool atomicUnchanged)
        {
            var finalAvailable = session.AvailableInk;
            var finalDrawing = session.DrawingReservedLength;
            var finalPending = session.PendingReservedLength;
            var finalCommitted = session.CommittedChargedLength;
            var finalTotal = finalAvailable + finalDrawing + finalPending + finalCommitted;
            rows.Add(FormattableString.Invariant(
                $"{scenario},{mode},{before.State},{session.State},{candidate.CandidateValid},{candidate.AcceptedAppended},{candidate.Reason},{before.Points},{candidateAfter.Points},{before.Length:F6},{candidateAfter.Length:F6},{before.Available:F6},{candidateAfter.Available:F6},{before.Drawing:F6},{candidateAfter.Drawing:F6},{before.Pending:F6},{candidateAfter.Pending:F6},{finalAvailable:F6},{finalDrawing:F6},{finalPending:F6},{finalCommitted:F6},{finalTotal:F6},{session.AcceptedPoints.Count},{session.PendingCount},{session.LiveCommittedCount},{session.LastTerminalState},{(session.LiveCommittedCount > 0 ? session.CommittedStrokes[^1].ChargedLength : 0f):F6},{(geometry.Valid ? geometry.SimplifiedPoints : simplifiedPoints)},{pendingColliders},{geometry.SegmentCount},{geometry.ColliderCount},{geometry.DegenerateSkipped},{geometry.Direction},{geometry.Radius:F6},{geometry.Height:F6},{geometry.ExpectedHeight:F6},{geometry.CenterZero},{geometry.NonTrigger},{geometry.RootScaleOne},{geometry.ChildScaleOne},{geometry.MidpointAligned},{geometry.YAxisAligned},{geometry.MaximumGap:F9},{evidence.RenderFrame},{evidence.LateUpdateSequence},{evidence.SamplePhase},{evidence.CandidateCountThisFrame},{evidence.EventOrder},{atomicUnchanged},PASS"));
            Debug.Log($"[DU03A_RUNTIME] scenario={scenario} mode={mode} state={session.State} finalAvailable={finalAvailable.ToString("F6", CultureInfo.InvariantCulture)} finalDrawing={finalDrawing.ToString("F6", CultureInfo.InvariantCulture)} finalPending={finalPending.ToString("F6", CultureInfo.InvariantCulture)} finalCommitted={finalCommitted.ToString("F6", CultureInfo.InvariantCulture)} ledgerTotal={finalTotal.ToString("F6", CultureInfo.InvariantCulture)} renderFrame={evidence.RenderFrame} sequence={evidence.LateUpdateSequence} phase={evidence.SamplePhase} candidateCount={evidence.CandidateCountThisFrame} order={evidence.EventOrder} result=PASS");
        }

        private static GeometrySnapshot InspectGeometry(Du03AStrokeGeometryResult result, int simplifiedPoints)
        {
            Require(result.Root != null && result.GeometryValid, "geometry result");
            var colliders = result.Root.GetComponentsInChildren<CapsuleCollider>(true);
            var valid = colliders.Length == result.ColliderCount && colliders.Length > 0;
            var direction = -1;
            var radius = 0f;
            var height = 0f;
            var expectedHeight = 0f;
            var centerZero = true;
            var nonTrigger = true;
            var childScaleOne = true;
            var midpointAligned = true;
            var yAxisAligned = true;
            foreach (var collider in colliders)
            {
                var segmentLength = collider.height - Du03AStrokeGeometryProfile.Diameter;
                var start = collider.transform.position - collider.transform.up * (segmentLength * 0.5f);
                var end = collider.transform.position + collider.transform.up * (segmentLength * 0.5f);
                direction = collider.direction;
                radius = collider.radius;
                height = collider.height;
                expectedHeight = Vector3.Distance(start, end) + Du03AStrokeGeometryProfile.Diameter;
                centerZero &= collider.center == Vector3.zero;
                nonTrigger &= !collider.isTrigger;
                childScaleOne &= collider.transform.localScale == Vector3.one && collider.transform.lossyScale == Vector3.one;
                midpointAligned &= Vector3.Distance(collider.transform.position, (start + end) * 0.5f) <= 0.000001f;
                yAxisAligned &= Vector3.Dot(collider.transform.up.normalized, (end - start).normalized) >= 0.999999f;
            }
            var rootScaleOne = result.Root.transform.localScale == Vector3.one && result.Root.transform.lossyScale == Vector3.one;
            valid &= direction == 1
                && Approximately(radius, Du03AStrokeGeometryProfile.Radius)
                && Approximately(height, expectedHeight)
                && centerZero && nonTrigger && rootScaleOne && childScaleOne
                && midpointAligned && yAxisAligned && result.MaximumSharedEndpointGap <= 0.000001f;
            return new GeometrySnapshot(valid, result.SegmentCount, result.ColliderCount, result.DegenerateSkipped,
                direction, radius, height, expectedHeight, centerZero, nonTrigger, rootScaleOne,
                childScaleOne, midpointAligned, yAxisAligned, result.MaximumSharedEndpointGap, simplifiedPoints);
        }

        private static void RequireCanonicalCancelled(Du03AStrokeSession session, string contract)
        {
            Require(session.State == Du03AStrokeSessionState.Idle
                && session.LastTerminalState == Du03AStrokeSessionState.Cancelled
                && session.PendingCount == 0 && session.LiveCommittedCount == 0
                && session.AcceptedPoints.Count == 0
                && Approximately(session.AvailableInk, Du03AStrokeProfile.InitialInk)
                && Approximately(session.DrawingReservedLength, 0f)
                && Approximately(session.PendingReservedLength, 0f), contract);
        }

        private static void RequireCanonicalReset(Du03AStrokeSession session, string contract)
        {
            Require(session.State == Du03AStrokeSessionState.Idle
                && session.LastTerminalState == Du03AStrokeSessionState.Idle
                && session.PendingCount == 0 && session.LiveCommittedCount == 0
                && session.AcceptedPoints.Count == 0
                && Approximately(session.AvailableInk, Du03AStrokeProfile.InitialInk)
                && Approximately(session.DrawingReservedLength, 0f)
                && Approximately(session.PendingReservedLength, 0f), contract);
        }

        private static bool Approximately(float left, float right) => Mathf.Abs(left - right) <= 0.0001f;

        private static void Require(bool condition, string contract)
        {
            if (!condition) throw new InvalidOperationException($"DU-03A runtime contract failed: {contract}");
        }

        private readonly struct SessionSnapshot
        {
            public readonly Du03AStrokeSessionState State;
            public readonly int Points;
            public readonly float Length;
            public readonly float Available;
            public readonly float Drawing;
            public readonly float Pending;
            public readonly float Committed;

            public SessionSnapshot(Du03AStrokeSessionState state, int points, float length,
                float available, float drawing, float pending, float committed)
            {
                State = state;
                Points = points;
                Length = length;
                Available = available;
                Drawing = drawing;
                Pending = pending;
                Committed = committed;
            }
        }

        private readonly struct GeometrySnapshot
        {
            public readonly bool Valid;
            public readonly int SegmentCount;
            public readonly int ColliderCount;
            public readonly int DegenerateSkipped;
            public readonly int Direction;
            public readonly float Radius;
            public readonly float Height;
            public readonly float ExpectedHeight;
            public readonly bool CenterZero;
            public readonly bool NonTrigger;
            public readonly bool RootScaleOne;
            public readonly bool ChildScaleOne;
            public readonly bool MidpointAligned;
            public readonly bool YAxisAligned;
            public readonly float MaximumGap;
            public readonly int SimplifiedPoints;

            public GeometrySnapshot(bool valid, int segmentCount, int colliderCount, int degenerateSkipped,
                int direction, float radius, float height, float expectedHeight, bool centerZero,
                bool nonTrigger, bool rootScaleOne, bool childScaleOne, bool midpointAligned,
                bool yAxisAligned, float maximumGap, int simplifiedPoints)
            {
                Valid = valid;
                SegmentCount = segmentCount;
                ColliderCount = colliderCount;
                DegenerateSkipped = degenerateSkipped;
                Direction = direction;
                Radius = radius;
                Height = height;
                ExpectedHeight = expectedHeight;
                CenterZero = centerZero;
                NonTrigger = nonTrigger;
                RootScaleOne = rootScaleOne;
                ChildScaleOne = childScaleOne;
                MidpointAligned = midpointAligned;
                YAxisAligned = yAxisAligned;
                MaximumGap = maximumGap;
                SimplifiedPoints = simplifiedPoints;
            }
        }

        private readonly struct ModeResult
        {
            public readonly Du03AStrokeSession Session;
            public readonly SessionSnapshot Before;
            public readonly SessionSnapshot CandidateAfter;
            public readonly Du03ACandidateResult Candidate;
            public readonly int SimplifiedPoints;
            public readonly Du03AStrokeSessionState State;
            public readonly int Points;
            public readonly float Available;
            public readonly float Drawing;
            public readonly float Pending;
            public readonly float Committed;
            public readonly float Charged;

            public ModeResult(Du03AStrokeSession session, SessionSnapshot before,
                SessionSnapshot candidateAfter, Du03ACandidateResult candidate,
                int simplifiedPoints, float charged)
            {
                Session = session;
                Before = before;
                CandidateAfter = candidateAfter;
                Candidate = candidate;
                SimplifiedPoints = simplifiedPoints;
                State = session.State;
                Points = simplifiedPoints;
                Available = session.AvailableInk;
                Drawing = session.DrawingReservedLength;
                Pending = session.PendingReservedLength;
                Committed = session.CommittedChargedLength;
                Charged = charged;
            }
        }
    }
}
