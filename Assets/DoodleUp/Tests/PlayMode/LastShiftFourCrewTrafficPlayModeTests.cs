using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.PlayMode
{
    /// <summary>
    /// 승무원 <b>넷을 동시에</b> 씬에 세우고 실제 콜라이더 위에서 걷게 한다.
    ///
    /// <c>LastShiftFourCrewClearanceTests</c>(EditMode)는 좌표끼리의 관계라 "구멍 1.6m 에
    /// 0.72m 차선이 둘 들어간다" 가 참이면 통과한다. 그런데 4인 플레이에서 실제로 막히는 것은
    /// 그 부등식이 참일 때다 — 캡슐 넷이 같은 프레임에 같은 구멍으로 몰리면 서로를 밀어내고,
    /// CharacterController 는 밀려난 자리에서 다시 벽에 걸린다. 그래서 이 파일은 좌표가 아니라
    /// <b>도달 여부</b>를 재고, 못 간 자리를 좌표째로 남긴다.
    ///
    /// <b>스폰 넷이 전부 숙소 안이라는 것이 이 시뮬레이션의 요점이다.</b>
    /// (<see cref="LastShiftNetworkSession.SpawnForSlot"/> — 온보딩 1단계가 "기상(숙소)" 라
    /// fb71c1b 에서 조종석에서 옮겨왔다) 그래서 넷은 시작하자마자 숙소↔광장 생활문
    /// <b>하나</b>를 같이 지나야 하고, 배에서 유일하게 4인이 한 점에 몰리는 자리가 거기다.
    /// </summary>
    public sealed class LastShiftFourCrewTrafficPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP02A_NETWORK.unity";

        private const int Crew = LastShiftNetworkSession.MaxPlayers;

        /// <summary>승무원 하나가 평면에서 먹는 폭. EditMode 쪽과 같은 유도다.</summary>
        private const float CrewLane =
            2f * (LastShiftShipPhysics.CrewRadius + LastShiftShipPhysics.CrewSkinWidth);

        /// <summary>이 거리 안에 들어오면 그 경유점은 지난 것으로 본다.</summary>
        private const float WaypointRadius = 0.7f;

        /// <summary>막힘 판정 창. 이 시간 동안 <see cref="StallEpsilon"/> 도 못 가면 막힌 것이다.</summary>
        private const float StallWindow = 1.5f;

        private const float StallEpsilon = 0.15f;

        private LastShiftSandboxController sandbox;
        private LastShiftPlayerController[] crew;

        [UnitySetUp]
        public IEnumerator LoadSceneAndPlaceFourCrew()
        {
            LastShiftNetworkSession.AutoStartHost = false;
            LastShiftAirlock.Clear();
            LastShiftVoyage.Clear();

            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, $"Failed to start loading {ScenePath}");
            while (!load.isDone) yield return null;
            yield return null;

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            sandbox = roots.SelectMany(root => root.GetComponentsInChildren<LastShiftSandboxController>(true)).Single();
            foreach (var networkObject in roots.SelectMany(root =>
                         root.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true)))
                networkObject.AutoObjectParentSync = false;

            var session = Object.FindAnyObjectByType<LastShiftNetworkSession>();
            Assert.That(session, Is.Not.Null, "network session missing from the scene");

            // 넷을 <b>같은 프레임에</b> 놓는다. 하나씩 놓고 안정시키면 실제 4인 접속에서
            // 일어나는 "겹친 채로 시작해 서로를 밀어낸다" 가 재현되지 않는다.
            crew = new LastShiftPlayerController[Crew];
            for (var slot = 0; slot < Crew; slot++)
            {
                var body = Object.Instantiate(session.PlayerPrefab.gameObject);
                body.name = $"Crew{slot}";
                var controller = body.GetComponent<LastShiftPlayerController>();
                Assert.That(controller, Is.Not.Null, "player prefab must carry LastShiftPlayerController");
                controller.ResetPlayer(LastShiftNetworkSession.SpawnForSlot(slot),
                    LastShiftNetworkSession.RotationForSlot(slot));
                crew[slot] = controller;
            }

            UnityEngine.Physics.SyncTransforms();
            sandbox.enabled = false;
        }

        [TearDown]
        public void DestroyCrew()
        {
            if (crew != null)
                foreach (var member in crew)
                    if (member != null)
                        Object.Destroy(member.gameObject);
            crew = null;
            LastShiftAirlock.Clear();
            LastShiftVoyage.Clear();
        }

        // ── 1. 동시 스폰 ─────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator FourCrewSpawnAtOnceWithoutOverlappingPushingOrFalling()
        {
            var placed = new Vector3[Crew];
            for (var slot = 0; slot < Crew; slot++) placed[slot] = LastShiftNetworkSession.SpawnForSlot(slot);

            // 입력 없이 물리만 돌린다. 겹쳐 있으면 이 1.5초 안에 서로를 밀어낸다.
            yield return StepAll(_ => Vector2.zero, 1.5f);

            var report = new StringBuilder();
            for (var slot = 0; slot < Crew; slot++)
            {
                var now = crew[slot].transform.position;
                var drift = Vector2.Distance(new Vector2(now.x, now.z),
                    new Vector2(placed[slot].x, placed[slot].z));
                report.AppendLine(
                    $"  slot{slot} 배치=({placed[slot].x:0.##},{placed[slot].z:0.##}) " +
                    $"1.5초후=({now.x:0.##},{now.y:0.##},{now.z:0.##}) 밀림={drift:0.###}m");
            }

            for (var slot = 0; slot < Crew; slot++)
            {
                var now = crew[slot].transform.position;

                Assert.That(now.y, Is.GreaterThan(LastShiftBypassDuct.FloorY),
                    $"slot{slot} 이 갑판을 뚫고 떨어졌다 — 스폰 좌표 밑에 밟을 것이 없다.\n{report}");
                Assert.That(now.y, Is.LessThan(LastShiftShipPhysics.CrewStepOffset),
                    $"slot{slot} 이 갑판 위로 떠올랐다 — 다른 캡슐 위로 밀려 올라간 자세다.\n{report}");

                var drift = Vector2.Distance(new Vector2(now.x, now.z),
                    new Vector2(placed[slot].x, placed[slot].z));
                Assert.That(drift, Is.LessThan(CrewLane * 0.5f),
                    $"slot{slot} 이 배치 자리에서 {drift:0.###}m 밀려났다 — 슬롯 간격 " +
                    $"{LastShiftNetworkSession.SpawnForSlot(1).z - LastShiftNetworkSession.SpawnForSlot(0).z:0.##}m " +
                    $"가 차선 폭 {CrewLane:0.##}m 를 못 버틴다는 뜻이다.\n{report}");

                Assert.That(LastShiftPlazaLayout.TryResolveSpace(now.x, now.z, out var space), Is.True,
                    $"slot{slot} 이 고정 발자국 밖이다 — ({now.x:0.##},{now.z:0.##}).\n{report}");
                // <b>깨어나는 방이 숙소로 옮겨왔다</b>(fb71c1b). 온보딩 1단계가 "기상(숙소)"
                // 인데 스폰이 조종석이었다 — 재려던 것(넷이 겹치지 않고 갑판 위에 선다)은
                // 그대로이고 서는 방만 바뀌었다.
                Assert.That(space, Is.EqualTo(LastShiftPlazaSpace.Quarters),
                    $"slot{slot} 이 숙소 밖에서 시작한다 — {space}.\n{report}");
            }

            for (var a = 0; a < Crew; a++)
            for (var b = a + 1; b < Crew; b++)
            {
                var pa = crew[a].transform.position;
                var pb = crew[b].transform.position;
                var gap = Vector2.Distance(new Vector2(pa.x, pa.z), new Vector2(pb.x, pb.z));
                Assert.That(gap, Is.GreaterThanOrEqualTo(CrewLane * 0.9f),
                    $"slot{a} 와 slot{b} 가 {gap:0.###}m 로 겹쳐 있다.\n{report}");
            }

            Debug.Log($"[LAST_SHIFT_4P_SPAWN]\n{report}");
        }

        // ── 2. 동시 동선 ─────────────────────────────────────────────────────

        /// <summary>
        /// 넷이 <b>같은 시각에</b> 숙소를 떠나 서로 다른 네 목적지로 간다. 생활문 하나에서
        /// 갈라져 압력문 셋 + 조종석 개구부로 흩어지는, 4인 플레이의 표준 첫 동선이다.
        /// </summary>
        [UnityTest]
        public IEnumerator FourCrewLeaveTheQuartersThroughOneDoorAndReachFourDifferentDestinations()
        {
            yield return OpenEveryPressureDoor();

            var routes = BuildDispersalRoutes();
            var stalls = new List<string>();
            yield return WalkRoutes(routes, budgetSeconds: 40f, stalls);

            AssertArrived(routes, stalls, "4인 분산");
        }

        /// <summary>
        /// 압력문 하나를 두 승무원이 <b>마주 보고</b> 지난다. EditMode 검사가 "물리로는 2인 교행,
        /// 플레이 폭으로는 단선" 이라고 낸 결론이 실제 콜라이더에서 어느 쪽인지 여기서 갈린다.
        /// </summary>
        [UnityTest]
        public IEnumerator TwoCrewCrossTheSamePressureDoorHeadOnWithoutDeadlocking()
        {
            yield return OpenEveryPressureDoor();

            // <b>둘이 각자 반쪽 차선을 잡는다.</b> 구멍 폭 1.6m 에 차선 0.72m 둘을 넣으면
            // 중심이 ±0.36 이고 양 옆에 0.08m 씩 남는다 — 실플레이에서 사람이 비켜서는 것과
            // 같은 자세이고, 이 자세로도 못 지나가면 그 문은 물리적으로 단선이다.
            const float lane = CrewLane * 0.5f;
            var door = LastShiftPlazaLayout.DoorOf(LastShiftPlazaSpace.PowerRoom).Waypoint; // (0, -6)
            var inside = new Vector2(-lane, -8.5f);   // 전력실 안
            var outside = new Vector2(lane, -4.2f);   // 광장 쪽, 코어(±2) 밖

            // 하나는 미리 전력실에 들여놓고, 다른 하나는 광장에서 들어간다.
            crew[0].ResetPlayer(new Vector3(inside.x, 0.1f, inside.y));
            crew[1].ResetPlayer(new Vector3(outside.x, 0.1f, outside.y));
            // 나머지 둘은 이 검사 밖이다 — 멀찍이 치워 두고 물리에서만 살려 둔다.
            crew[2].ResetPlayer(new Vector3(-10f, 0.1f, -2f));
            crew[3].ResetPlayer(new Vector3(-10f, 0.1f, 2f));
            UnityEngine.Physics.SyncTransforms();
            yield return StepAll(_ => Vector2.zero, 0.5f);

            var routes = new List<Route>
            {
                // 각자 자기 차선을 <b>끝까지</b> 유지한다. 문 한가운데서 반대 차선으로 넘어가면
                // 교행이 아니라 정면 충돌을 재는 검사가 된다.
                new(0, "전력실→광장", new List<Vector2>
                    { new(inside.x, door.y), new(inside.x, outside.y) }),
                new(1, "광장→전력실", new List<Vector2>
                    { new(outside.x, door.y), new(outside.x, inside.y) }),
                new(2, "대기", new List<Vector2> { new(-10f, -2f) }),
                new(3, "대기", new List<Vector2> { new(-10f, 2f) })
            };

            var stalls = new List<string>();
            yield return WalkRoutes(routes, budgetSeconds: 25f, stalls);

            AssertArrived(routes, stalls, "압력문 교행");
        }

        // ── 경로 ─────────────────────────────────────────────────────────────

        private sealed class Route
        {
            public Route(int slot, string label, List<Vector2> waypoints)
            {
                Slot = slot;
                Label = label;
                Waypoints = waypoints;
            }

            public int Slot { get; }
            public string Label { get; }
            public List<Vector2> Waypoints { get; }
            public int Index { get; set; }
            public bool Arrived => Index >= Waypoints.Count;
        }

        /// <summary>
        /// 목적지 넷은 압력 구역 셋 + 조종석이다. <b>출발이 숙소</b>라 넷은 먼저 생활문
        /// <c>(4.8, 6)</c> 하나로 몰렸다가 광장에서 흩어진다. 경유점에 <b>코어를 피하는 꺾임</b>이
        /// 들어가는 이유는 광장 한가운데가 <c>4 x 4</c> 코어로 막혀 있어 직선이 안 나기 때문이다 —
        /// 실플레이에서도 사람이 그렇게 돈다.
        ///
        /// <b>문은 정면으로만 지난다.</b> 구멍이 <c>1.6m</c> 뿐이라 벌크헤드를 비스듬히 가로지르는
        /// 경유점을 주면 캡슐이 문틀 옆 벽면을 긁으며 서고, 그때 재는 것은 "4인이 몰려서 막혔다"가
        /// 아니라 "대각선으로 벽에 박았다"가 된다. 그래서 문마다 앞뒤로 <b>정렬 경유점</b>을 놓아
        /// 통과 방향을 문 법선에 맞춘다 — 실플레이에서 사람이 문을 나온 뒤에 방향을 트는 것과 같다.
        ///
        /// <b>숙소 안에서는 문 x 를 잡은 채 -z 로만 내려간다.</b> 숙소 서쪽은 냉각실과 맞댄
        /// 칸막이(<c>walls_007</c>, <c>x = 4</c> 평면)이고 그 벽에는 구멍이 없다. 나가기 전에
        /// 광장 쪽 목적지를 먼저 겨누면 넷이 그대로 그 벽에 박힌다 — 스폰이 조종석에서 숙소로
        /// 옮겨온(fb71c1b) 뒤에도 이 경로가 조종석 개구부를 첫 경유점으로 들고 있어서 실제로
        /// 넷 다 <c>(4.52, 6.7~8.6)</c> 에 갇혔다.
        /// </summary>
        private static List<Route> BuildDispersalRoutes()
        {
            var opening = LastShiftPlazaLayout.DoorOf(LastShiftPlazaSpace.CockpitRoom).Waypoint; // (-6, 0)
            var power = LastShiftPlazaLayout.DoorOf(LastShiftPlazaSpace.PowerRoom).Waypoint;     // (0, -6)
            var cooling = LastShiftPlazaLayout.DoorOf(LastShiftPlazaSpace.CoolingRoom).Waypoint; // (0, +6)
            var life = LastShiftPlazaLayout.DoorOf(LastShiftPlazaSpace.LifeSupportRoom).Waypoint; // (+6, 0)
            var quarters = LastShiftPlazaLayout.DoorOf(LastShiftPlazaSpace.Quarters).Waypoint;   // (4.8, 6)

            // 생활문을 <b>빠져나온 자리</b>. 넷이 여기서 처음 흩어진다. 코어(±2) 밖이고
            // 광장 안(z < 6)이다.
            var plazaSide = new Vector2(quarters.x, 4.8f);

            return new List<Route>
            {
                // 전력실. 문을 나와 우현을 따라 선미로 내려간 뒤 문 앞에서 정렬한다.
                new(0, "전력실", new List<Vector2>
                {
                    quarters, plazaSide, new(4.5f, 0f), new(4.5f, -4.5f), new(0f, -4.5f),
                    power, Center(LastShiftPlazaSpace.PowerRoom)
                }),

                // 냉각실. 문을 나와 광장 위쪽을 가로질러 냉각문 앞에서 정렬한다. 문을 지난 뒤
                // <b>좌현으로 한 번 비킨다</b> — 문 정면 z=7.2 에 CoolingCanister 가 서 있어
                // 문에서 방 중심까지 직선이 안 난다.
                new(1, "냉각실", new List<Vector2>
                {
                    quarters, plazaSide, new(0f, 4.5f), cooling, new(0f, 6.6f), new(-1.5f, 7.4f),
                    Center(LastShiftPlazaSpace.CoolingRoom)
                }),

                // 산소실. 우현을 따라 내려와 선미 문 앞에서 정렬한다.
                new(2, "산소실", new List<Vector2>
                {
                    quarters, plazaSide, new(4.5f, 0f), life, Center(LastShiftPlazaSpace.LifeSupportRoom)
                }),

                // 조종석. 코어를 뱃머리 쪽으로 크게 돌아 개구부 앞에서 정렬한다.
                new(3, "조종석", new List<Vector2>
                {
                    quarters, plazaSide, new(-4.5f, 4.5f), new(-4.5f, 0f), opening,
                    Center(LastShiftPlazaSpace.CockpitRoom)
                })
            };
        }

        private static Vector2 Center(LastShiftPlazaSpace space)
        {
            var footprint = LastShiftPlazaLayout.Of(space);
            return new Vector2((footprint.MinX + footprint.MaxX) * 0.5f,
                (footprint.MinZ + footprint.MaxZ) * 0.5f);
        }

        // ── 구동 ─────────────────────────────────────────────────────────────

        private IEnumerator OpenEveryPressureDoor()
        {
            for (var boundary = 0; boundary < LastShiftPlazaLayout.PressureBoundaryCount; boundary++)
                sandbox.SetDoorOpen(boundary, true);

            var doors = Object.FindObjectsByType<LastShiftZoneDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(doors.Length, Is.EqualTo(LastShiftPlazaLayout.PressureBoundaryCount),
                "씬의 압력문 수가 경계 수와 다르다.");

            var guard = 0f;
            while (doors.Any(door => door.IsMoving) && guard < 3f)
            {
                guard += Time.deltaTime;
                yield return null;
            }

            foreach (var door in doors)
                Assert.That(door.IsOpen, Is.True, "압력문이 안 열렸다 — 교행 검사의 전제가 안 선다.");
        }

        /// <summary>네 승무원을 같은 고정 스텝 위에서 같이 민다. 한 명씩 돌리면 교행이 안 생긴다.</summary>
        private IEnumerator WalkRoutes(List<Route> routes, float budgetSeconds, List<string> stalls)
        {
            var lastSample = new Vector2[Crew];
            for (var slot = 0; slot < Crew; slot++)
            {
                var position = crew[slot].transform.position;
                lastSample[slot] = new Vector2(position.x, position.z);
            }

            var elapsed = 0f;
            var sinceSample = 0f;

            while (elapsed < budgetSeconds && routes.Any(route => !route.Arrived))
            {
                foreach (var route in routes)
                {
                    var member = crew[route.Slot];
                    var position = member.transform.position;
                    var here = new Vector2(position.x, position.z);

                    if (route.Arrived)
                    {
                        member.MoveForProbe(Vector2.zero, Time.fixedDeltaTime);
                        continue;
                    }

                    var target = route.Waypoints[route.Index];
                    if (Vector2.Distance(here, target) <= WaypointRadius)
                    {
                        route.Index++;
                        member.MoveForProbe(Vector2.zero, Time.fixedDeltaTime);
                        continue;
                    }

                    var heading = target - here;
                    member.SetAimDirectionForProbe(new Vector3(heading.x, 0f, heading.y));
                    member.MoveForProbe(new Vector2(0f, 1f), Time.fixedDeltaTime);
                }

                elapsed += Time.fixedDeltaTime;
                sinceSample += Time.fixedDeltaTime;

                if (sinceSample >= StallWindow)
                {
                    sinceSample = 0f;
                    foreach (var route in routes)
                    {
                        if (route.Arrived) continue;
                        var position = crew[route.Slot].transform.position;
                        var here = new Vector2(position.x, position.z);
                        if (Vector2.Distance(here, lastSample[route.Slot]) >= StallEpsilon) continue;

                        stalls.Add($"slot{route.Slot}({route.Label}) 막힘 " +
                                   $"좌표=({position.x:0.##},{position.y:0.##},{position.z:0.##}) " +
                                   $"목표=({route.Waypoints[route.Index].x:0.##},{route.Waypoints[route.Index].y:0.##}) " +
                                   $"t={elapsed:0.#}s | {Contact(crew[route.Slot])}");
                    }

                    for (var slot = 0; slot < Crew; slot++)
                    {
                        var position = crew[slot].transform.position;
                        lastSample[slot] = new Vector2(position.x, position.z);
                    }
                }

                yield return new WaitForFixedUpdate();
            }
        }

        private IEnumerator StepAll(System.Func<int, Vector2> move, float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                for (var slot = 0; slot < Crew; slot++)
                    crew[slot].MoveForProbe(move(slot), Time.fixedDeltaTime);
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        private void AssertArrived(List<Route> routes, List<string> stalls, string what)
        {
            var report = new StringBuilder();
            foreach (var route in routes)
            {
                var position = crew[route.Slot].transform.position;
                report.AppendLine($"  slot{route.Slot}({route.Label}) 경유 {route.Index}/{route.Waypoints.Count} " +
                                  $"끝좌표=({position.x:0.##},{position.y:0.##},{position.z:0.##})");
            }

            if (stalls.Count > 0)
            {
                report.AppendLine("  ── 막힌 좌표 ──");
                foreach (var stall in stalls) report.AppendLine($"  {stall}");
            }

            Debug.Log($"[LAST_SHIFT_4P_TRAFFIC] {what}\n{report}");

            var stranded = routes.Where(route => !route.Arrived).ToList();
            Assert.That(stranded, Is.Empty,
                $"{what} — 목적지에 못 간 승무원이 있다:\n{report}");
        }

        /// <summary>왜 못 갔는지. 캡슐에 실제로 닿아 있는 콜라이더를 이름째로 적는다.</summary>
        private static string Contact(LastShiftPlayerController member)
        {
            var controller = member.GetComponent<CharacterController>();
            if (controller == null) return "접촉=?";

            var foot = member.transform.position + Vector3.up * controller.radius;
            var head = member.transform.position + Vector3.up * (controller.height - controller.radius);
            var names = new List<string>();
            foreach (var touched in UnityEngine.Physics.OverlapCapsule(foot, head,
                         controller.radius + controller.skinWidth, ~0, QueryTriggerInteraction.Ignore))
            {
                if (touched.transform.IsChildOf(member.transform)) continue;
                names.Add(Path(touched.transform));
            }

            return names.Count == 0 ? "접촉=없음" : "접촉=" + string.Join(",", names);
        }

        private static string Path(Transform node)
        {
            var name = node.name;
            for (var parent = node.parent; parent != null; parent = parent.parent) name = $"{parent.name}/{name}";
            return name;
        }
    }
}
