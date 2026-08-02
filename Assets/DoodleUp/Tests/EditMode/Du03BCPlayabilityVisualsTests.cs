using DoodleUp.Core;
using DoodleUp.Runtime;
using DoodleUp.Stroke;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class Du03BCPlayabilityVisualsTests
    {
        [Test]
        public void FirstPersonPlayabilityVisualsKeepHandPoseAndAddNoPhysics()
        {
            var root = new GameObject("playability-root");
            var player = new GameObject("Player");
            var pitchAnchor = new GameObject(Du02CameraRig.ArmPitchAnchorName);
            pitchAnchor.transform.SetParent(player.transform, false);
            pitchAnchor.transform.localPosition = Du02CameraRig.ArmPitchAnchorLocalPosition;
            var hand = new GameObject("HandMarker");
            hand.transform.SetParent(pitchAnchor.transform, false);
            hand.transform.localPosition = Du03BCArmDirectInputAdapter.NeutralHandPitchLocalPosition;
            hand.transform.localRotation = Quaternion.identity;
            hand.transform.localScale = Vector3.one;
            hand.AddComponent<MeshRenderer>();
            hand.AddComponent<MeshFilter>();
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            var driver = root.AddComponent<Du03AStrokeDriver>();
            driver.Configure(hand.transform, camera, null, "owner", Du03AStrokeMode.Aim);
            var reachObject = new GameObject("Reach");
            reachObject.transform.SetParent(root.transform, false);
            var reachLine = reachObject.AddComponent<LineRenderer>();
            var visuals = root.AddComponent<Du03BCPlayabilityVisuals>();

            visuals.Configure(hand.transform, driver, reachLine);

            Assert.That(hand.transform.localPosition, Is.EqualTo(Du03BCArmDirectInputAdapter.NeutralHandPitchLocalPosition));
            Assert.That(hand.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(hand.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(player.transform.Find("BodyVisual"), Is.Not.Null);
            var armRoot = pitchAnchor.transform.Find("ArmVisualRoot");
            Assert.That(armRoot, Is.Not.Null);
            Assert.That(armRoot.Find("UpperArmVisual"), Is.Not.Null);
            Assert.That(armRoot.Find("ForearmVisual"), Is.Not.Null);
            Assert.That(hand.transform.Find("HandVisual"), Is.Null);
            Assert.That(hand.transform.Find("PalmVisual"), Is.Not.Null);
            Assert.That(hand.transform.Find("FingerIndexVisual"), Is.Not.Null);
            Assert.That(hand.transform.Find("FingerMiddleVisual"), Is.Not.Null);
            Assert.That(hand.transform.Find("FingerRingVisual"), Is.Not.Null);
            Assert.That(hand.transform.Find("ThumbVisual"), Is.Not.Null);
            Assert.That(visuals.ArmVisualRoot, Is.EqualTo(armRoot));
            Assert.That(visuals.VisualState, Is.EqualTo(Du03BCArmVisualState.Neutral));
            Assert.That(player.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(player.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(reachLine.positionCount, Is.EqualTo(64));
            Assert.That(reachLine.loop, Is.True);
            Assert.That(visuals.ReachVisible, Is.False);

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void DrawingAlwaysUsesDrawingVisualStateAfterArmDirectReachRemoval()
        {
            var root = new GameObject("drawing-visual-root");
            var player = new GameObject("Player");
            var pitchAnchor = new GameObject(Du02CameraRig.ArmPitchAnchorName);
            pitchAnchor.transform.SetParent(player.transform, false);
            pitchAnchor.transform.localPosition = Du02CameraRig.ArmPitchAnchorLocalPosition;
            var hand = new GameObject("HandMarker");
            hand.transform.SetParent(pitchAnchor.transform, false);
            hand.transform.localPosition = Du03BCArmDirectInputAdapter.NeutralHandPitchLocalPosition;
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            var latch = root.AddComponent<DoodleUp.Input.Du03BCInputEdgeLatch>();
            var adapter = root.AddComponent<Du03BCArmDirectInputAdapter>();
            adapter.Configure(latch, hand.transform, camera);
            var driver = root.AddComponent<Du03AStrokeDriver>();
            driver.Configure(hand.transform, camera, null, "owner", Du03AStrokeMode.Spatial);
            driver.ProcessIntent(new Du03ADrawIntent(true, false, false, false, false, default));
            adapter.SetProbeMouseDelta(new Vector2(600f, 0f));
            latch.EnqueueProbeSnapshot(new DoodleUp.Input.Du03BCInputSnapshot(
                1, false, false, true, false, false, "TEST"));
            adapter.ReadIntent();
            hand.transform.position += Vector3.right;
            var reachLine = root.AddComponent<LineRenderer>();
            var visuals = root.AddComponent<Du03BCPlayabilityVisuals>();

            visuals.Configure(hand.transform, driver, reachLine);

            Assert.That(driver.Session.State, Is.EqualTo(Du03AStrokeSessionState.Drawing));
            Assert.That(visuals.VisualState, Is.EqualTo(Du03BCArmVisualState.Drawing));
            Assert.That(visuals.ReachVisible, Is.False);

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }
    }
}
