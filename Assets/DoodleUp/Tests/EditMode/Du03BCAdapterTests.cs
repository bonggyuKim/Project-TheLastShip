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
        public void InputManifestSeparatesResetFromCancel()
        {
            Assert.That(Du03BCInputEdgeLatch.BindingManifest, Does.Contain("Cancel=<Mouse>/rightButton|<Keyboard>/escape"));
            Assert.That(Du03BCInputEdgeLatch.BindingManifest, Does.Contain("Reset=<Keyboard>/r"));
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
