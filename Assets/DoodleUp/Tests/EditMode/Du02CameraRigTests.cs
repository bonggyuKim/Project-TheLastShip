using DoodleUp.Core;
using DoodleUp.Input;
using DoodleUp.Runtime;
using DoodleUp.Stroke;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class Du02CameraRigTests
    {
        [Test]
        public void PretestOrbitClampsAndResetRestoresCanonicalPose()
        {
            var root = new GameObject("camera-orbit-test");
            var player = new GameObject("player");
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            var latch = root.AddComponent<Du03BCInputEdgeLatch>();
            var driver = root.AddComponent<Du03AStrokeDriver>();
            var rig = cameraObject.AddComponent<Du02CameraRig>();
            driver.Configure(player.transform, camera, null, "owner", Du03AStrokeMode.Aim);
            rig.Configure(camera, player.transform);
            rig.ConfigurePretestOrbit(driver, latch, true);
            rig.ResetPose(Vector3.zero);

            rig.TickPretestOrbitForProbe(1f, 1f);

            Assert.That(rig.VisualYawOffset, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(camera.transform.eulerAngles.y, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(camera.transform.position, Is.EqualTo(Du02Profile.PretestCameraLocalPosition));
            Assert.That(NormalizeSignedAngle(camera.transform.eulerAngles.x), Is.EqualTo(Du02Profile.PretestCameraPitch).Within(0.0001f));
            var expectedProfileId = Application.isBatchMode
                ? Du02Profile.ProfileId
                : Du02CameraRig.PretestProfileId;
            Assert.That(rig.ActiveProfileId, Is.EqualTo(expectedProfileId));
            Assert.That(Du02CameraRig.PretestProfileId, Is.EqualTo("PRETEST_FIRST_PERSON_V2"));
            Assert.That(player.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(rig.GameplayNormal, Is.EqualTo(Vector3.forward));

            rig.ResetPose(new Vector3(2f, 0f, 0f));
            rig.TickPretestOrbitForProbe(1f, 0f);

            Assert.That(rig.VisualYawOffset, Is.Zero.Within(0.0001f));
            Assert.That(camera.transform.eulerAngles.y, Is.Zero.Within(0.0001f));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(2f, 0f, 0f) + Du02Profile.PretestCameraLocalPosition));

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }

        private static float NormalizeSignedAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        [Test]
        public void FollowMaintainsPretestLocalPoseAcrossPlayerMovementAndReset()
        {
            var player = new GameObject("player-follow-test");
            var cameraObject = new GameObject("camera-follow-test");
            var camera = cameraObject.AddComponent<Camera>();
            var root = new GameObject("follow-driver-root");
            var driver = root.AddComponent<Du03AStrokeDriver>();
            driver.Configure(player.transform, camera, null, "owner", Du03AStrokeMode.Aim);
            var rig = cameraObject.AddComponent<Du02CameraRig>();
            rig.Configure(camera, player.transform);
            rig.ConfigurePretestOrbit(driver, null, true);
            rig.ResetPose(player.transform.position);
            var expectedOffset = Application.isBatchMode
                ? new Vector3(0f, Du02Profile.CameraHeight, -Du02Profile.CameraDistance)
                : Du02Profile.PretestCameraLocalPosition;
            var expectedPitch = Application.isBatchMode
                ? Du02Profile.CameraPitch
                : Du02Profile.PretestCameraPitch;

            player.transform.position = new Vector3(2f, 1f, -3f);
            rig.FollowPlayerForProbe();

            Assert.That(camera.transform.position, Is.EqualTo(player.transform.position + expectedOffset));
            Assert.That(NormalizeSignedAngle(camera.transform.eulerAngles.x), Is.EqualTo(expectedPitch).Within(0.0001f));

            player.transform.position = DuSandboxController.SpawnPosition;
            rig.ResetPose(player.transform.position);
            rig.FollowPlayerForProbe();

            Assert.That(camera.transform.position, Is.EqualTo(player.transform.position + expectedOffset));
            Assert.That(rig.VisualYawOffset, Is.Zero.Within(0.0001f));

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ArmDirectFollowMaintainsEyeOffsetAndForwardControlPlane()
        {
            var player = new GameObject("arm-follow-test");
            var cameraObject = new GameObject("arm-camera-follow-test");
            var camera = cameraObject.AddComponent<Camera>();
            var rig = cameraObject.AddComponent<Du02CameraRig>();
            rig.Configure(camera, player.transform);
            rig.ConfigurePretestOrbit(null, null, true);
            rig.ResetPose(player.transform.position);
            rig.SetArmDirectProfile(true);

            player.transform.position = new Vector3(-1f, 0.4f, 2.5f);
            rig.FollowPlayerForProbe();

            Assert.That(camera.transform.position, Is.EqualTo(player.transform.position + Du02CameraRig.ArmDirectEyeOffset));
            Assert.That(camera.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(rig.GameplayNormal, Is.EqualTo(Vector3.forward));
            Assert.That(rig.ActiveProfileId, Is.EqualTo(Du03BCArmDirectInputAdapter.ProfileId));

            player.transform.position = DuSandboxController.SpawnPosition;
            rig.ResetPose(player.transform.position);
            rig.FollowPlayerForProbe();

            Assert.That(camera.transform.position, Is.EqualTo(player.transform.position + Du02CameraRig.ArmDirectEyeOffset));
            Assert.That(camera.transform.rotation, Is.EqualTo(Quaternion.identity));

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
        }

        [Test]
        public void ArmDirectLookClampsPitchAndFollowPreservesOrientation()
        {
            var root = new GameObject("arm-look-test");
            var player = new GameObject("player");
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            var latch = root.AddComponent<Du03BCInputEdgeLatch>();
            var driver = root.AddComponent<Du03AStrokeDriver>();
            var rig = cameraObject.AddComponent<Du02CameraRig>();
            driver.Configure(player.transform, camera, null, "owner", Du03AStrokeMode.Aim);
            rig.Configure(camera, player.transform);
            rig.ConfigurePretestOrbit(driver, latch, true);
            rig.ResetPose(Vector3.zero);
            rig.SetArmDirectProfile(true);

            rig.TickFirstPersonLookForProbe(new Vector2(250f, -1000f));
            var rotationBeforeFollow = camera.transform.rotation;
            player.transform.position = new Vector3(2f, 0.5f, -4f);
            rig.FollowPlayerForProbe();

            Assert.That(rig.VisualYawOffset, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(rig.VisualPitchOffset, Is.EqualTo(Du02CameraRig.FirstPersonPitchLimit).Within(0.0001f));
            Assert.That(camera.transform.rotation, Is.EqualTo(rotationBeforeFollow));
            Assert.That(camera.transform.position, Is.EqualTo(player.transform.position + Du02CameraRig.ArmDirectEyeOffset));

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ArmDirectLookContinuesDuringDrawing()
        {
            var root = new GameObject("arm-look-owner-test");
            var player = new GameObject("player");
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            var latch = root.AddComponent<Du03BCInputEdgeLatch>();
            var driver = root.AddComponent<Du03AStrokeDriver>();
            var rig = cameraObject.AddComponent<Du02CameraRig>();
            driver.Configure(player.transform, camera, null, "owner", Du03AStrokeMode.Aim);
            rig.Configure(camera, player.transform);
            rig.ConfigurePretestOrbit(driver, latch, true);
            rig.ResetPose(Vector3.zero);
            rig.SetArmDirectProfile(true);
            rig.TickFirstPersonLookForProbe(new Vector2(100f, 50f));
            var rotationBeforeDrawingLook = camera.transform.rotation;
            driver.ProcessIntent(new Du03ADrawIntent(true, false, false, false, false, default));

            rig.TickFirstPersonLookForProbe(new Vector2(100f, 100f));

            Assert.That(driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Drawing));
            Assert.That(camera.transform.rotation, Is.Not.EqualTo(rotationBeforeDrawingLook));

            var drawingRotation = camera.transform.rotation;
            driver.ProcessIntent(new Du03ADrawIntent(false, false, false, true, false, default));
            rig.TickFirstPersonLookForProbe(new Vector2(100f, 0f));

            Assert.That(driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(camera.transform.rotation, Is.Not.EqualTo(drawingRotation));

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ArmDirectMovementUsesYawAndIgnoresPitch()
        {
            var root = new GameObject("arm-movement-basis-test");
            var player = new GameObject("player");
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            var latch = root.AddComponent<Du03BCInputEdgeLatch>();
            var driver = root.AddComponent<Du03AStrokeDriver>();
            var rig = cameraObject.AddComponent<Du02CameraRig>();
            driver.Configure(player.transform, camera, null, "owner", Du03AStrokeMode.Aim);
            rig.Configure(camera, player.transform);
            rig.ConfigurePretestOrbit(driver, latch, true);
            rig.ResetPose(Vector3.zero);
            rig.SetArmDirectProfile(true);
            rig.TickFirstPersonLookForProbe(new Vector2(750f, -1000f));

            var movement = rig.TransformMovementForProbe(0f, 1f);

            Assert.That(movement.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(movement.y, Is.Zero.Within(0.0001f));
            Assert.That(rig.VisualPitchOffset, Is.EqualTo(Du02CameraRig.FirstPersonPitchLimit).Within(0.0001f));

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ArmDirectBodyLookRotatesArmAndLogicalHandWithoutRotatingPlayerRoot()
        {
            var root = new GameObject("arm-body-look-test");
            var player = new GameObject("player");
            var bodyYawAnchor = new GameObject(Du02CameraRig.BodyYawAnchorName);
            bodyYawAnchor.transform.SetParent(player.transform, false);
            var bodyVisual = new GameObject("BodyVisual");
            bodyVisual.transform.SetParent(bodyYawAnchor.transform, false);
            bodyVisual.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var armPitchAnchor = new GameObject(Du02CameraRig.ArmPitchAnchorName);
            armPitchAnchor.transform.SetParent(bodyYawAnchor.transform, false);
            armPitchAnchor.transform.localPosition = Du02CameraRig.ArmPitchAnchorLocalPosition;
            var armVisualRoot = new GameObject("ArmVisualRoot");
            armVisualRoot.transform.SetParent(armPitchAnchor.transform, false);
            var hand = new GameObject("HandMarker");
            hand.transform.SetParent(armPitchAnchor.transform, false);
            hand.transform.localPosition = Du03BCArmDirectInputAdapter.NeutralHandPitchLocalPosition;
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            var latch = root.AddComponent<Du03BCInputEdgeLatch>();
            var driver = root.AddComponent<Du03AStrokeDriver>();
            var rig = cameraObject.AddComponent<Du02CameraRig>();
            driver.Configure(hand.transform, camera, null, "owner", Du03AStrokeMode.Aim);
            rig.Configure(camera, player.transform, bodyYawAnchor.transform, armPitchAnchor.transform);
            rig.ConfigurePretestOrbit(driver, latch, true);
            rig.ResetPose(Vector3.zero);
            rig.SetArmDirectProfile(true);

            rig.TickFirstPersonLookForProbe(new Vector2(750f, -250f));

            Assert.That(player.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(bodyYawAnchor.transform.localRotation.eulerAngles.y, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(NormalizeSignedAngle(armPitchAnchor.transform.localEulerAngles.x), Is.EqualTo(30f).Within(0.0001f));
            Assert.That(hand.transform.localPosition, Is.EqualTo(Du03BCArmDirectInputAdapter.NeutralHandPitchLocalPosition));
            Assert.That(bodyVisual.transform.parent, Is.EqualTo(bodyYawAnchor.transform));
            Assert.That(armVisualRoot.transform.parent, Is.EqualTo(armPitchAnchor.transform));
            Assert.That(Vector3.Distance(
                hand.transform.position,
                armPitchAnchor.transform.TransformPoint(Du03BCArmDirectInputAdapter.NeutralHandPitchLocalPosition)),
                Is.LessThanOrEqualTo(0.00001f));

            var localOffsetBeforeMove = player.transform.InverseTransformPoint(hand.transform.position);
            player.transform.position = new Vector3(2f, 0.3f, -4f);
            rig.FollowPlayerForProbe();
            Assert.That(Vector3.Distance(
                hand.transform.position,
                player.transform.TransformPoint(localOffsetBeforeMove)),
                Is.LessThanOrEqualTo(0.00001f));

            rig.ResetPose(DuSandboxController.SpawnPosition);
            Assert.That(bodyYawAnchor.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(armPitchAnchor.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(hand.transform.localPosition, Is.EqualTo(Du03BCArmDirectInputAdapter.NeutralHandPitchLocalPosition));

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void PretestOrbitFreezesOutsideIdle()
        {
            var root = new GameObject("camera-orbit-freeze-test");
            var player = new GameObject("player");
            var cameraObject = new GameObject("camera");
            var camera = cameraObject.AddComponent<Camera>();
            var latch = root.AddComponent<Du03BCInputEdgeLatch>();
            var driver = root.AddComponent<Du03AStrokeDriver>();
            var rig = cameraObject.AddComponent<Du02CameraRig>();
            driver.Configure(player.transform, camera, null, "owner", Du03AStrokeMode.Aim);
            rig.Configure(camera, player.transform);
            rig.ConfigurePretestOrbit(driver, latch, true);
            rig.ResetPose(Vector3.zero);
            driver.ProcessIntent(new Du03ADrawIntent(true, false, false, false, false, default));

            rig.TickPretestOrbitForProbe(1f, 0.5f);

            Assert.That(driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Drawing));
            Assert.That(rig.VisualYawOffset, Is.Zero.Within(0.0001f));

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }
    }
}
