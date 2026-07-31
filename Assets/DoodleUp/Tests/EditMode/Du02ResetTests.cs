using DoodleUp.Core;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class Du02ResetTests
    {
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void ResetSnapshotIsFrameRateIndependent(int frameRate)
        {
            var lane = Du02CourseDefinition.Get(Du02TaskId.T1Horizontal);
            var cameraPosition = lane.SpawnPosition + new Vector3(0f, Du02Profile.CameraHeight, -Du02Profile.CameraDistance);
            var cameraRotation = Quaternion.Euler(Du02Profile.CameraPitch, 0f, 0f);
            var expected = CreateSnapshot(lane, cameraPosition, cameraRotation);

            Application.targetFrameRate = frameRate;
            var actual = CreateSnapshot(lane, cameraPosition, cameraRotation);

            Assert.That(actual, Is.EqualTo(expected), $"Reset snapshot changed at {frameRate} fps");
            Assert.That(actual.FixedDeltaTime, Is.EqualTo(0.020f));
        }

        [Test]
        public void HandMarkerUsesApprovedFixedPose()
        {
            Assert.That(Du02Profile.HandLocalPosition, Is.EqualTo(new Vector3(0.35f, 0.80f, 0f)));
        }

        [Test]
        public void SnapshotDetectsAngularVelocityAndProbePhasePerturbation()
        {
            var lane = Du02CourseDefinition.Get(Du02TaskId.T1Horizontal);
            var cameraPosition = lane.SpawnPosition + new Vector3(0f, Du02Profile.CameraHeight, -Du02Profile.CameraDistance);
            var cameraRotation = Quaternion.Euler(Du02Profile.CameraPitch, 0f, 0f);
            var baseline = CreateSnapshot(lane, cameraPosition, cameraRotation);
            var perturbed = new Du02ResetSnapshot(
                lane.TaskId,
                lane.SpawnPosition,
                Quaternion.Euler(17f, 23f, 31f),
                Vector3.zero,
                false,
                Du02Profile.HandLocalPosition,
                Quaternion.identity,
                cameraPosition,
                cameraRotation,
                Du02Profile.CameraVerticalFov,
                Du02Profile.FixedDeltaTime,
                Vector3.one,
                Du02ScaffoldPhase.ProbePerturbed,
                0f,
                19.25f,
                false,
                true,
                3,
                1.25f,
                0,
                new Vector3(1.5f, -2.25f, 3.75f));

            Assert.That(perturbed, Is.Not.EqualTo(baseline));
            Assert.That(perturbed.GetHashCode(), Is.Not.EqualTo(baseline.GetHashCode()));
            Assert.That(perturbed.PlayerRotation, Is.Not.EqualTo(Quaternion.identity));
            Assert.That(perturbed.AngularVelocity, Is.Not.EqualTo(Vector3.zero));
            Assert.That(perturbed.Phase, Is.EqualTo(Du02ScaffoldPhase.ProbePerturbed));
        }

        private static Du02ResetSnapshot CreateSnapshot(Du02LaneDefinition lane, Vector3 cameraPosition, Quaternion cameraRotation)
        {
            return new Du02ResetSnapshot(
                lane.TaskId,
                lane.SpawnPosition,
                Quaternion.identity,
                Vector3.zero,
                false,
                Du02Profile.HandLocalPosition,
                Quaternion.identity,
                cameraPosition,
                cameraRotation,
                Du02Profile.CameraVerticalFov,
                Du02Profile.FixedDeltaTime);
        }
    }
}
