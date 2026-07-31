using DoodleUp.Stroke;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class Du03AStrokeSessionTests
    {
        [Test]
        public void ShortReleaseCancelsAndRefundsReserve()
        {
            var session = Begin();
            var candidate = session.SubmitCandidate(new Vector3(0.16f, 0f, 0f));

            Assert.That(candidate.AcceptedAppended, Is.True);
            Assert.That(session.AcceptedLength, Is.EqualTo(0.16f).Within(0.0001f));
            Assert.That(session.Release(), Is.True);
            Assert.That(session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(session.LastTerminalState, Is.EqualTo(Du03AStrokeSessionState.Cancelled));
            Assert.That(session.AvailableInk, Is.EqualTo(Du03AStrokeProfile.InitialInk).Within(0.0001f));
            Assert.That(session.DrawingReservedLength, Is.Zero);
            Assert.That(session.PendingReservedLength, Is.Zero);
            Assert.That(session.PendingCount, Is.Zero);
        }

        [Test]
        public void ReleaseCreatesColliderFreePendingAndConfirmCreatesImmutableStrokeData()
        {
            var session = Begin();
            session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));

            Assert.That(session.Release(), Is.True);
            Assert.That(session.State, Is.EqualTo(Du03AStrokeSessionState.Pending));
            Assert.That(session.PendingCount, Is.EqualTo(1));
            Assert.That(session.LiveCommittedCount, Is.Zero);
            Assert.That(session.DrawingReservedLength, Is.Zero);
            Assert.That(session.PendingReservedLength, Is.EqualTo(0.24f).Within(0.0001f));

            var stroke = session.Confirm();
            Assert.That(stroke, Is.Not.Null);
            Assert.That(session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(session.LastTerminalState, Is.EqualTo(Du03AStrokeSessionState.Committed));
            Assert.That(session.PendingCount, Is.Zero);
            Assert.That(session.LiveCommittedCount, Is.EqualTo(1));
            Assert.That(stroke.ChargedLength, Is.EqualTo(0.24f).Within(0.0001f));
            Assert.That(stroke.OwnerId, Is.EqualTo("owner-a"));
            Assert.That(stroke.Mode, Is.EqualTo(Du03AStrokeMode.Trajectory));
            Assert.That(stroke.SimplifiedPoints.Count, Is.EqualTo(2));
        }

        [Test]
        public void PendingCancelRefundsAndDoesNotCommit()
        {
            var session = Begin();
            session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            session.Release();

            Assert.That(session.Cancel(), Is.True);
            Assert.That(session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(session.LastTerminalState, Is.EqualTo(Du03AStrokeSessionState.Cancelled));
            Assert.That(session.LiveCommittedCount, Is.Zero);
            Assert.That(session.PendingCount, Is.Zero);
            Assert.That(session.AvailableInk, Is.EqualTo(Du03AStrokeProfile.InitialInk).Within(0.0001f));
        }

        [Test]
        public void ReachRejectIsAtomic()
        {
            var session = Begin();
            session.SubmitCandidate(new Vector3(0.16f, 0f, 0f));
            var beforeLength = session.AcceptedLength;
            var beforeInk = session.AvailableInk;
            var beforeCount = session.AcceptedPoints.Count;

            var result = session.SubmitCandidate(new Vector3(2f, 0f, 0f));

            Assert.That(result.CandidateValid, Is.False);
            Assert.That(result.AcceptedAppended, Is.False);
            Assert.That(result.Reason, Is.EqualTo(Du03ACandidateReason.ReachInvalid));
            Assert.That(session.AcceptedLength, Is.EqualTo(beforeLength));
            Assert.That(session.AvailableInk, Is.EqualTo(beforeInk));
            Assert.That(session.AcceptedPoints.Count, Is.EqualTo(beforeCount));
        }

        [Test]
        public void InkRejectWithMultipleProspectivePointsIsAllOrNothing()
        {
            var session = new Du03AStrokeSession(0.15f);
            Assert.That(session.TryBegin(Vector3.zero, Vector3.forward, "owner-a", Du03AStrokeMode.Aim), Is.True);

            var result = session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));

            Assert.That(result.RequiredInk, Is.EqualTo(0.24f).Within(0.0001f));
            Assert.That(result.CandidateValid, Is.False);
            Assert.That(result.AcceptedAppended, Is.False);
            Assert.That(result.Reason, Is.EqualTo(Du03ACandidateReason.InkInvalid));
            Assert.That(session.AcceptedPoints.Count, Is.EqualTo(1));
            Assert.That(session.AcceptedLength, Is.Zero);
            Assert.That(session.DrawingReservedLength, Is.Zero);
            Assert.That(session.AvailableInk, Is.EqualTo(0.15f));
        }

        [Test]
        public void SpacingAndDedupeAreValidWithoutAppend()
        {
            var session = Begin();

            var dedupe = session.SubmitCandidate(new Vector3(0.01f, 0f, 0f));
            var spacing = session.SubmitCandidate(new Vector3(0.04f, 0f, 0f));

            Assert.That(dedupe.CandidateValid, Is.True);
            Assert.That(dedupe.AcceptedAppended, Is.False);
            Assert.That(dedupe.Reason, Is.EqualTo(Du03ACandidateReason.Dedupe));
            Assert.That(spacing.CandidateValid, Is.True);
            Assert.That(spacing.AcceptedAppended, Is.False);
            Assert.That(spacing.Reason, Is.EqualTo(Du03ACandidateReason.SpacingNotReached));
            Assert.That(session.AcceptedLength, Is.Zero);
            Assert.That(session.AvailableInk, Is.EqualTo(Du03AStrokeProfile.InitialInk));
        }

        [Test]
        public void ProjectionUsesSnapshottedYawNormalPlane()
        {
            var session = new Du03AStrokeSession();
            Assert.That(session.TryBegin(new Vector3(1f, 2f, 3f), new Vector3(0f, -0.5f, 1f), "owner-a", Du03AStrokeMode.Aim), Is.True);

            var result = session.SubmitCandidate(new Vector3(1.16f, 2f, 9f));

            Assert.That(session.PlaneOrigin, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(session.PlaneNormal, Is.EqualTo(Vector3.forward));
            Assert.That(result.ProjectedCandidate.z, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(result.AcceptedAppended, Is.True);
        }

        [Test]
        public void ChargedLengthIsNotRecomputedFromSimplifiedGeometry()
        {
            var session = Begin();
            session.SubmitCandidate(new Vector3(0.08f, 0.01f, 0f));
            session.SubmitCandidate(new Vector3(0.16f, 0f, 0f));
            session.SubmitCandidate(new Vector3(0.24f, 0.01f, 0f));
            session.Release();

            var stroke = session.Confirm();
            var simplifiedLength = 0f;
            for (var index = 1; index < stroke.SimplifiedPoints.Count; index++)
                simplifiedLength += Vector3.Distance(stroke.SimplifiedPoints[index - 1], stroke.SimplifiedPoints[index]);

            Assert.That(stroke.ChargedLength, Is.EqualTo(0.24f).Within(0.0001f));
            Assert.That(stroke.SimplifiedPoints.Count, Is.EqualTo(2));
            Assert.That(Mathf.Abs(simplifiedLength - stroke.ChargedLength), Is.GreaterThan(0.0001f));
        }

        [Test]
        public void ResetClearsLiveAndPendingAndReturnsIdle()
        {
            var session = Begin();
            session.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            session.Release();
            session.Confirm();

            session.Reset();

            Assert.That(session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(session.LiveCommittedCount, Is.Zero);
            Assert.That(session.PendingCount, Is.Zero);
            Assert.That(session.DrawingReservedLength, Is.Zero);
            Assert.That(session.PendingReservedLength, Is.Zero);
            Assert.That(session.AvailableInk, Is.EqualTo(Du03AStrokeProfile.InitialInk));
        }

        [Test]
        public void ConfirmCreatesGoldenCapsuleOnlyAfterPending()
        {
            var root = new GameObject("test-root");
            var hand = new GameObject("test-hand");
            var cameraObject = new GameObject("test-camera");
            var camera = cameraObject.AddComponent<Camera>();
            var driver = root.AddComponent<Du03AStrokeDriver>();
            driver.Configure(hand.transform, camera, null, "owner-a", Du03AStrokeMode.Trajectory, null, root.transform);

            driver.ProcessIntent(new Du03ADrawIntent(true, false, false, false, false, default));
            driver.ProcessIntent(new Du03ADrawIntent(false, true, false, false, true, new Vector3(0.24f, 0f, 0f)));

            Assert.That(driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Pending));
            Assert.That(driver.CommittedColliderCount, Is.Zero);
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);

            driver.ProcessIntent(new Du03ADrawIntent(false, false, true, false, false, default));

            Assert.That(driver.Session.LastTerminalState, Is.EqualTo(Du03AStrokeSessionState.Committed));
            Assert.That(driver.LastGeometryResult.SegmentCount, Is.EqualTo(1));
            Assert.That(driver.LastGeometryResult.ColliderCount, Is.EqualTo(1));
            Assert.That(driver.LastGeometryResult.DegenerateSkipped, Is.Zero);
            Assert.That(driver.LastGeometryResult.MaximumSharedEndpointGap, Is.Zero.Within(0.000001f));
            var capsule = root.GetComponentInChildren<CapsuleCollider>(true);
            Assert.That(capsule.direction, Is.EqualTo(1));
            Assert.That(capsule.radius, Is.EqualTo(0.14f).Within(0.000001f));
            Assert.That(capsule.height, Is.EqualTo(0.52f).Within(0.0001f));
            Assert.That(capsule.center, Is.EqualTo(Vector3.zero));
            Assert.That(capsule.isTrigger, Is.False);
            Assert.That(Vector3.Distance(capsule.transform.position, new Vector3(0.12f, 0f, 0f)), Is.LessThanOrEqualTo(0.000001f));
            Assert.That(Vector3.Dot(capsule.transform.up, Vector3.right), Is.EqualTo(1f).Within(0.000001f));
            Assert.That(capsule.transform.localScale, Is.EqualTo(Vector3.one));

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(hand);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void OutOfStateConfirmDoesNotConsumeSameFrameRelease()
        {
            var root = new GameObject("test-root");
            var hand = new GameObject("test-hand");
            var cameraObject = new GameObject("test-camera");
            var camera = cameraObject.AddComponent<Camera>();
            var driver = root.AddComponent<Du03AStrokeDriver>();
            driver.Configure(hand.transform, camera, null, "owner-a", Du03AStrokeMode.Trajectory);
            driver.ProcessIntent(new Du03ADrawIntent(true, false, false, false, false, default));
            driver.ProcessIntent(new Du03ADrawIntent(false, false, false, false, true, new Vector3(0.16f, 0f, 0f)));

            var result = driver.ProcessIntent(new Du03ADrawIntent(false, true, true, false, true, new Vector3(0.24f, 0f, 0f)));

            Assert.That(result.AcceptedAppended, Is.True);
            Assert.That(driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Pending));
            Assert.That(driver.Session.PendingReservedLength, Is.EqualTo(0.24f).Within(0.0001f));
            Assert.That(driver.Session.LiveCommittedCount, Is.Zero);

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(hand);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void InvalidReleaseUsesLastAcceptedLengthForTerminalBranch()
        {
            var under = Begin();
            under.SubmitCandidate(new Vector3(0.16f, 0f, 0f));
            var underInvalid = under.SubmitCandidate(new Vector3(2f, 0f, 0f));
            under.Release();

            var over = Begin();
            over.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            var overInvalid = over.SubmitCandidate(new Vector3(2f, 0f, 0f));
            over.Release();

            Assert.That(underInvalid.CandidateValid, Is.False);
            Assert.That(under.LastTerminalState, Is.EqualTo(Du03AStrokeSessionState.Cancelled));
            Assert.That(under.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(overInvalid.CandidateValid, Is.False);
            Assert.That(over.State, Is.EqualTo(Du03AStrokeSessionState.Pending));
            Assert.That(over.PendingReservedLength, Is.EqualTo(0.24f).Within(0.0001f));
        }

        [Test]
        public void DrawingCancelAndPendingDrawRejectPreserveLedger()
        {
            var drawing = Begin();
            drawing.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            Assert.That(drawing.Cancel(), Is.True);
            Assert.That(drawing.LedgerTotal, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(drawing.AcceptedPoints, Is.Empty);

            var pending = Begin();
            pending.SubmitCandidate(new Vector3(0.24f, 0f, 0f));
            pending.Release();
            var ledger = pending.LedgerTotal;
            Assert.That(pending.TryBegin(Vector3.zero, Vector3.forward, "owner-b", Du03AStrokeMode.Aim), Is.False);
            Assert.That(pending.State, Is.EqualTo(Du03AStrokeSessionState.Pending));
            Assert.That(pending.LedgerTotal, Is.EqualTo(ledger));
        }

        [Test]
        public void AimAndTrajectoryProduceIdenticalBackendResults()
        {
            var aim = RunMode(Du03AStrokeMode.Aim);
            var trajectory = RunMode(Du03AStrokeMode.Trajectory);

            Assert.That(aim.ChargedLength, Is.EqualTo(trajectory.ChargedLength));
            Assert.That(aim.SimplifiedPoints.Count, Is.EqualTo(trajectory.SimplifiedPoints.Count));
            for (var index = 0; index < aim.SimplifiedPoints.Count; index++)
                Assert.That(aim.SimplifiedPoints[index], Is.EqualTo(trajectory.SimplifiedPoints[index]));
        }

        private static Du03AStrokeData RunMode(Du03AStrokeMode mode)
        {
            var session = new Du03AStrokeSession();
            Assert.That(session.TryBegin(Vector3.zero, Vector3.forward, "owner-a", mode), Is.True);
            session.SubmitCandidate(new Vector3(0.08f, 0.01f, 0f));
            session.SubmitCandidate(new Vector3(0.16f, 0f, 0f));
            session.SubmitCandidate(new Vector3(0.24f, 0.01f, 0f));
            session.Release();
            return session.Confirm();
        }

        private static Du03AStrokeSession Begin()
        {
            var session = new Du03AStrokeSession();
            Assert.That(session.TryBegin(Vector3.zero, Vector3.forward, "owner-a", Du03AStrokeMode.Trajectory), Is.True);
            return session;
        }
    }
}
