using System;
using System.Linq;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Editor
{
    public static class LastShiftSceneVerifier
    {
        [MenuItem("Last Shift/SP-01/Verify Sandbox Structure")]
        public static void VerifySandboxStructure()
        {
            var scene = EditorSceneManager.OpenScene(LastShiftSceneBuilder.ScenePath, OpenSceneMode.Single);
            VerifyScene(scene);
        }

        public static void VerifySavedSandboxLifecycle()
        {
            var setup = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Require(setup.IsValid() && setup.isLoaded, "lifecycle setup scene must be loaded");
            LastShiftSceneBuilder.RebuildSandboxForAutomation();
            var savedScene = SceneManager.GetActiveScene();
            Require(savedScene.path == LastShiftSceneBuilder.ScenePath, "rebuilt scene path mismatch");
            Require(!savedScene.isDirty, "rebuilt scene must be saved");
            VerifyScene(savedScene);

            var reopened = EditorSceneManager.OpenScene(LastShiftSceneBuilder.ScenePath, OpenSceneMode.Single);
            Require(reopened.IsValid() && reopened.isLoaded, "saved scene must reopen");
            Require(!reopened.isDirty, "reopened scene must be clean");
            VerifyScene(reopened);
            Debug.Log($"[LAST_SHIFT_LIFECYCLE] scene={LastShiftSceneBuilder.ScenePath} rebuild=PASS reopen=PASS result=PASS");
        }

        public static void VerifyScene(Scene scene)
        {
            Require(scene.IsValid() && scene.isLoaded, "scene must be loaded");
            Require(scene.path == LastShiftSceneBuilder.ScenePath, "scene path mismatch");
            Require(SceneManager.GetActiveScene() == scene, "SP-01 scene must be active");
            Require(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == LastShiftSceneBuilder.ScenePath), "SP-01 scene must be enabled in build settings");

            var roots = scene.GetRootGameObjects();
            Require(roots.All(root => root.activeInHierarchy), "all root objects must be active");
            var all = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            Require(all.Count(x => x.name == LastShiftSceneBuilder.CockpitZoneName) == 1, "cockpit zone count must be 1");
            Require(all.Count(x => x.name == LastShiftSceneBuilder.UtilityZoneName) == 1, "utility zone count must be 1");
            Require(all.Count(x => x.name == LastShiftSceneBuilder.LifeSupportZoneName) == 1, "life support zone count must be 1");
            Require(all.Count(x => x.name == "PlayerOne") == 1, "player one count must be 1");
            Require(all.All(x => x.name != "PlayerTwo"), "SP-01 must not contain player two");
            Require(all.Count(x => x.name == "CanonicalMeteorStimulus") == 1, "canonical meteor count must be 1");
            var meteorVisual = all.Single(x => x.name == "CanonicalMeteorStimulus");
            var meteor = LastShiftMeteorStimulus.Canonical;
            Require(Vector3.Distance(meteorVisual.position, meteor.ImpactPoint - meteor.ImpactVector * 2f) < 0.0001f, "canonical meteor visual origin mismatch");

            var sandboxes = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).ToArray();
            Require(sandboxes.Length == 1, "sandbox controller count must be 1");
            Require(sandboxes[0].isActiveAndEnabled, "sandbox controller must be active and enabled");
            Require(sandboxes[0].Players != null && sandboxes[0].Players.Length == 1, "sandbox player wiring must contain exactly player one");
            Require(sandboxes[0].Items != null && sandboxes[0].Items.Length == 4, "sandbox item wiring must contain exactly four items");

            var players = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftPlayerController>(true)).ToArray();
            Require(players.Length == 1, "player controller count must be 1");
            var player = players[0];
            Require(player.isActiveAndEnabled, "player controller must be active and enabled");
            Require(player.PlayerSlot == LastShiftPlayerSlot.PlayerOne, "solo player must use player one slot");
            Require(player.GetComponent<CharacterController>() != null, "player CharacterController missing");
            Require(player.TargetCamera != null && player.TargetCamera.isActiveAndEnabled, "active player camera missing");
            Require(player.TargetCamera.CompareTag("MainCamera"), "solo camera must be MainCamera");
            Require(player.TargetCamera.rect == new Rect(0f, 0f, 1f, 1f), "solo camera viewport mismatch");
            Require(player.HoldSocket != null && player.HoldSocket.IsChildOf(player.TargetCamera.transform), "player hold socket wiring mismatch");

            var items = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftGrabbable>(true)).ToArray();
            Require(items.Length == 4, "grabbable item count must be 4");
            foreach (LastShiftItemRole role in Enum.GetValues(typeof(LastShiftItemRole)))
                Require(items.Count(item => item.Role == role) == 1, $"{role} count must be 1");
            Require(items.All(item => item.isActiveAndEnabled), "all grabbables must be active and enabled");
            Require(items.All(item => item.GetComponent<Rigidbody>() != null), "grabbable Rigidbody missing");
            Require(items.All(item => item.GetComponent<Collider>() != null), "grabbable Collider missing");
            Require(items.All(item => item.GetComponent<Collider>().enabled), "grabbable Collider must be enabled");
            Require(items.All(item => item.Body != null), "grabbable Rigidbody cache not configured");
            Require(items.All(item => item.NominalPosition == item.transform.position), "grabbable nominal position must match saved scene position");
            Require(items.All(item => item.Secured == item.Body.isKinematic), "secured/kinematic wiring mismatch");
            Require(sandboxes[0].Players[0] == player, "sandbox player reference mismatch");
            Require(sandboxes[0].Items.All(item => items.Contains(item)), "sandbox item references must match scene grabbables");
            Require(roots.SelectMany(root => root.GetComponentsInChildren<DoodleUp.Stroke.Du03AStrokeDriver>(true)).Any() == false, "drawing runtime must not be coupled to SP-01");
            VerifyZoneDoors(roots);
            var sightRange = VerifyCameraCoversTheShip(roots, player.TargetCamera);
            Debug.Log($"[LAST_SHIFT_VERIFY] scene={LastShiftSceneBuilder.ScenePath} active=1 zones=3 players=1 cameras=1 sockets=1 items=4 rigidbodies=4 colliders=4 meteor=1 doors=2 drawingDependency=0 farClip={player.TargetCamera.farClipPlane:F0} sightRange={sightRange:F1} result=PASS");
        }

        /// <summary>
        /// 카메라 far clip 이 씬을 실제로 덮는가. 배 대각선을 손으로 계산해 비교하면 안 된다 —
        /// 화면에서 잘리는 것은 선체가 아니라 창밖 우주판과 별이고, 그것들은 선체 바깥에
        /// 따로 놓여 있어 선체 치수에서 나오지 않는다. 그래서 씬에 실제로 놓인 오브젝트의
        /// 좌표 범위를 재고, 승무원이 설 수 있는 가장 먼 자리에서 가장 먼 오브젝트까지의
        /// 거리를 기준으로 삼는다.
        ///
        /// 반환값은 그 최대 시거리다. 로그에 남겨 두면 다음에 배를 키울 때 far clip 여유가
        /// 얼마나 남았는지 계산 없이 보인다.
        /// </summary>
        private static float VerifyCameraCoversTheShip(GameObject[] roots, Camera camera)
        {
            var all = roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true)).ToArray();
            Require(all.Length > 0, "scene must contain renderers");
            var scene = all[0].bounds;
            foreach (var renderer in all) scene.Encapsulate(renderer.bounds);

            // 승무원이 설 수 있는 가장 먼 두 자리. 눈높이는 카메라 로컬 y 를 그대로 쓴다.
            var eyeHeight = camera.transform.localPosition.y;
            var corners = new[]
            {
                new Vector3(-LastShiftShipDimensions.HalfLength, eyeHeight, -LastShiftShipDimensions.HalfWidth),
                new Vector3(-LastShiftShipDimensions.HalfLength, eyeHeight, LastShiftShipDimensions.HalfWidth),
                new Vector3(LastShiftShipDimensions.HalfLength, eyeHeight, -LastShiftShipDimensions.HalfWidth),
                new Vector3(LastShiftShipDimensions.HalfLength, eyeHeight, LastShiftShipDimensions.HalfWidth)
            };
            var sightRange = 0f;
            foreach (var eye in corners)
            {
                // 축마다 눈에서 먼 쪽 끝을 고르면 그 자리에서 보이는 가장 먼 점이 된다.
                var farthest = new Vector3(
                    Mathf.Abs(scene.min.x - eye.x) > Mathf.Abs(scene.max.x - eye.x) ? scene.min.x : scene.max.x,
                    Mathf.Abs(scene.min.y - eye.y) > Mathf.Abs(scene.max.y - eye.y) ? scene.min.y : scene.max.y,
                    Mathf.Abs(scene.min.z - eye.z) > Mathf.Abs(scene.max.z - eye.z) ? scene.min.z : scene.max.z);
                sightRange = Mathf.Max(sightRange, Vector3.Distance(eye, farthest));
            }

            Require(camera.farClipPlane >= sightRange,
                $"camera far clip {camera.farClipPlane:F0} must cover the {sightRange:F1}m sight range");
            Require(camera.nearClipPlane < 0.1f, "camera near clip must stay under 0.1m for close item inspection");
            return sightRange;
        }

        /// <summary>
        /// N0b 문 배치. 여기서 확인하는 것은 두 가지다: 경계마다 문이 정확히 하나 있는가,
        /// 그리고 벌크헤드가 문 밖 통과 경로를 남기지 않았는가. 두 번째가 빠지면 예전처럼
        /// 벌크헤드 옆으로 걸어서 지나갈 수 있고, 그러면 격리가 압력만 끊는 반쪽이 된다.
        /// </summary>
        private static void VerifyZoneDoors(GameObject[] roots)
        {
            var doors = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftZoneDoor>(true)).ToArray();
            Require(doors.Length == LastShiftZoneAtlas.BoundaryCount, "zone door count must equal boundary count");
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var matching = doors.Where(door => door.Boundary == boundary).ToArray();
                Require(matching.Length == 1, $"boundary {boundary} must have exactly one door");
                var door = matching[0];
                Require(Mathf.Abs(door.transform.position.x - LastShiftZoneAtlas.BoundaryX(boundary)) < 0.0001f,
                    $"boundary {boundary} door must sit on the boundary plane");

                // 문 옆 통과 경로 검사. 벌크헤드 조각의 z 범위 합이 선체 안쪽 폭에서
                // 문 구멍을 뺀 만큼을 덮어야 한다. 기준 폭은 치수 정본에서 가져온다 —
                // 리터럴로 두면 배 폭을 넓힐 때 이 검사만 옛 폭을 계속 통과시킨다.
                var panels = roots
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(x => x.name.StartsWith("Bulkhead_") &&
                                Mathf.Abs(x.position.x - LastShiftZoneAtlas.BoundaryX(boundary)) < 0.0001f &&
                                !x.name.EndsWith("_Lintel"))
                    .ToArray();
                Require(panels.Length == 2, $"boundary {boundary} must have two side panels beside the opening");
                var covered = panels.Sum(panel => panel.localScale.z);
                Require(covered >= LastShiftShipDimensions.InteriorWidth - LastShiftZoneDoor.OpeningWidth - 0.0001f,
                    $"boundary {boundary} bulkhead must leave no walkable gap beside the door");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"LAST SHIFT SP-01 verification failed: {message}.");
        }
    }
}
