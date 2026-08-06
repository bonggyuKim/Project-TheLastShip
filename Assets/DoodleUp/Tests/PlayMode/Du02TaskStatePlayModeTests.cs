using System.Collections;
using DoodleUp.Core;
using DoodleUp.Physics;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    public sealed class Du02TaskStatePlayModeTests
    {
        [UnityTest]
        public IEnumerator CountdownUnlocksAtGoAndTimerStartsAfterward()
        {
            var gameObject = new GameObject("TaskStateTest");
            var state = gameObject.AddComponent<Du02TaskState>();
            state.ResetState(Du02TaskId.T1Horizontal);

            Assert.That(state.InputLocked, Is.True);
            Assert.That(state.CountdownRemaining, Is.EqualTo(3f));
            Assert.That(state.TimerSeconds, Is.Zero);

            state.Tick(3f);
            Assert.That(state.InputLocked, Is.False);
            Assert.That(state.TimerSeconds, Is.Zero);
            state.Tick(0.25f);
            Assert.That(state.TimerSeconds, Is.EqualTo(0.25f));

            Object.Destroy(gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllLaneSpawnsAreGroundedOnFirstPhysicsStep()
        {
            foreach (var taskId in new[]
                     {
                         Du02TaskId.T1Horizontal,
                         Du02TaskId.T2Rising,
                         Du02TaskId.T3Bridge
                     })
            {
                var lane = Du02CourseDefinition.Get(taskId);
                var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ledge.name = $"GroundProbeLedge-{taskId}";
                ledge.transform.position = lane.StartCenter;
                ledge.transform.localScale = lane.StartSize;
                var player = new GameObject($"GroundProbePlayer-{taskId}");
                player.transform.position = lane.SpawnPosition;
                var body = player.AddComponent<Rigidbody>();
                body.useGravity = true;
                var capsule = player.AddComponent<CapsuleCollider>();
                capsule.radius = 0.25f;
                capsule.height = 1f;
                capsule.center = new Vector3(0f, 0.5f, 0f);
                var motor = player.AddComponent<Du02PlayerMotor>();
                UnityEngine.Physics.SyncTransforms();
                var bounds = capsule.bounds;
                Assert.That(
                    UnityEngine.Physics.Raycast(
                        bounds.center,
                        Vector3.down,
                        bounds.extents.y + 0.06f,
                        ~0,
                        QueryTriggerInteraction.Ignore),
                    Is.True,
                    taskId.ToString());

                for (var i = 0; i < 3 && !motor.IsGrounded; i++)
                    yield return new WaitForFixedUpdate();

                Assert.That(motor.IsGrounded, Is.True, taskId.ToString());
                Assert.That(body.position.y, Is.EqualTo(lane.SpawnPosition.y).Within(0.001f), taskId.ToString());
                Object.Destroy(player);
                Object.Destroy(ledge);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator OffsetCapsuleGroundingEnablesMovementAndJump()
        {
            var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name = "GroundProbeLedge";
            ledge.transform.position = Vector3.zero;
            ledge.transform.localScale = new Vector3(4f, 0.2f, 2f);
            var player = new GameObject("GroundProbePlayer");
            player.transform.position = new Vector3(0f, 0.1f, 0f);
            var body = player.AddComponent<Rigidbody>();
            body.useGravity = true;
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.radius = 0.25f;
            capsule.height = 1f;
            capsule.center = new Vector3(0f, 0.5f, 0f);
            var motor = player.AddComponent<Du02PlayerMotor>();

            for (var i = 0; i < 30 && !motor.IsGrounded; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(motor.IsGrounded, Is.True);
            var beforeMove = body.position.x;
            motor.SetInput(1f, false);
            yield return new WaitForFixedUpdate();
            Assert.That(body.position.x, Is.GreaterThan(beforeMove));
            Assert.That(body.linearVelocity.x, Is.EqualTo(Du02Profile.GroundSpeed).Within(0.0001f));

            var afterMove = body.position.x;
            motor.SetInput(-1f, false);
            yield return new WaitForFixedUpdate();
            Assert.That(body.position.x, Is.LessThan(afterMove));
            Assert.That(body.linearVelocity.x, Is.EqualTo(-Du02Profile.GroundSpeed).Within(0.0001f));

            motor.SetInput(0f, true);
            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
            yield return new WaitForFixedUpdate();
            Assert.That(motor.IsGrounded, Is.False);

            Object.Destroy(player);
            Object.Destroy(ledge);
        }

        [UnityTest]
        public IEnumerator DepthLocomotionClampsDiagonalAndCanBeLocked()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "DepthLocomotionFloor";
            floor.transform.localScale = new Vector3(8f, 0.2f, 8f);
            var player = new GameObject("DepthLocomotionPlayer");
            player.transform.position = new Vector3(0f, 0.1f, 0f);
            var body = player.AddComponent<Rigidbody>();
            body.useGravity = true;
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.radius = 0.25f;
            capsule.height = 1f;
            capsule.center = new Vector3(0f, 0.5f, 0f);
            var motor = player.AddComponent<Du02PlayerMotor>();

            for (var i = 0; i < 30 && !motor.IsGrounded; i++)
                yield return new WaitForFixedUpdate();

            // 30 스텝을 다 써도 조용히 통과하던 자리다(game-qa 관측). 아래 검사가
            // GroundSpeed 를 요구하므로 접지가 안 된 채 넘어가면 AirSpeed 가 잡혀
            // 원인을 알기 어려운 실패가 난다. 같은 파일 :73 / :101 은 이미 걸려 있다.
            Assert.That(motor.IsGrounded, Is.True,
                "30 스텝 안에 접지하지 못했다 — 아래 GroundSpeed 검사의 전제다.");

            motor.SetInput(1f, 1f, false, true);
            yield return new WaitForFixedUpdate();
            var horizontalSpeed = new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude;
            Assert.That(horizontalSpeed, Is.EqualTo(Du02Profile.GroundSpeed).Within(0.0001f));
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(body.linearVelocity.z, Is.GreaterThan(0f));

            var lockedDepth = body.position.z;
            motor.SetDepthLocomotionAllowed(false);
            Assert.That(body.constraints & RigidbodyConstraints.FreezePositionZ, Is.Not.Zero);
            motor.SetInput(1f, 1f, false, false);
            yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.x, Is.EqualTo(Du02Profile.GroundSpeed).Within(0.0001f));
            Assert.That(body.linearVelocity.z, Is.Zero.Within(0.0001f));
            Assert.That(body.position.z, Is.EqualTo(lockedDepth).Within(0.0001f));

            Object.Destroy(player);
            Object.Destroy(floor);
        }

        [UnityTest]
        public IEnumerator GoalSuccessRequiresStrokeEvidenceAndT3BothBands()
        {
            var gameObject = new GameObject("TaskStateTest");
            var state = gameObject.AddComponent<Du02TaskState>();

            state.ResetState(Du02TaskId.T1Horizontal);
            state.Tick(3f);
            state.SetInsideGoal(true);
            state.Tick(1f);
            Assert.That(state.GoalReached, Is.False);
            state.NotifyCommittedStrokeContact(false, false);
            state.Tick(1f);
            Assert.That(state.GoalReached, Is.True);

            state.ResetState(Du02TaskId.T3Bridge);
            state.Tick(3f);
            state.NotifyCommittedStrokeContact(true, false);
            state.SetInsideGoal(true);
            state.Tick(1f);
            Assert.That(state.GoalReached, Is.False);

            state.ResetState(Du02TaskId.T3Bridge);
            state.Tick(3f);
            state.NotifyCommittedStrokeContact(true, true);
            state.SetInsideGoal(true);
            state.Tick(1f);
            Assert.That(state.GoalReached, Is.True);

            Object.Destroy(gameObject);
            yield return null;
        }
    }
}
