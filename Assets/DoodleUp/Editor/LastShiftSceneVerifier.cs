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
            Require(all.Count(x => x.name == LastShiftSceneBuilder.PowerZoneName) == 1, "power zone count must be 1");
            Require(all.Count(x => x.name == LastShiftSceneBuilder.CoolingZoneName) == 1, "cooling zone count must be 1");
            Require(all.Count(x => x.name == LastShiftSceneBuilder.LifeSupportZoneName) == 1, "life support zone count must be 1");
            VerifyCockpitGlass(all);

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
            VerifyZoneDoors(roots);
            var sightRange = VerifyCameraCoversTheShip(roots, PlayerPrefabCamera());
            var (samples, worstReadings) = VerifySimultaneousZoneReadings(roots);
            var clearance = VerifyOccupiedPointsSitInsideRealGeometry();
            Debug.Log($"[LAST_SHIFT_VERIFY] scene={LastShiftSceneBuilder.ScenePath} active=1 zones={LastShiftZoneAtlas.ZoneCount} players=prefab cameras=1 sockets=1 items=4 rigidbodies=4 colliders=4 meteor=1 doors={LastShiftZoneAtlas.BoundaryCount} farClip={PlayerPrefabCamera().farClipPlane:F0} sightRange={sightRange:F1} plazaSamples={samples} simulZones={worstReadings} geometryClearance={clearance:F2} result=PASS");
        }

        private static void VerifyCockpitGlass(Transform[] all)
        {
            var glassRoot = all.SingleOrDefault(x => x.name == LastShiftSceneBuilder.CockpitGlassRootName);
            Require(glassRoot != null, "cockpit glass root must exist exactly once");

            var panes = glassRoot.GetComponentsInChildren<MeshRenderer>(true);
            Require(panes.Length > 0, "cockpit glass must contain visible panes");
            var glass = AssetDatabase.LoadAssetAtPath<Material>(LastShiftSceneBuilder.CockpitGlassMaterialPath);
            Require(glass != null, "cockpit glass material must exist");
            Require(panes.All(pane => pane.sharedMaterial == glass), "every cockpit pane must use the canonical glass material");
            Require(glassRoot.GetComponentsInChildren<Collider>(true).Length == 0,
                "cockpit glass is visual dressing and must not block traversal or interaction rays");
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
            // 발자국 전체의 네 극단. 광장 반폭으로 잡으면 안 된다 — 승무원은 조종석 선수
            // 구석(x -14)과 에어록 홀 좌현(z -12)까지 걸어가고, 카메라 far clip 은 거기서
            // 반대편 끝을 봤을 때를 견뎌야 한다.
            var corners = new[]
            {
                new Vector3(LastShiftPlazaLayout.MinX, eyeHeight, LastShiftPlazaLayout.MinZ),
                new Vector3(LastShiftPlazaLayout.MinX, eyeHeight, LastShiftPlazaLayout.MaxZ),
                new Vector3(LastShiftPlazaLayout.MaxX, eyeHeight, LastShiftPlazaLayout.MinZ),
                new Vector3(LastShiftPlazaLayout.MaxX, eyeHeight, LastShiftPlazaLayout.MaxZ)
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
        /// 압력문 배치. 여기서 확인하는 것은 두 가지다: 경계마다 문이 정확히 하나 있는가,
        /// 그리고 광장 벽이 문 밖 통과 경로를 남기지 않았는가. 두 번째가 빠지면 벽 옆으로
        /// 걸어서 지나갈 수 있고, 그러면 격리가 압력만 끊는 반쪽이 된다.
        ///
        /// <b>평면 축이 문마다 다르다.</b> 압력문 셋 중 둘(전력실·냉각실)이 <c>z</c> 평면에
        /// 서므로 좌표를 축으로 골라 재야 한다 — <c>x</c> 로 고정해 두면 두 문이 서로의
        /// 자리에서 통과한다.
        /// </summary>
        private static void VerifyZoneDoors(GameObject[] roots)
        {
            var doors = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftZoneDoor>(true)).ToArray();
            Require(doors.Length == LastShiftZoneAtlas.BoundaryCount, "zone door count must equal boundary count");

            // 벽을 <b>이름으로 찾지 않는다</b>. 예전에는 <c>PlazaWall_*</c> 를 모아 그 판의
            // 스케일 구간으로 덮임을 셌는데, 그 이름은 그레이박스 빌더가 붙이던 것이라
            // 정본 지도가 벽을 맡은 뒤로는 아무것도 안 잡혔다. 이 검사가 지키려는 것은
            // "경계 평면은 문 구멍 말고는 막혀 있다" 이지 "그 이름의 오브젝트가 있다" 가
            // 아니므로, 누가 세웠든 <b>실제 충돌 형상</b>을 본다.
            UnityEngine.Physics.SyncTransforms();

            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
            {
                var matching = doors.Where(door => door.Boundary == boundary).ToArray();
                Require(matching.Length == 1, $"boundary {boundary} must have exactly one door");
                var door = matching[0];
                var plazaDoor = LastShiftZoneAtlas.BoundaryDoor(boundary);

                var position = door.transform.position;
                var through = plazaDoor.PlaneIsX ? position.x : position.z;
                var free = plazaDoor.PlaneIsX ? position.z : position.x;
                Require(Mathf.Abs(through - plazaDoor.Plane) < 0.0001f,
                    $"boundary {boundary} door must sit on the boundary plane");
                Require(Mathf.Abs(free - plazaDoor.Center) < 0.0001f,
                    $"boundary {boundary} door must sit on its opening centre");

                // 문 옆 통과 경로 검사. 판 스케일의 <b>합</b>만 보면 위치가 어긋나도 통과한다 —
                // 판 둘이 같은 쪽으로 몰려 반대편이 통째로 뚫려 있어도 PASS 다. 그래서 실제로
                // 덮인 구간을 보고, 구멍을 뺀 나머지가 남김없이 덮였는지 확인한다.
                //
                // <b>같은 변에 문이 둘일 수 있다</b>(좌현: 전력실 + 에어록 홀). 그래서 이 변의
                // 문 전부를 모아 구멍 목록을 만들고, 그 사이 구간만 덮였는지 본다.
                var side = LastShiftPlazaLayout.Doors
                    .Where(other => other.PlaneIsX == plazaDoor.PlaneIsX &&
                                    Mathf.Abs(other.Plane - plazaDoor.Plane) < 0.0001f)
                    .OrderBy(other => other.Center)
                    .ToArray();

                var lo = plazaDoor.PlaneIsX ? LastShiftPlazaLayout.PlazaMinZ : LastShiftPlazaLayout.PlazaMinX;
                var hi = plazaDoor.PlaneIsX ? LastShiftPlazaLayout.PlazaMaxZ : LastShiftPlazaLayout.PlazaMaxX;
                var cursor = lo;
                foreach (var other in side)
                {
                    RequireSolidAlong(plazaDoor, cursor, other.MinSpan,
                        $"boundary {boundary} plane must be solid over [{cursor:F2}, {other.MinSpan:F2}] beside a door");
                    cursor = other.MaxSpan;
                }
                RequireSolidAlong(plazaDoor, cursor, hi,
                    $"boundary {boundary} plane must be solid over [{cursor:F2}, {hi:F2}] beside a door");
            }
        }

        /// <summary>
        /// 경계 평면의 <c>[from, to]</c> 구간이 실제로 막혀 있는가. 문 구멍은 호출부가 이미
        /// 빼고 넘긴다.
        ///
        /// <b>가슴 높이 한 줄만 본다.</b> 여기서 막고 싶은 것은 "승무원이 벽을 통과한다" 이고,
        /// 그 판정은 사람이 지나는 높이에서 난다. 바닥 틈이나 천장 띠는 이 검사의 몫이 아니다
        /// (그쪽은 저중력 부유물 문제라 별도 항목이다).
        ///
        /// 구간 양 끝은 <c>0.05m</c> 안쪽으로 물러나서 잰다. 딱 끝점을 재면 이웃한 벽 판과의
        /// 이음매가 표본에 걸려, 실제로는 닫혀 있는데 실패로 나온다.
        /// </summary>
        private static void RequireSolidAlong(LastShiftPlazaDoor plane, float from, float to, string message)
        {
            const float step = 0.1f;
            const float inset = 0.05f;
            var start = from + inset;
            var end = to - inset;
            if (end <= start) return;

            for (var at = start; at <= end; at += step)
            {
                var centre = plane.PlaneIsX
                    ? new Vector3(plane.Plane, 1.0f, at)
                    : new Vector3(at, 1.0f, plane.Plane);
                var extents = plane.PlaneIsX
                    ? new Vector3(0.30f, 0.20f, 0.02f)
                    : new Vector3(0.02f, 0.20f, 0.30f);
                Require(UnityEngine.Physics.OverlapBox(centre, extents, Quaternion.identity,
                            ~0, QueryTriggerInteraction.Ignore).Length > 0,
                    $"{message} — 비어 있는 지점 ({centre.x:F2}, {centre.z:F2})");
            }
        }

        /// <summary>
        /// <c>SIMUL_ZONES ≤ 2</c>(기획 정본 §4). <b>A3 "구역끼리 서로 안 보임" 을 대체한다.</b>
        ///
        /// 폐지된 이유는 위상이다 — 일자 스파인에서는 방과 방 사이에 꺾인 통로가 있어 시선
        /// 자체를 끊을 수 있었지만, 방사형에서는 방 여섯이 <b>같은 광장</b>을 보고 있어 두
        /// 구역이 서로 보이는 직선은 기하학적으로 반드시 남는다. 중앙 광장 허브가 그것을
        /// 알고 고른 대가이고, 대신 §4 가 막는 것을 <b>판독</b>으로 좁혔다: 게이지를 문틀이
        /// 아니라 문 너머 방 안쪽 끝벽에 달아 보이는 영역을 구멍을 지나는 쐐기로 줄이고,
        /// 광장 한가운데를 코어 <c>4x4</c> 로 점유해 쐐기 셋이 겹치는 자리를 없앤다.
        ///
        /// 그래서 여기서 재는 것은 레이캐스트가 아니라 <b>실제 판독 수</b>다. 격자
        /// <c>0.05m</c> 로 광장을 훑어 세 구역이 동시에 읽히는 점이 하나라도 있으면 실패다.
        /// 코어가 씬에 실제로 서 있는지도 함께 본다 — 좌표 계산만 맞고 판이 안 서 있으면
        /// 이 검사는 통과하고 실플레이는 위반이다.
        /// </summary>
        private static (int samples, int worst) VerifySimultaneousZoneReadings(GameObject[] roots)
        {
            // <b>코어는 통짜 큐브가 아니라 3면 고정 셸이다</b>(PM 승인 2026-08-11, P0).
            // 승강 샤프트가 이 자리를 쓰게 되면서 속을 열었고, 조종석 쪽 한 면만 게이트다.
            // 그래서 여기서 재는 것이 "큐브 하나의 스케일" 에서 "세 면이 서 있는가" 로 바뀐다 —
            // 판정이 지키는 것은 형상이 아니라 <b>그 세 방향이 막혀 있다</b> 이기 때문이다.
            var all = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var core = all.Where(x => x.name == "PlazaCore").ToArray();
            Require(core.Length == 1, "plaza core must exist exactly once — it is the SIMUL_ZONES device, not decor");

            var half = LastShiftPlazaLayout.CoreHalfExtent;
            foreach (var face in new[] { "PlazaCore_Stern", "PlazaCore_Port", "PlazaCore_Starboard" })
                Require(all.Count(x => x.name == face) == 1,
                    $"{face} 가 없다 — 그 방향으로 시야가 열려 SIMUL_ZONES 가 깨진다");

            // 조종석 쪽만 게이트다. 나머지 셋에는 열 수단이 없어야 한다 — 끌 수 있는 스위치를
            // 만들어 두면 언젠가 꺼진다.
            var gates = all.SelectMany(x => x.GetComponents<LastShiftEvaGate>()).ToArray();
            Require(gates.Length == 1, $"코어 게이트가 {gates.Length} 개다 — 조종석 쪽 하나여야 한다");

            var faces = all.Where(x => x.name.StartsWith("PlazaCore_")).ToArray();
            Require(faces.Length > 0, "plaza core shell has no faces");
            var footprint = faces[0].position;
            var minX = footprint.x; var maxX = footprint.x; var minZ = footprint.z; var maxZ = footprint.z;
            foreach (var f in faces)
            {
                minX = Mathf.Min(minX, f.position.x); maxX = Mathf.Max(maxX, f.position.x);
                minZ = Mathf.Min(minZ, f.position.z); maxZ = Mathf.Max(maxZ, f.position.z);
            }
            Require(Mathf.Abs(maxX - minX - half * 2f) < 0.01f && Mathf.Abs(maxZ - minZ - half * 2f) < 0.01f,
                $"plaza core footprint must stay {half * 2f:F1}m square");

            const float step = 0.05f;
            var samples = 0;
            var worst = 0;
            for (var x = LastShiftPlazaLayout.PlazaMinX; x <= LastShiftPlazaLayout.PlazaMaxX; x += step)
            for (var z = LastShiftPlazaLayout.PlazaMinZ; z <= LastShiftPlazaLayout.PlazaMaxZ; z += step)
            {
                if (LastShiftPlazaLayout.InsideCore(x, z)) continue;
                samples++;
                var readings = LastShiftPlazaLayout.SimultaneousZoneReadings(x, z);
                if (readings <= worst) continue;
                worst = readings;
                Require(worst <= 2, $"SIMUL_ZONES violated at ({x:F2}, {z:F2}) — {worst} zones readable at once");
            }

            Require(samples > 0, "plaza sampling produced no points");
            return (samples, worst);
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
            // 고정 공간 일곱을 그대로 훑는다. 방과 통로를 따로 나열하던 자리이고, 통로가
            // 폐지되면서 목록이 발자국표 하나로 합쳐졌다 — 광장도 사람이 서는 자리라 여기
            // 들어온다.
            foreach (var footprint in LastShiftPlazaLayout.Footprints)
                best = Mathf.Max(best, Clearance(point,
                    footprint.MinX, footprint.MaxX, footprint.MinZ, footprint.MaxZ));
            return best;
        }

        private static float Clearance(Vector3 point, float minX, float maxX, float minZ, float maxZ) =>
            Mathf.Min(point.x - minX, maxX - point.x, point.z - minZ, maxZ - point.z);


        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"LAST SHIFT SP-01 verification failed: {message}.");
        }
    }
}
