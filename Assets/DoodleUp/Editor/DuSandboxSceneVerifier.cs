using System;
using System.Linq;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleUp.Editor
{
    public static class DuSandboxSceneVerifier
    {
        [MenuItem("Doodle Up/DU-03BC/Verify Sandbox Structure")]
        public static void VerifySandboxStructure()
        {
            var scene = EditorSceneManager.OpenScene(DuSandboxSceneBuilder.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var all = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();

            Require(all.Count(transform => transform.name == DuSandboxSceneBuilder.FloorName) == 1, "floor count must be 1");
            Require(all.Count(transform => transform.name == "Player") == 1, "player count must be 1");
            Require(all.All(transform => transform.name != "GoalZone"), "GoalZone must not exist");
            Require(all.All(transform => transform.name != "StartLedge"), "course ledges must not exist");
            Require(all.All(transform => transform.name != "GoalLedge"), "course ledges must not exist");
            Require(all.All(transform => transform.name != "T1Horizontal"), "T1 lane must not exist");
            Require(all.All(transform => transform.name != "T2Rising"), "T2 lane must not exist");
            Require(all.All(transform => transform.name != "T3Bridge"), "T3 lane must not exist");
            Require(roots.SelectMany(root => root.GetComponentsInChildren<DuSandboxController>(true)).Count() == 1, "sandbox controller count must be 1");
            Require(roots.SelectMany(root => root.GetComponentsInChildren<Du03BCArmDirectInputAdapter>(true)).Count() == 1, "Arm Direct adapter count must be 1");
            Require(roots.SelectMany(root => root.GetComponentsInChildren<Du03BCAdapterRouter>(true)).Single().ActiveRoute == Du03BCAdapterRoute.ArmDirect, "sandbox start route must be ArmDirect");
            Require(roots.SelectMany(root => root.GetComponentsInChildren<Du02RuntimeProbeRunner>(true)).Any() == false, "DU-02 evidence runner must not exist");
            Require(roots.SelectMany(root => root.GetComponentsInChildren<Du03ARuntimeProbeRunner>(true)).Any() == false, "DU-03A evidence runner must not exist");
            Require(roots.SelectMany(root => root.GetComponentsInChildren<Du03BCRuntimeProbeRunner>(true)).Any() == false, "DU-03BC evidence runner must not exist");

            var player = all.Single(transform => transform.name == "Player");
            var bodyYawAnchor = all.Single(transform => transform.name == Du02CameraRig.BodyYawAnchorName);
            var armPitchAnchor = all.Single(transform => transform.name == Du02CameraRig.ArmPitchAnchorName);
            var handMarker = all.Single(transform => transform.name == "HandMarker");
            var bodyVisual = all.Single(transform => transform.name == "BodyVisual");
            var armVisualRoot = all.Single(transform => transform.name == "ArmVisualRoot");
            var upperArmVisual = all.Single(transform => transform.name == "UpperArmVisual");
            var forearmVisual = all.Single(transform => transform.name == "ForearmVisual");
            Require(all.All(transform => transform.name != "HandVisual"), "legacy isolated HandVisual sphere must not exist");
            Require(all.Count(transform => transform.name == "PalmVisual") == 1, "PalmVisual count must be 1");
            Require(all.Count(transform => transform.name == "FingerIndexVisual") == 1, "FingerIndexVisual count must be 1");
            Require(all.Count(transform => transform.name == "FingerMiddleVisual") == 1, "FingerMiddleVisual count must be 1");
            Require(all.Count(transform => transform.name == "FingerRingVisual") == 1, "FingerRingVisual count must be 1");
            Require(all.Count(transform => transform.name == "ThumbVisual") == 1, "ThumbVisual count must be 1");
            Require(bodyYawAnchor.parent == player, "BodyYawAnchor must be a direct Player child");
            Require(armPitchAnchor.parent == bodyYawAnchor, "ArmPitchAnchor must be body-yaw local");
            Require(handMarker.parent == armPitchAnchor, "HandMarker must be arm-pitch local");
            Require(bodyVisual.parent == bodyYawAnchor, "BodyVisual must be body-yaw local");
            Require(armVisualRoot.parent == armPitchAnchor, "ArmVisualRoot must be arm-pitch local");
            Require(upperArmVisual.parent == armVisualRoot, "UpperArmVisual must attach to ArmVisualRoot");
            Require(forearmVisual.parent == armVisualRoot, "ForearmVisual must attach to ArmVisualRoot");
            Require(armVisualRoot.GetComponentsInChildren<Collider>(true).Length == 0, "arm visuals must add no physics");
            Require(handMarker.GetComponentsInChildren<Collider>(true).Length == 0, "hand visuals must add no physics");
            var capsule = player.GetComponent<CapsuleCollider>();
            var floor = all.Single(transform => transform.name == DuSandboxSceneBuilder.FloorName);
            var capsuleBottom = player.position.y + capsule.center.y - capsule.height * 0.5f;
            var floorTop = floor.position.y + floor.localScale.y * 0.5f;
            Require(Mathf.Abs(capsuleBottom - floorTop) <= 0.000001f, "spawn capsule bottom must match floor top");

            Debug.Log(
                $"[DU_SANDBOX_VERIFY] scene={DuSandboxSceneBuilder.ScenePath} " +
                $"profiles={DuSandboxController.ProfileId}|{Du03BCArmDirectInputAdapter.ProfileId} " +
                $"floor=1 player=1 bodyYawAnchor=1 armPitchAnchor=1 armDirect=1 armVisual=upper+forearm+palm forbiddenStructures=0 evidenceRunners=0 " +
                $"spawnGroundGap={Mathf.Abs(capsuleBottom - floorTop):F6} result=PASS");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"DU Sandbox verification failed: {message}.");
        }
    }
}
