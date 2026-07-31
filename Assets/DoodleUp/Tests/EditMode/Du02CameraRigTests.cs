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
            Assert.That(player.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(rig.GameplayNormal, Is.EqualTo(Vector3.forward));

            rig.ResetPose(new Vector3(2f, 0f, 0f));

            Assert.That(rig.VisualYawOffset, Is.Zero.Within(0.0001f));
            Assert.That(camera.transform.eulerAngles.y, Is.Zero.Within(0.0001f));

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
