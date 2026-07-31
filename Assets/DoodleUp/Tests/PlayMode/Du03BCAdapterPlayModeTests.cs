using System.Collections;
using DoodleUp.Input;
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
        public IEnumerator ReleaseFrameProcessesCandidateBeforeRelease()
        {
            var setup = CreateSetup(Du03AStrokeMode.Trajectory);
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(1, true, false, true, false, false, "PLAYMODE"));
            yield return null;
            setup.Player.transform.position += Vector3.right * 0.24f;
            Du03ALateUpdateEvidence releaseEvidence = default;
            setup.Driver.LateUpdateProcessed += value => releaseEvidence = value;
            setup.Latch.EnqueueProbeSnapshot(new Du03BCInputSnapshot(2, false, true, false, false, false, "PLAYMODE"));

            yield return null;

            Assert.That(releaseEvidence.EventOrder, Is.EqualTo("CANDIDATE>RELEASE"));
            Assert.That(releaseEvidence.CandidateCountThisFrame, Is.EqualTo(1));
            Assert.That(setup.Driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Pending));
            Object.Destroy(setup.Root);
        }

        [UnityTest]
        public IEnumerator DrawingConsumesExactlyOneCandidatePerFrame()
        {
            var setup = CreateSetup(Du03AStrokeMode.Trajectory);
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
        public IEnumerator ResetClearsHeldEdgeAndStrokeState()
        {
            var setup = CreateSetup(Du03AStrokeMode.Trajectory);
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

        private static Setup CreateSetup(Du03AStrokeMode mode)
        {
            var root = new GameObject("DU03BC-PlayMode");
            var player = new GameObject("Player");
            player.transform.SetParent(root.transform, false);
            var hand = new GameObject("HandMarker");
            hand.transform.SetParent(player.transform, false);
            hand.transform.localPosition = new Vector3(0.35f, 0.8f, 0f);
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
            var router = root.AddComponent<Du03BCAdapterRouter>();
            router.Configure(deterministic, aim, trajectory);
            router.SetRoute(mode == Du03AStrokeMode.Aim ? Du03BCAdapterRoute.Aim : Du03BCAdapterRoute.Trajectory);
            var driver = root.AddComponent<Du03AStrokeDriver>();
            driver.Configure(hand.transform, camera, router, "test-owner", mode);
            router.SetStrokeDriver(driver);
            return new Setup(root, player, latch, router, driver);
        }

        private readonly struct Setup
        {
            public readonly GameObject Root;
            public readonly GameObject Player;
            public readonly Du03BCInputEdgeLatch Latch;
            public readonly Du03BCAdapterRouter Router;
            public readonly Du03AStrokeDriver Driver;

            public Setup(GameObject root, GameObject player, Du03BCInputEdgeLatch latch, Du03BCAdapterRouter router, Du03AStrokeDriver driver)
            {
                Root = root;
                Player = player;
                Latch = latch;
                Router = router;
                Driver = driver;
            }
        }
    }
}
