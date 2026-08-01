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
            var hand = new GameObject("HandMarker");
            hand.transform.SetParent(player.transform, false);
            hand.transform.localPosition = Du02Profile.HandLocalPosition;
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

            Assert.That(hand.transform.localPosition, Is.EqualTo(Du02Profile.HandLocalPosition));
            Assert.That(hand.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(hand.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(player.transform.Find("BodyVisual"), Is.Not.Null);
            Assert.That(hand.transform.Find("HandVisual"), Is.Not.Null);
            Assert.That(player.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(player.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(reachLine.positionCount, Is.EqualTo(64));
            Assert.That(reachLine.loop, Is.True);
            Assert.That(visuals.ReachVisible, Is.False);

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }
    }
}
