using DoodleUp.Core;
using DoodleUp.Input;
using DoodleUp.Runtime;
using DoodleUp.Stroke;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class Du03BCAdapterTests
    {
        private GameObject root;
        private GameObject player;
        private GameObject hand;
        private GameObject cameraObject;
        private Du03BCInputEdgeLatch latch;
        private Camera camera;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("DU03BC-TestRoot");
            player = new GameObject("Player");
            hand = new GameObject("HandMarker");
            hand.transform.SetParent(player.transform, false);
            hand.transform.localPosition = Du02Profile.HandLocalPosition;
            cameraObject = new GameObject("Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 2f, -6f), Quaternion.Euler(10f, 15f, 0f));
            latch = root.AddComponent<Du03BCInputEdgeLatch>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(hand);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void InputManifestUsesReleaseCommitAndSeparatesResetFromCancel()
        {
            Assert.That(Du03BCInputEdgeLatch.BindingManifest, Does.Contain("Commit=<Mouse>/leftButton#release"));
            Assert.That(Du03BCInputEdgeLatch.BindingManifest, Does.Not.Contain("Confirm=<Keyboard>/e;"));
            Assert.That(Du03BCInputEdgeLatch.BindingManifest, Does.Contain("Cancel=<Mouse>/rightButton|<Keyboard>/escape"));
            Assert.That(Du03BCInputEdgeLatch.BindingManifest, Does.Contain("Reset=<Keyboard>/r"));
            Assert.That(Du03BCInputEdgeLatch.BindingManifest, Does.Contain("InkReset=<Keyboard>/q"));
        }

        [Test]
        public void ProbeSnapshotIsConsumedExactlyOnce()
        {
            var snapshot = new Du03BCInputSnapshot(3, true, false, true, false, false, "TEST");
            latch.EnqueueProbeSnapshot(snapshot);

            var first = latch.ConsumeStrokeEdges();
            var second = latch.ConsumeStrokeEdges();

            Assert.That(first.DrawPressed, Is.True);
            Assert.That(first.EventSequence, Is.EqualTo(3));
            Assert.That(second.DrawPressed, Is.False);
        }

        [Test]
        public void AimSnapshotsGameplayNormalIndependentOfVisualCameraYaw()
        {
            var adapter = root.AddComponent<Du03BCAimInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            adapter.SetProbeRay(new Ray(hand.transform.position - Vector3.forward, Vector3.forward));
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));

            adapter.ReadIntent();

            Assert.That(Vector3.Distance(adapter.LastMappingEvidence.PlaneOrigin, hand.transform.position), Is.LessThanOrEqualTo(0.00001f));
            Assert.That(Vector3.Distance(adapter.LastMappingEvidence.PlaneNormal, Vector3.forward), Is.LessThanOrEqualTo(0.00001f));
            Assert.That(Mathf.Abs(adapter.LastMappingEvidence.PlaneNormal.y), Is.LessThanOrEqualTo(0.00001f));
        }

        [Test]
        public void AimRayIntersectionMatchesIndependentExpected()
        {
            var adapter = root.AddComponent<Du03BCAimInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            var normal = Vector3.forward;
            var point = hand.transform.position
                + Vector3.Cross(Vector3.up, normal).normalized * 0.2f
                + Vector3.up * 0.1f;
            adapter.SetProbeRay(new Ray(point - normal * 2f, normal));
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));

            var intent = adapter.ReadIntent();

            Assert.That(intent.HasCandidate, Is.True);
            Assert.That(Vector3.Distance(intent.Candidate, point), Is.LessThanOrEqualTo(0.00001f));
            Assert.That(adapter.LastMappingEvidence.MappingError, Is.EqualTo(0f).Within(0.00001f));
        }

        [Test]
        public void AimPlaneRemainsFrozenWhenCameraChanges()
        {
            var adapter = root.AddComponent<Du03BCAimInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            var normal = Vector3.forward;
            adapter.SetProbeRay(new Ray(hand.transform.position - normal, normal));
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));
            adapter.ReadIntent();
            var frozenOrigin = adapter.LastMappingEvidence.PlaneOrigin;
            var frozenNormal = adapter.LastMappingEvidence.PlaneNormal;

            camera.transform.Rotate(0f, 30f, 0f);
            adapter.SetProbeRay(new Ray(hand.transform.position - frozenNormal, frozenNormal));
            latch.EnqueueProbeSnapshot(Input(2, false, false, true));
            adapter.ReadIntent();

            Assert.That(adapter.LastMappingEvidence.PlaneOrigin, Is.EqualTo(frozenOrigin));
            Assert.That(adapter.LastMappingEvidence.PlaneNormal, Is.EqualTo(frozenNormal));
        }

        [Test]
        public void AimParallelRayReturnsNullMappingEvidenceAndNonFiniteCandidate()
        {
            var adapter = root.AddComponent<Du03BCAimInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            var normal = Vector3.forward;
            var parallel = Vector3.Cross(normal, Vector3.up).normalized;
            adapter.SetProbeRay(new Ray(hand.transform.position, parallel));
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));

            var intent = adapter.ReadIntent();

            Assert.That(intent.HasCandidate, Is.True);
            Assert.That(float.IsNaN(intent.Candidate.x), Is.True);
            Assert.That(adapter.LastMappingEvidence.RawCandidate.HasValue, Is.False);
            Assert.That(adapter.LastMappingEvidence.InvalidReason, Is.EqualTo(Du03BCMappingInvalidReason.NoPlaneIntersection));
        }

        [Test]
        public void AimNonFiniteRayReportsNonFinite()
        {
            var adapter = root.AddComponent<Du03BCAimInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            adapter.SetProbeRay(new Ray(new Vector3(float.NaN, 0f, 0f), Vector3.forward));
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));

            adapter.ReadIntent();

            Assert.That(adapter.LastMappingEvidence.RawCandidate.HasValue, Is.False);
            Assert.That(adapter.LastMappingEvidence.InvalidReason, Is.EqualTo(Du03BCMappingInvalidReason.NonFinite));
        }

        [Test]
        public void TrajectoryCandidateIsExactlyHandMarkerPosition()
        {
            var adapter = root.AddComponent<Du03BCTrajectoryInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            player.transform.position = new Vector3(0.4f, 1.2f, 0f);
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));

            var intent = adapter.ReadIntent();

            Assert.That(intent.Candidate, Is.EqualTo(hand.transform.position));
            Assert.That(adapter.LastMappingEvidence.MappingSource, Is.EqualTo("HAND_MARKER"));
            Assert.That(adapter.LastMappingEvidence.MouseScreen.HasValue, Is.False);
        }

        [Test]
        public void TrajectoryTracksCurrentMarkerWithoutIndependentOffset()
        {
            var adapter = root.AddComponent<Du03BCTrajectoryInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));
            adapter.ReadIntent();
            player.transform.position += new Vector3(0.08f, 0.12f, 0f);
            latch.EnqueueProbeSnapshot(Input(2, false, false, true));

            var intent = adapter.ReadIntent();

            Assert.That(Vector3.Distance(intent.Candidate, hand.transform.position), Is.LessThanOrEqualTo(0.00001f));
            Assert.That(adapter.LastMappingEvidence.MappingError, Is.EqualTo(0f));
        }

        [Test]
        public void ArmDirectUsesCurrentCameraBasisForSpatialCandidate()
        {
            hand.transform.localPosition = Du03BCArmDirectInputAdapter.NeutralHandLocalPosition;
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var adapter = root.AddComponent<Du03BCArmDirectInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));
            adapter.ReadIntent();
            camera.transform.rotation = Quaternion.Euler(30f, 40f, 0f);
            var neutralTip = hand.transform.parent.TransformPoint(Du03BCArmDirectInputAdapter.NeutralHandLocalPosition);
            adapter.SetProbeMouseDelta(new Vector2(40f, -20f));
            latch.EnqueueProbeSnapshot(Input(2, false, false, true));

            var intent = adapter.ReadIntent();
            var expected = neutralTip
                + camera.transform.right * 0.10f
                + camera.transform.up * -0.05f;

            Assert.That(Vector3.Distance(intent.Candidate, expected), Is.LessThanOrEqualTo(0.00001f));
            Assert.That(Mathf.Abs(intent.Candidate.z - adapter.LastMappingEvidence.PlaneOrigin.z), Is.GreaterThan(0.0001f));
            Assert.That(hand.transform.position, Is.EqualTo(intent.Candidate));
            Assert.That(adapter.Mode, Is.EqualTo(Du03AStrokeMode.Spatial));
            Assert.That(adapter.LastMappingEvidence.MappingSource, Is.EqualTo("CAMERA_LOOK_ARM_SPATIAL"));
        }

        [Test]
        public void ArmDirectFollowsCurrentCameraBasisAfterPress()
        {
            hand.transform.localPosition = Du03BCArmDirectInputAdapter.NeutralHandLocalPosition;
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(15f, 30f, 0f));
            var adapter = root.AddComponent<Du03BCArmDirectInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));
            adapter.ReadIntent();
            camera.transform.rotation = Quaternion.Euler(-20f, -45f, 0f);
            var currentRight = camera.transform.right;
            adapter.SetProbeMouseDelta(new Vector2(40f, 0f));
            latch.EnqueueProbeSnapshot(Input(2, false, false, true));

            var intent = adapter.ReadIntent();
            var expected = hand.transform.parent.TransformPoint(Du03BCArmDirectInputAdapter.NeutralHandLocalPosition)
                + currentRight * 0.10f;

            Assert.That(Vector3.Distance(intent.Candidate, expected), Is.LessThanOrEqualTo(0.00001f));
            Assert.That(Mathf.Abs(intent.Candidate.z - adapter.LastMappingEvidence.PlaneOrigin.z), Is.GreaterThan(0.0001f));
        }

        [Test]
        public void ArmDirectCancelReturnsHandToNeutral()
        {
            hand.transform.localPosition = Du03BCArmDirectInputAdapter.NeutralHandLocalPosition;
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var adapter = root.AddComponent<Du03BCArmDirectInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));
            adapter.ReadIntent();
            adapter.SetProbeMouseDelta(new Vector2(100f, 0f));
            latch.EnqueueProbeSnapshot(Input(2, false, false, true));
            adapter.ReadIntent();
            latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(3, false, false, false, false, true, "TEST"));

            var intent = adapter.ReadIntent();

            Assert.That(intent.CancelPressed, Is.True);
            Assert.That(hand.transform.localPosition, Is.EqualTo(Du03BCArmDirectInputAdapter.NeutralHandLocalPosition));
        }

        [Test]
        public void ArmDirectTracksDesiredTipBeyondLegacyReachRadius()
        {
            hand.transform.localPosition = Du03BCArmDirectInputAdapter.NeutralHandLocalPosition;
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var adapter = root.AddComponent<Du03BCArmDirectInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));
            adapter.ReadIntent();

            adapter.SetProbeMouseDelta(new Vector2(600f, 0f));
            latch.EnqueueProbeSnapshot(Input(2, false, false, true));
            var intent = adapter.ReadIntent();

            Assert.That(Vector3.Distance(adapter.LastMappingEvidence.PlaneOrigin, intent.Candidate), Is.GreaterThan(Du03AStrokeProfile.ReachRadius));
            Assert.That(hand.transform.position, Is.EqualTo(intent.Candidate));
            Assert.That(adapter.LastValidHandPosition, Is.EqualTo(intent.Candidate));
            Assert.That(adapter.DesiredTip, Is.EqualTo(intent.Candidate));
        }

        [Test]
        public void ArmDirectReleaseReturnsHandToNeutral()
        {
            hand.transform.localPosition = Du03BCArmDirectInputAdapter.NeutralHandLocalPosition;
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var adapter = root.AddComponent<Du03BCArmDirectInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));
            adapter.ReadIntent();
            adapter.SetProbeMouseDelta(new Vector2(100f, 0f));
            latch.EnqueueProbeSnapshot(Input(2, false, false, true));
            adapter.ReadIntent();
            adapter.SetProbeMouseDelta(new Vector2(50f, 0f));
            latch.EnqueueProbeSnapshot(Input(3, false, true, false));

            adapter.ReadIntent();

            Assert.That(hand.transform.localPosition, Is.EqualTo(Du03BCArmDirectInputAdapter.NeutralHandLocalPosition));
        }

        [Test]
        public void RouterAppliesConfiguredArmDirectPlayableStartRoute()
        {
            var deterministic = root.AddComponent<Du03ADeterministicIntentSource>();
            var aim = root.AddComponent<Du03BCAimInputAdapter>();
            aim.Configure(latch, hand.transform, camera);
            var trajectory = root.AddComponent<Du03BCTrajectoryInputAdapter>();
            trajectory.Configure(latch, hand.transform, camera);
            var armDirect = root.AddComponent<Du03BCArmDirectInputAdapter>();
            armDirect.Configure(latch, hand.transform, camera);
            var router = root.AddComponent<Du03BCAdapterRouter>();
            router.Configure(deterministic, aim, trajectory, armDirect: armDirect);
            router.SetRoute(Du03BCAdapterRoute.Aim);
            router.ConfigurePlayableStartRoute(Du03BCAdapterRoute.ArmDirect);

            router.ApplyPlayableStartRouteForProbe();

            Assert.That(router.ActiveRoute, Is.EqualTo(Du03BCAdapterRoute.ArmDirect));
            Assert.That(router.ActiveAdapter, Is.SameAs(armDirect));
        }

        [Test]
        public void InactiveAdapterProducesNonePhaseAndNoCandidate()
        {
            var adapter = root.AddComponent<Du03BCTrajectoryInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            latch.EnqueueProbeSnapshot(Input(1, false, false, false));

            var intent = adapter.ReadIntent();

            Assert.That(intent.HasCandidate, Is.False);
            Assert.That(adapter.LastMappingEvidence.SamplePhase, Is.EqualTo("NONE"));
            Assert.That(adapter.LastMappingEvidence.RawCandidate.HasValue, Is.False);
        }

        [Test]
        public void AdapterResetClearsSnapshotAndLatchedEdges()
        {
            var adapter = root.AddComponent<Du03BCTrajectoryInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            latch.EnqueueProbeSnapshot(Input(1, true, false, true));
            adapter.ReadIntent();

            adapter.ResetAdapter();
            var intent = adapter.ReadIntent();

            Assert.That(intent.DrawPressed, Is.False);
            Assert.That(intent.HasCandidate, Is.False);
            Assert.That(adapter.LastMappingEvidence.SamplePhase, Is.EqualTo("NONE"));
        }

        private static Du03BCInputSnapshot Input(long sequence, bool pressed, bool released, bool held) =>
            new(sequence, pressed, released, held, false, false, "TEST");
    }
}
