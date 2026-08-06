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

            yield return WaitUntilGrounded(setup);

            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;
            var lockedDepth = setup.Player.transform.position.z;

            // 수평 이동과 점프를 <b>따로</b> 본다. 한 호출에 섞으면 검사가 불안정해진다 —
            // 한 프레임에 fixed 스텝이 둘 이상 도는 경우, 첫 스텝이 점프로 몸을 띄우고 둘째
            // 스텝은 이미 공중이라 speed 가 GroundSpeed(2.5) 대신 AirSpeed(2.0) 로 잡힌다.
            // 스텝이 몇 번 도는지는 프레임 길이에 달렸고 -nographics 에서는 특히 흔들린다
            // (QA 실측: 이 자리에서 2.5 기대에 2.0 관측). 점프를 빼면 접지가 유지되므로
            // 스텝이 몇 번 돌든 수평 속도는 GroundSpeed 하나로 고정된다.
            yield return DriveProbeMovement(setup, 1f, 1f, false);

            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Drawing));
            Assert.That(setup.Controller.DepthMovementLocked, Is.True);
            Assert.That(setup.Player.transform.position.z, Is.EqualTo(lockedDepth).Within(0.001f));
            Assert.That(setup.Motor.Velocity.z, Is.Zero.Within(0.0001f));
            Assert.That(setup.Motor.Velocity.x, Is.EqualTo(Du02Profile.GroundSpeed).Within(0.0001f));

            // 점프는 뜬 사실만 본다. 스텝이 몇 번 돌아도 한 스텝의 중력(약 0.196m/s)으로는
            // JumpSpeed 가 부호를 잃지 않으므로 이 assertion 은 스텝 수에 흔들리지 않는다.
            yield return DriveProbeMovement(setup, 1f, 1f, true);
            Assert.That(setup.Motor.Velocity.y, Is.GreaterThan(0f));
            Assert.That(setup.Controller.DepthMovementLocked, Is.True);
            Assert.That(setup.Player.transform.position.z, Is.EqualTo(lockedDepth).Within(0.001f));

            setup.Player.transform.position += Vector3.right * 0.24f;
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(2, false, true, false, false, false, "PLAYMODE"));
            yield return null;
            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Idle));

            yield return DriveProbeMovement(setup, 0f, 1f, false);
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

            yield return WaitUntilGrounded(setup);

            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;
            yield return DriveProbeMovement(setup, 1f, 1f, true);

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

        /// <summary>
        /// probe 이동 입력을 넣고, <b>그 입력을 실제로 먹은 fixed 스텝</b>까지 진행한다.
        ///
        /// 예전에는 <c>ApplyMovementForProbe</c> 뒤에 <c>WaitForFixedUpdate</c> 하나만 두었는데,
        /// 그 둘 사이에 <c>DuSandboxController.Update</c>(실행순서 200)가 끼면 입력이 지워진다 —
        /// batchmode 에는 장치가 없어 실제 입력이 항상 <c>(0,0,false)</c> 이기 때문이다.
        /// <see cref="Du02PlayerMotor"/> 가 매 스텝 velocity 를 입력에서 재계산하므로 그 스텝의
        /// 속도가 정확히 <c>0</c> 이 되고, 검사는 무작위로 실패한다(QA 실측 10회 중 5회).
        ///
        /// <b>보장되는 순서를 쓴다.</b> <c>yield return null</c> 로 재개하면 그 프레임의
        /// <c>Update</c> 는 이미 지났고 남은 것은 <c>LateUpdate</c> 뿐이다. 다음 프레임에
        /// fixed 스텝이 있으면 그 스텝은 <c>Update</c> 보다 먼저 돌므로 입력이 살아서 도착한다.
        /// 문제는 프레임이 <c>fixedDeltaTime</c> 보다 짧아 <b>다음 프레임에 스텝이 없는</b>
        /// 경우뿐이고, 그건 프레임 번호로 구분된다 — 스텝이 제때 돌았다면 재개 시점이 정확히
        /// 다음 프레임이다. 아니면 <c>Update</c> 가 한 번 이상 끼었다는 뜻이므로 다시 넣는다.
        ///
        /// 다시 넣는 것이 안전한 이유: 입력이 지워진 경로에서는 그 스텝이 <c>jumpRequested</c>
        /// 도 <c>false</c> 로 받았으므로 점프가 소모되지 않았다. 그래서 재시도가 점프를 두 번
        /// 쓰지 않는다. 이 성질 덕분에 "점프가 앞선 스텝에서 소모돼 검사 스텝에서는 이미 공중"
        /// 이라 <c>AirSpeed</c> 가 잡히던 실패(QA L233)도 함께 사라진다.
        /// </summary>
        private static IEnumerator DriveProbeMovement(Setup setup, float horizontal, float forward, bool jump)
        {
            for (var attempt = 0; attempt < 120; attempt++)
            {
                var appliedFrame = Time.frameCount;
                setup.Controller.ApplyMovementForProbe(horizontal, forward, jump);
                yield return new WaitForFixedUpdate();
                if (Time.frameCount == appliedFrame + 1) yield break;
                yield return null;   // Update 가 끼었다 — 그 Update 뒤에서 다시 넣는다
            }

            Assert.Fail("probe 입력을 먹은 fixed 스텝에 도달하지 못했다.");
        }

        /// <summary>
        /// 접지까지 기다린다. 예전 <c>for (i &lt; 30 &amp;&amp; !IsGrounded)</c> 루프는 30 스텝을
        /// 다 써도 조용히 통과해서, 접지 전제가 깨진 채 본문이 진행될 수 있었다(QA 부수 관찰).
        /// </summary>
        private static IEnumerator WaitUntilGrounded(Setup setup)
        {
            for (var step = 0; step < 30 && !setup.Motor.IsGrounded; step++)
                yield return new WaitForFixedUpdate();

            Assert.That(setup.Motor.IsGrounded, Is.True,
                "30 스텝 안에 접지하지 못했다 — 이 검사는 접지 상태를 전제한다.");
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
