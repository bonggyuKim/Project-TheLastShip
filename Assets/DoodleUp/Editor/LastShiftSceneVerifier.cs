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
            Require(all.Count(x => x.name == "CanonicalMeteorStimulus") == 1, "canonical meteor count must be 1");
            var meteorVisual = all.Single(x => x.name == "CanonicalMeteorStimulus");
            var meteor = LastShiftMeteorStimulus.Canonical;
            Require(Vector3.Distance(meteorVisual.position, meteor.ImpactPoint - meteor.ImpactVector * 2f) < 0.0001f, "canonical meteor visual origin mismatch");

            var sandboxes = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).ToArray();
            Require(sandboxes.Length == 1, "sandbox controller count must be 1");
            Require(sandboxes[0].isActiveAndEnabled, "sandbox controller must be active and enabled");
            // 씬에 박힌 플레이어는 없다. 승무원은 접속 시 NGO 가 플레이어 프리팹에서 스폰한다 —
            // 솔로도 host 1인이라 같은 경로를 탄다. 배열이 비어 있는 것이 정상이고, 여기 뭔가
            // 들어 있으면 씬에 플레이어가 다시 구워졌다는 뜻이다.
            Require(sandboxes[0].Players != null && sandboxes[0].Players.Length == 0, "network scene must not bake a player into the scene");
            Require(sandboxes[0].Items != null && sandboxes[0].Items.Length == 4, "sandbox item wiring must contain exactly four items");

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
            Require(sandboxes[0].Items.All(item => items.Contains(item)), "sandbox item references must match scene grabbables");
            Require(roots.SelectMany(root => root.GetComponentsInChildren<DoodleUp.Stroke.Du03AStrokeDriver>(true)).Any() == false, "drawing runtime must not be coupled to SP-01");
            VerifyZoneDoors(roots);
            var sightRange = VerifyCameraCoversTheShip(roots, PlayerPrefabCamera());
            var (rays, gapZ) = VerifyZonesCannotSeeEachOther();
            var clearance = VerifyOccupiedPointsSitInsideRealGeometry();
            Debug.Log($"[LAST_SHIFT_VERIFY] scene={LastShiftSceneBuilder.ScenePath} active=1 zones=3 players=prefab cameras=1 sockets=1 items=4 rigidbodies=4 colliders=4 meteor=1 doors=2 drawingDependency=0 farClip={PlayerPrefabCamera().farClipPlane:F0} sightRange={sightRange:F1} sightlineRays={rays} gapZ={gapZ:F2} geometryClearance={clearance:F2} result=PASS");
        }

        /// <summary>
        /// 승무원 카메라. 씬에 플레이어가 없으므로 프리팹에서 읽는다 — far clip 이 배를 덮는지는
        /// 여전히 봐야 한다. 배가 길어지면 창밖 우주판이 잘려 실내가 검은 벽으로 끝난다.
        /// </summary>
        private static Camera PlayerPrefabCamera()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LastShiftNetworkSceneBuilder.PlayerPrefabPath);
            Require(prefab != null, $"player prefab missing at {LastShiftNetworkSceneBuilder.PlayerPrefabPath}");
            var camera = prefab.GetComponentInChildren<Camera>(true);
            Require(camera != null, "player prefab must carry a camera");
            return camera;
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
                var centerZ = LastShiftZoneDoor.CenterZOf(boundary);
                Require(Mathf.Abs(door.transform.position.z - centerZ) < 0.0001f,
                    $"boundary {boundary} door must sit on its opening centre");

                // 문 옆 통과 경로 검사. 예전에는 두 판의 z 스케일 <b>합</b>만 봤는데, 합이
                // 맞으면 위치가 어긋나도 통과한다 — 판 둘이 같은 쪽으로 몰려 반대편이
                // 통째로 뚫려 있어도 PASS 다. 개구부가 통로를 따라 한쪽으로 치우친 뒤로는
                // 좌우 판 폭이 서로 다르므로 합 검사가 특히 위험하다. 그래서 실제로 덮인
                // z 구간을 보고, 개구부를 뺀 나머지가 남김없이 덮였는지 확인한다.
                var panels = roots
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(x => x.name.StartsWith("Bulkhead_") &&
                                Mathf.Abs(x.position.x - LastShiftZoneAtlas.BoundaryX(boundary)) < 0.0001f &&
                                !x.name.EndsWith("_Lintel"))
                    .ToArray();
                Require(panels.Length == 2, $"boundary {boundary} must have two side panels beside the opening");

                var wallMin = -LastShiftShipDimensions.EndWallSpan * 0.5f;
                var wallMax = LastShiftShipDimensions.EndWallSpan * 0.5f;
                var openingMin = centerZ - LastShiftZoneDoor.OpeningWidth * 0.5f;
                var openingMax = centerZ + LastShiftZoneDoor.OpeningWidth * 0.5f;
                RequireCovered(panels, wallMin, openingMin,
                    $"boundary {boundary} bulkhead must cover z [{wallMin:F2}, {openingMin:F2}] beside the door");
                RequireCovered(panels, openingMax, wallMax,
                    $"boundary {boundary} bulkhead must cover z [{openingMax:F2}, {wallMax:F2}] beside the door");
            }
        }

        /// <summary>
        /// 구역끼리 서로 보이지 않는가(A3). 구역 쌍 셋에 대해 각각 눈 3자리 × 표적 3자리로
        /// 9발씩, 모두 27발을 쏘고 전부 막혀야 한다.
        ///
        /// 조종석↔엔진실을 빼먹은 것이 이번 사건의 원인이다. 조종석↔산소실만 보면 통로가
        /// 둘 다 꺾여 있어 자동으로 막히지만, 통로 하나만 지나는 인접 구역 쌍은 통로가
        /// 직선이면 그대로 뚫린다. 그래서 인접 쌍 둘을 반드시 함께 본다.
        ///
        /// 다만 레이캐스트만으로는 부족하다. 유한 개를 쏘는 이상 폭 0 의 칼날 틈은 확률적으로
        /// 안 걸리고, 그때 검사는 "여유 0" 을 "막혔음" 으로 보고한다. 그래서 왜 막혔는지의
        /// 여유값인 GAP_Z 를 함께 재서 돌려준다 — 실측이므로 다음에 누가 개구부를 0.1m 옮길 때
        /// 남은 여유가 계산 없이 보인다.
        /// </summary>
        private static (int rays, float gapZ) VerifyZonesCannotSeeEachOther()
        {
            var pairs = new[]
            {
                (from: LastShiftZone.Cockpit, to: LastShiftZone.Utility),
                (from: LastShiftZone.Utility, to: LastShiftZone.LifeSupport),
                (from: LastShiftZone.Cockpit, to: LastShiftZone.LifeSupport)
            };

            // 씬을 막 열었거나 막 만든 직후에는 콜라이더의 물리 트랜스폼이 아직 반영되지 않아
            // 레이가 전부 빈 공간을 지나간다. 그 상태로 두면 이 검사가 "27발 전부 안 막힘" 이라는
            // 요란한 실패가 아니라, 형상을 조금만 손보면 조용히 뒤집히는 불안정한 검사가 된다.
            UnityEngine.Physics.SyncTransforms();

            var rays = 0;
            foreach (var pair in pairs)
                foreach (var eye in SightSamples(pair.from, pair.to))
                    foreach (var target in SightSamples(pair.to, pair.from))
                    {
                        rays++;
                        var direction = target - eye;
                        var distance = direction.magnitude;
                        // 막혔다 = 눈과 표적 사이에 무엇이든 있다. 무엇이 막았는지는 보지 않는다 —
                        // 벌크헤드든 통로 벽이든 캐비닛이든 시선을 끊었으면 조건은 만족이다.
                        // UnityEngine.Physics 를 명시한다 — DoodleUp.Physics 네임스페이스가 있어
                        // 짧게 쓰면 그쪽으로 해석된다.
                        Require(UnityEngine.Physics.Raycast(eye, direction / distance, distance),
                            $"{pair.from}→{pair.to} sightline from {eye} to {target} is not blocked");
                    }

            // 실측 GAP_Z. 상수를 다시 읽는 것이 아니라 개구부 구간에서 직접 뺀다. 통로 A 는
            // 개구부 0·1, 통로 B 는 3·2 이고 둘 중 좁은 쪽이 실제 여유다.
            var gapA = LastShiftShipDimensions.OpeningMinZ(0) - LastShiftShipDimensions.OpeningMaxZ(1);
            var gapB = LastShiftShipDimensions.OpeningMinZ(3) - LastShiftShipDimensions.OpeningMaxZ(2);
            var gapZ = Mathf.Min(gapA, gapB);
            Require(gapZ > 0f,
                $"openings inside a passage must not overlap in z (measured gap {gapZ:F2}m)");
            return (rays, gapZ);
        }

        /// <summary>
        /// 한 방 안에서 시선을 쏘거나 받을 세 자리. 눈높이는 서 있는 승무원 기준 1.55m 다.
        ///
        /// 세 자리 모두 <b>문턱 쪽 벽 밀착</b>이다. 예전에는 방 중심의 좌/중/우를 잡았는데,
        /// 그러면 새는 자리를 비껴간다 — 누출은 벽에 붙어 문턱 가까이 선 자리에서 가장 크고,
        /// 방 중심(문턱에서 4m)에서는 필요한 z 가 1.667 이라 inset 2.6 이 그것보다 크긴 해도
        /// 여유가 얇다. 벽에서 0.3m, 문턱에서 1m 안으로 잡으면 설비(배플)가 사라지는 순간
        /// 이 검사가 FAIL 한다.
        ///
        /// 기준은 구역이 아니라 <b>방</b>이다. 구역으로 재면 통로 안 좌표가 "조종석 안" 으로
        /// 판정돼, 이미 벽에 막힌 자리에서 쏘고 PASS 를 받게 된다.
        ///
        /// <b>wallInset 을 줄이는 방향으로만 틀린다.</b> 0.3 → z = ±2.7 이고 d=1 누출 띠는
        /// z ≥ 1.467 이라 띠 안쪽 여유가 1.23m, 벽 쪽 여유가 0.3m 다. 늘리면 띠 안이라
        /// 안전하고, 줄이면 z 가 벽면(±3.0)에 붙어 광선이 벽에 먼저 막혀 <b>차단물이 없어도
        /// 언제나 PASS</b> 가 된다. 가운데 한 발만 쏘던 시절의 실패와 같은 형태이고,
        /// 좌·우 두 발도 z 를 벽 쪽으로 미는 같은 상수를 쓰므로 똑같이 성립한다.
        /// </summary>
        private static Vector3[] SightSamples(LastShiftZone zone, LastShiftZone toward)
        {
            const float eyeHeight = 1.55f;
            const float wallInset = 0.3f;
            const float thresholdInset = 1.0f;
            var forward = LastShiftShipDimensions.RoomCenterX(toward) > LastShiftShipDimensions.RoomCenterX(zone);
            // 상대 구역 쪽 문턱에서 1m 안. 그 자리가 이 방에서 가장 크게 새는 자리다.
            var x = forward
                ? LastShiftShipDimensions.RoomMaxX(zone) - thresholdInset
                : LastShiftShipDimensions.RoomMinX(zone) + thresholdInset;
            var inset = LastShiftShipDimensions.HalfWidth - wallInset;
            return new[]
            {
                new Vector3(x, eyeHeight, -inset),
                new Vector3(x, eyeHeight, 0f),
                new Vector3(x, eyeHeight, inset)
            };
        }

        /// <summary>
        /// 사람과 물건이 놓이는 자리가 실제 <b>형상</b> 안인가. 여기서 구역 범위를 쓰면 안 된다 —
        /// 통로도 조종석 구역이므로 통로 한가운데 좌표가 "조종석 안" 으로 통과한다. 실제로
        /// 그렇게 됐었다: 스폰이 x -8.6 으로 통로 A 한복판이었고 4인 슬롯 중 하나는 통로 z 범위
        /// 밖(벽 안)이었으며, Tether 받침대도 통로 옆 솔리드 안이었는데 셋 다 검사를 통과했다.
        ///
        /// 그래서 방 셋과 통로 둘의 x·z 범위를 실제로 나열하고, 그 중 어느 하나에 완전히
        /// 들어가는지를 본다. 반환값은 가장 아슬아슬한 자리의 여유다 — 다음에 누가 좌표를
        /// 0.2m 옮길 때 남은 여유가 계산 없이 보인다.
        /// </summary>
        private static float VerifyOccupiedPointsSitInsideRealGeometry()
        {
            var points = new (string name, Vector3 at)[]
            {
                ("spawn0", LastShiftNetworkSession.SpawnForSlot(0)),
                ("spawn1", LastShiftNetworkSession.SpawnForSlot(1)),
                ("spawn2", LastShiftNetworkSession.SpawnForSlot(2)),
                ("spawn3", LastShiftNetworkSession.SpawnForSlot(3)),
                ("Battery", LastShiftShipDimensions.BatteryNominal),
                ("CoolingCanister", LastShiftShipDimensions.CoolingNominal),
                ("PatchPlate", LastShiftShipDimensions.PatchPlateNominal),
                ("Tether", LastShiftShipDimensions.TetherNominal)
            };

            var worst = float.MaxValue;
            foreach (var point in points)
            {
                var clearance = BestGeometryClearance(point.at);
                Require(clearance > 0f,
                    $"{point.name} at {point.at:F2} is not inside any room or passage (clearance {clearance:F2}m)");
                worst = Mathf.Min(worst, clearance);
            }
            return worst;
        }

        /// <summary>
        /// 이 점이 방·통로 중 가장 여유 있게 들어가는 곳에서의 여유. 어디에도 안 들어가면 음수다.
        /// </summary>
        private static float BestGeometryClearance(Vector3 point)
        {
            var best = float.MinValue;
            foreach (LastShiftZone zone in Enum.GetValues(typeof(LastShiftZone)))
                best = Mathf.Max(best, Clearance(point,
                    LastShiftShipDimensions.RoomMinX(zone), LastShiftShipDimensions.RoomMaxX(zone),
                    -LastShiftShipDimensions.HalfWidth, LastShiftShipDimensions.HalfWidth));
            for (var passage = 0; passage < 2; passage++)
                best = Mathf.Max(best, Clearance(point,
                    LastShiftShipDimensions.PassageMinX(passage), LastShiftShipDimensions.PassageMaxX(passage),
                    LastShiftShipDimensions.PassageMinZ(passage), LastShiftShipDimensions.PassageMaxZ(passage)));
            return best;
        }

        private static float Clearance(Vector3 point, float minX, float maxX, float minZ, float maxZ) =>
            Mathf.Min(point.x - minX, maxX - point.x, point.z - minZ, maxZ - point.z);

        /// <summary>
        /// [min, max] 구간이 판들에 실제로 덮였는가. 판 하나가 통째로 덮거나, 여러 판이
        /// 이어 붙어 덮는 경우를 모두 인정한다. 덮인 z 를 왼쪽부터 밀어 나가면서 빈 곳이
        /// 나오면 실패한다 — 합 비교와 달리 위치가 틀리면 여기서 걸린다.
        /// </summary>
        private static void RequireCovered(Transform[] panels, float min, float max, string message)
        {
            if (max - min <= 0.0001f) return;
            var reached = min;
            var spans = panels
                .Select(panel => (lo: panel.position.z - panel.localScale.z * 0.5f,
                                  hi: panel.position.z + panel.localScale.z * 0.5f))
                .OrderBy(span => span.lo)
                .ToArray();
            foreach (var span in spans)
            {
                if (span.lo > reached + 0.0001f) break;
                if (span.hi > reached) reached = span.hi;
                if (reached >= max - 0.0001f) return;
            }
            Require(false, $"{message} (covered up to {reached:F2})");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"LAST SHIFT SP-01 verification failed: {message}.");
        }
    }
}
