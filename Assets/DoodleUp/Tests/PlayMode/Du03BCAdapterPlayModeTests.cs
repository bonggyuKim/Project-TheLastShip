using System.Collections;
using DoodleUp.Core;
using DoodleUp.Input;
using DoodleUp.Physics;
using DoodleUp.Runtime;
using DoodleUp.Stroke;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    public sealed class Du03BCAdapterPlayModeTests
    {
        [UnityTest]
        public IEnumerator TrajectoryLatchFlowsThroughDriverLateUpdateOnce()
        {
            var setup = CreateSetup(Du03AStrokeMode.Trajectory);
            yield return null;
            SetRouteAfterSceneStart(setup, Du03AStrokeMode.Trajectory);
            var evidenceCount = 0;
            Du03ALateUpdateEvidence evidence = default;
            setup.Driver.LateUpdateProcessed += value =>
            {
                evidence = value;
                evidenceCount++;
            };
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));

            yield return null;

            Assert.That(evidenceCount, Is.EqualTo(1));
            Assert.That(evidence.CandidateCountThisFrame, Is.EqualTo(1));
            Assert.That(evidence.EventOrder, Is.EqualTo("PRESS>CANDIDATE"));
            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Drawing));
            Object.Destroy(setup.Root);
        }

        [UnityTest]
        public IEnumerator ReleaseFrameProcessesCandidateThenAutoCommits()
        {
            var setup = CreateSetup(Du03AStrokeMode.Trajectory);
            yield return null;
            SetRouteAfterSceneStart(setup, Du03AStrokeMode.Trajectory);
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;
            setup.Player.transform.position += Vector3.right * 0.24f;
            Du03ALateUpdateEvidence releaseEvidence = default;
            setup.Driver.LateUpdateProcessed += value => releaseEvidence = value;
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(2, false, true, false, false, false, "PLAYMODE"));

            yield return null;

            Assert.That(releaseEvidence.EventOrder, Is.EqualTo("CANDIDATE>RELEASE>AUTO_COMMIT"));
            Assert.That(releaseEvidence.CandidateCountThisFrame, Is.EqualTo(1));
            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(setup.Driver.Session.LastTerminalState, Is.EqualTo(Du03AStrokeSessionState.Committed));
            Assert.That(setup.Driver.CommittedColliderCount, Is.EqualTo(1));
            Object.Destroy(setup.Root);
        }

        [UnityTest]
        public IEnumerator DrawingConsumesExactlyOneCandidatePerFrame()
        {
            var setup = CreateSetup(Du03AStrokeMode.Trajectory);
            yield return null;
            SetRouteAfterSceneStart(setup, Du03AStrokeMode.Trajectory);
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;
            setup.Player.transform.position += Vector3.right * 0.24f;
            Du03ALateUpdateEvidence evidence = default;
            var evidenceCount = 0;
            setup.Driver.LateUpdateProcessed += value =>
            {
                evidence = value;
                evidenceCount++;
            };
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(2, false, false, true, false, false, "PLAYMODE"));

            yield return null;

            Assert.That(evidenceCount, Is.EqualTo(1));
            Assert.That(evidence.CandidateCountThisFrame, Is.EqualTo(1));
            Assert.That(evidence.EventOrder, Is.EqualTo("CANDIDATE"));
            Object.Destroy(setup.Root);
        }

        [UnityTest]
        public IEnumerator PlayableRouteSwitchResetsSessionAndChangesDriverMode()
        {
            var setup = CreateSetup(Du03AStrokeMode.Trajectory);
            yield return null;
            SetRouteAfterSceneStart(setup, Du03AStrokeMode.Trajectory);
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;
            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Drawing));

            setup.Router.CyclePlayableRoute();

            Assert.That(setup.Router.ActiveRoute, Is.EqualTo(Du03BCAdapterRoute.Aim));
            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(2, true, false, true, false, false, "PLAYMODE"));
            yield return null;
            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Drawing));
            Assert.That(setup.Driver.Session.PlaneOrigin, Is.EqualTo(setup.Router.ActiveAdapter.LastMappingEvidence.PlaneOrigin));
            Object.Destroy(setup.Root);
        }

        [UnityTest]
        public IEnumerator ArmDirectAcceptsBeyondLegacyReachAndAutoCommitsThenReturnsNeutral()
        {
            var setup = CreateSetup(Du03AStrokeMode.Aim, includeArmDirect: true);
            yield return null;
            setup.Router.SetRoute(Du03BCAdapterRoute.ArmDirect);
            setup.Driver.SetModeForProbe(Du03AStrokeMode.Spatial);
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;

            Du03ALateUpdateEvidence beyondReachEvidence = default;
            setup.Driver.LateUpdateProcessed += value => beyondReachEvidence = value;
            setup.ArmDirect.SetProbeMouseDelta(new Vector2(600f, 0f));
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(2, false, false, true, false, false, "PLAYMODE"));
            yield return null;

            Assert.That(Vector3.Distance(setup.Driver.Session.PlaneOrigin, setup.ArmDirect.DesiredTip), Is.GreaterThan(Du03AStrokeProfile.ReachRadius));
            Assert.That(beyondReachEvidence.CandidateResult.CandidateValid, Is.True);
            Assert.That(beyondReachEvidence.CandidateResult.AcceptedAppended, Is.True);
            Assert.That(beyondReachEvidence.CandidateResult.Reason, Is.EqualTo(Du03ACandidateReason.Appended));
            Assert.That(setup.ArmDirect.LastValidHandPosition, Is.EqualTo(setup.ArmDirect.DesiredTip));
            Assert.That(setup.Driver.HandMarker.position, Is.EqualTo(setup.ArmDirect.DesiredTip));

            setup.ArmDirect.SetProbeMouseDelta(new Vector2(100f, 0f));
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(3, false, true, false, false, false, "PLAYMODE"));
            yield return null;

            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(setup.Driver.Session.LastTerminalState, Is.EqualTo(Du03AStrokeSessionState.Committed));
            Assert.That(setup.Driver.CommittedColliderCount, Is.GreaterThan(0));
            Assert.That(setup.Driver.HandMarker.localPosition, Is.EqualTo(
                Du03BCArmDirectInputAdapter.GetNeutralHandLocalPosition(setup.Driver.HandMarker)));
            Object.Destroy(setup.Root);
        }

        [UnityTest]
        public IEnumerator ArmDirectBodyLookSetsReachOriginAndReleaseReturnsCurrentBodyLocalNeutral()
        {
            var setup = CreateSetup(Du03AStrokeMode.Aim, includeSandbox: true, includeArmDirect: true);
            yield return null;
            setup.Router.SetRoute(Du03BCAdapterRoute.ArmDirect);
            setup.Driver.SetModeForProbe(Du03AStrokeMode.Spatial);
            setup.CameraRig.SetArmDirectProfile(true);
            setup.CameraRig.TickFirstPersonLookForProbe(new Vector2(750f, -250f));
            var expectedNeutral = setup.Driver.HandMarker.position;
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;

            Assert.That(Vector3.Distance(setup.Driver.Session.PlaneOrigin, expectedNeutral), Is.LessThanOrEqualTo(0.00001f));
            Assert.That(setup.Driver.Session.PlaneNormal, Is.EqualTo(Vector3.forward));
            Assert.That(setup.Driver.HandMarker.parent.name, Is.EqualTo(Du02CameraRig.ArmPitchAnchorName));
            Assert.That(setup.Driver.HandMarker.parent.parent.localRotation.eulerAngles.y, Is.EqualTo(90f).Within(0.0001f));
            Assert.That(NormalizeSignedAngle(setup.Driver.HandMarker.parent.localEulerAngles.x), Is.EqualTo(30f).Within(0.0001f));

            setup.ArmDirect.SetProbeMouseDelta(new Vector2(100f, 0f));
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(2, false, false, true, false, false, "PLAYMODE"));
            yield return null;
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(3, false, false, false, false, true, "PLAYMODE"));
            yield return null;

            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(setup.Driver.HandMarker.localPosition, Is.EqualTo(Du03BCArmDirectInputAdapter.NeutralHandPitchLocalPosition));
            Assert.That(Vector3.Distance(setup.Driver.HandMarker.position, expectedNeutral), Is.LessThanOrEqualTo(0.00001f));
            Assert.That(setup.Player.transform.rotation, Is.EqualTo(Quaternion.identity));
            Object.Destroy(setup.Root);
        }

        [UnityTest]
        public IEnumerator ArmDirectDrawingRotatesCameraAndCommitsSpatialStroke()
        {
            var setup = CreateSetup(Du03AStrokeMode.Aim, includeSandbox: true, includeArmDirect: true);
            yield return null;
            setup.Router.SetRoute(Du03BCAdapterRoute.ArmDirect);
            setup.Driver.SetModeForProbe(Du03AStrokeMode.Spatial);
            setup.CameraRig.SetArmDirectProfile(true);
            setup.CameraRig.TickFirstPersonLookForProbe(new Vector2(100f, -50f));
            var rotationBeforeDrawingLook = setup.CameraRig.transform.rotation;
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;

            setup.CameraRig.TickFirstPersonLookForProbe(new Vector2(100f, 100f));
            setup.ArmDirect.SetProbeMouseDelta(new Vector2(100f, 0f));
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(2, false, false, true, false, false, "PLAYMODE"));
            yield return null;
            var firstSpatialPoint = setup.ArmDirect.DesiredTip;

            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Drawing));
            Assert.That(setup.CameraRig.transform.rotation, Is.Not.EqualTo(rotationBeforeDrawingLook));
            Assert.That(setup.Driver.Session.AcceptedPoints.Count, Is.GreaterThan(1));
            Assert.That(Mathf.Abs(firstSpatialPoint.z - setup.Driver.Session.PlaneOrigin.z), Is.GreaterThan(0.0001f));

            setup.CameraRig.TickFirstPersonLookForProbe(new Vector2(-75f, -75f));
            setup.ArmDirect.SetProbeMouseDelta(new Vector2(100f, 0f));
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(3, false, true, false, false, false, "PLAYMODE"));
            yield return null;

            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(setup.Driver.Session.LastTerminalState, Is.EqualTo(Du03AStrokeSessionState.Committed));
            Assert.That(setup.Driver.CommittedColliderCount, Is.GreaterThan(0));
            Assert.That(setup.Driver.HandMarker.localPosition, Is.EqualTo(
                Du03BCArmDirectInputAdapter.GetNeutralHandLocalPosition(setup.Driver.HandMarker)));
            Object.Destroy(setup.Root);
        }

        [UnityTest]
        public IEnumerator SandboxDrawingLocksDepthButKeepsHorizontalAndJumpThenUnlocks()
        {
            var setup = CreateSetup(Du03AStrokeMode.Trajectory, true);
            yield return null;
            SetRouteAfterSceneStart(setup, Du03AStrokeMode.Trajectory);

            for (var i = 0; i < 30 && !setup.Motor.IsGrounded; i++)
                yield return new WaitForFixedUpdate();

            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;
            var lockedDepth = setup.Player.transform.position.z;
            setup.Controller.ApplyMovementForProbe(1f, 1f, true);
            yield return new WaitForFixedUpdate();

            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Drawing));
            Assert.That(setup.Controller.DepthMovementLocked, Is.True);
            Assert.That(setup.Player.transform.position.z, Is.EqualTo(lockedDepth).Within(0.001f));
            Assert.That(setup.Motor.Velocity.z, Is.Zero.Within(0.0001f));
            Assert.That(setup.Motor.Velocity.x, Is.EqualTo(Du02Profile.GroundSpeed).Within(0.0001f));
            Assert.That(setup.Motor.Velocity.y, Is.GreaterThan(0f));

            setup.Player.transform.position += Vector3.right * 0.24f;
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(2, false, true, false, false, false, "PLAYMODE"));
            yield return null;
            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));

            setup.Controller.ApplyMovementForProbe(0f, 1f, false);
            yield return new WaitForFixedUpdate();
            Assert.That(setup.Motor.Velocity.z, Is.GreaterThan(0f));

            Object.Destroy(setup.Root);
        }

        [UnityTest]
        public IEnumerator ArmDirectDrawingAllowsCameraRelativeMovementAndJump()
        {
            var setup = CreateSetup(Du03AStrokeMode.Aim, includeSandbox: true, includeArmDirect: true);
            yield return null;
            setup.Router.SetRoute(Du03BCAdapterRoute.ArmDirect);
            setup.Driver.SetModeForProbe(Du03AStrokeMode.Spatial);
            setup.CameraRig.SetArmDirectProfile(true);

            for (var i = 0; i < 30 && !setup.Motor.IsGrounded; i++)
                yield return new WaitForFixedUpdate();

            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;
            setup.Controller.ApplyMovementForProbe(1f, 1f, true);
            yield return new WaitForFixedUpdate();

            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Drawing));
            Assert.That(setup.Controller.DepthMovementLocked, Is.False);
            Assert.That(setup.Motor.Velocity.x, Is.GreaterThan(0f));
            Assert.That(setup.Motor.Velocity.z, Is.GreaterThan(0f));
            Assert.That(setup.Motor.Velocity.y, Is.GreaterThan(0f));
            Assert.That(setup.Controller.StrokeRootDepth, Is.Zero.Within(0.0001f));
            Assert.That(setup.Controller.StrokeHandDepth, Is.Zero.Within(0.0001f));

            Object.Destroy(setup.Root);
        }

        [UnityTest]
        public IEnumerator InkResetRestoresInkAndStrokesWithoutMovingPlayerOrCamera()
        {
            var setup = CreateSetup(Du03AStrokeMode.Aim, includeSandbox: true, includeArmDirect: true);
            yield return null;
            setup.Router.SetRoute(Du03BCAdapterRoute.ArmDirect);
            setup.Driver.SetModeForProbe(Du03AStrokeMode.Spatial);
            setup.CameraRig.SetArmDirectProfile(true);
            setup.Motor.SetState(
                new Vector3(1.75f, 0.4f, 2.25f),
                Quaternion.identity,
                Vector3.zero,
                Vector3.zero);
            setup.Motor.SetDepthLocomotionAllowed(true);
            setup.CameraRig.TickFirstPersonLookForProbe(new Vector2(250f, -125f));
            setup.CameraRig.FollowPlayerForProbe();

            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;
            setup.ArmDirect.SetProbeMouseDelta(new Vector2(100f, 0f));
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(2, false, true, false, false, false, "PLAYMODE"));
            yield return null;
            Assert.That(setup.Driver.Session.AvailableInk, Is.LessThan(Du03AStrokeProfile.InitialInk));
            Assert.That(setup.Driver.CommittedColliderCount, Is.GreaterThan(0));
            setup.CameraRig.FollowPlayerForProbe();
            var playerPosition = setup.Player.transform.position;
            var cameraPosition = setup.CameraRig.transform.position;
            var cameraRotation = setup.CameraRig.transform.rotation;

            setup.Controller.ResetInk("PLAYMODE_PROBE");

            Assert.That(setup.Driver.Session.AvailableInk, Is.EqualTo(Du03AStrokeProfile.InitialInk).Within(0.0001f));
            Assert.That(setup.Driver.CommittedColliderCount, Is.Zero);
            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(Vector3.Distance(setup.Player.transform.position, playerPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Vector3.Distance(setup.CameraRig.transform.position, cameraPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(setup.CameraRig.transform.rotation, cameraRotation), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(setup.Controller.InkResetGeneration, Is.EqualTo(1));

            Object.Destroy(setup.Root);
        }

        [UnityTest]
        public IEnumerator ResetClearsHeldEdgeAndStrokeState()
        {
            var setup = CreateSetup(Du03AStrokeMode.Trajectory);
            yield return null;
            SetRouteAfterSceneStart(setup, Du03AStrokeMode.Trajectory);
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;
            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Drawing));

            setup.Driver.ResetSession();
            setup.Router.ResetActiveAdapter();
            yield return null;

            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));
            Assert.That(setup.Driver.Session.LedgerTotal, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(setup.Latch.DrawHeld, Is.False);
            Object.Destroy(setup.Root);
        }

        private static float NormalizeSignedAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private static void SetRouteAfterSceneStart(Setup setup, Du03AStrokeMode mode)
        {
            var route = mode == Du03AStrokeMode.Aim
                ? Du03BCAdapterRoute.Aim
                : Du03BCAdapterRoute.Trajectory;
            setup.Router.SetRoute(route);
            setup.Driver.SetModeForProbe(mode);
        }

        private static Setup CreateSetup(Du03AStrokeMode mode, bool includeSandbox = false, bool includeArmDirect = false)
        {
            var root = new GameObject("DU03BC-PlayMode");
            GameObject floor = null;
            if (includeSandbox)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "SandboxFloor";
                floor.transform.SetParent(root.transform, false);
                floor.transform.localScale = new Vector3(8f, 0.2f, 8f);
            }
            var player = new GameObject("Player");
            player.transform.SetParent(root.transform, false);
            player.transform.position = DuSandboxController.SpawnPosition;
            Du02PlayerMotor motor = null;
            if (includeSandbox)
            {
                var body = player.AddComponent<Rigidbody>();
                body.useGravity = true;
                var capsule = player.AddComponent<CapsuleCollider>();
                capsule.radius = 0.25f;
                capsule.height = 1f;
                capsule.center = new Vector3(0f, 0.5f, 0f);
                motor = player.AddComponent<Du02PlayerMotor>();
            }
            Transform handParent = player.transform;
            Transform bodyYawAnchor = null;
            Transform armPitchAnchor = null;
            if (includeSandbox)
            {
                bodyYawAnchor = new GameObject(Du02CameraRig.BodyYawAnchorName).transform;
                bodyYawAnchor.SetParent(player.transform, false);
                armPitchAnchor = new GameObject(Du02CameraRig.ArmPitchAnchorName).transform;
                armPitchAnchor.SetParent(bodyYawAnchor, false);
                armPitchAnchor.localPosition = Du02CameraRig.ArmPitchAnchorLocalPosition;
                handParent = armPitchAnchor;
            }
            var hand = new GameObject("HandMarker");
            hand.transform.SetParent(handParent, false);
            hand.transform.localPosition = Du02Profile.HandLocalPosition;
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 2f, -6f);
            var camera = cameraObject.AddComponent<Camera>();
            var latch = root.AddComponent<Du03BCInputEdgeLatch>();
            var deterministic = root.AddComponent<Du03ADeterministicIntentSource>();
            var aim = root.AddComponent<Du03BCAimInputAdapter>();
            aim.Configure(latch, hand.transform, camera);
            var trajectory = root.AddComponent<Du03BCTrajectoryInputAdapter>();
            trajectory.Configure(latch, hand.transform, camera);
            Du03BCArmDirectInputAdapter armDirect = null;
            if (includeArmDirect)
            {
                hand.transform.localPosition = Du03BCArmDirectInputAdapter.GetNeutralHandLocalPosition(hand.transform);
                camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                armDirect = root.AddComponent<Du03BCArmDirectInputAdapter>();
                armDirect.Configure(latch, hand.transform, camera);
            }
            var router = root.AddComponent<Du03BCAdapterRouter>();
            router.Configure(deterministic, aim, trajectory, armDirect: armDirect);
            router.SetRoute(mode == Du03AStrokeMode.Aim ? Du03BCAdapterRoute.Aim : Du03BCAdapterRoute.Trajectory);
            var driver = root.AddComponent<Du03AStrokeDriver>();
            driver.Configure(hand.transform, camera, router, "test-owner", mode);
            router.SetStrokeDriver(driver);
            DuSandboxController controller = null;
            if (includeSandbox)
            {
                var cameraRig = cameraObject.AddComponent<Du02CameraRig>();
                cameraRig.Configure(camera, player.transform, bodyYawAnchor, armPitchAnchor);
                cameraRig.ConfigurePretestOrbit(driver, latch, false);
                controller = root.AddComponent<DuSandboxController>();
                controller.Configure(root.AddComponent<Du02InputReader>(), motor, hand.transform, cameraRig, driver, router, latch);
            }
            return new Setup(root, player, latch, router, driver, motor, controller, armDirect, cameraObject.GetComponent<Du02CameraRig>());
        }

        private readonly struct Setup
        {
            public readonly GameObject Root;
            public readonly GameObject Player;
            public readonly Du03BCInputEdgeLatch Latch;
            public readonly Du03BCAdapterRouter Router;
            public readonly Du03AStrokeDriver Driver;
            public readonly Du02PlayerMotor Motor;
            public readonly DuSandboxController Controller;
            public readonly Du03BCArmDirectInputAdapter ArmDirect;
            public readonly Du02CameraRig CameraRig;

            public Setup(
                GameObject root,
                GameObject player,
                Du03BCInputEdgeLatch latch,
                Du03BCAdapterRouter router,
                Du03AStrokeDriver driver,
                Du02PlayerMotor motor = null,
                DuSandboxController controller = null,
                Du03BCArmDirectInputAdapter armDirect = null,
                Du02CameraRig cameraRig = null)
            {
                Root = root;
                Player = player;
                Latch = latch;
                Router = router;
                Driver = driver;
                Motor = motor;
                Controller = controller;
                ArmDirect = armDirect;
                CameraRig = cameraRig;
            }
        }
    }
}
