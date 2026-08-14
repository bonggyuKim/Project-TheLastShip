using System.Collections.Generic;
using System.IO;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 구운 배에 <b>천장 면이 실제로 있는가</b>.
    ///
    /// 이 검사가 생긴 이유. 정본 지도에 <c>ceilings</c> 규칙이 여섯 공간을 겨냥해 살아 있었고
    /// 프리팹에도 그 이름의 오브젝트가 아홉 개 들어 있었는데, 배 안에서 위를 보면 일곱 공간이
    /// 전부 별 배경이었다. <c>LPK_Ceiling_Straight_4m</c> 이 <b>이름과 달리 판이 아니라</b>
    /// 보 하나와 등 하나였기 때문이다 — 덮는 면은 <c>ceilingPanels</c> 층이 지고 있었고,
    /// 그 층을 "겹친 두 겹" 으로 보고 지우면서(<c>91902d5</c>) 지붕이 통째로 사라졌다.
    ///
    /// 규칙이 있는지·오브젝트가 있는지를 세는 검사는 그 사고를 <b>전부 통과시켰다</b>.
    /// 그래서 여기서는 이름을 안 세고 <b>덮였는지</b>를 잰다 — 방 바닥 격자 위 어느 점에서
    /// 위를 봐도 머리 위에 면이 하나는 있어야 한다.
    /// </summary>
    public sealed class LastShiftCeilingCoverageTests
    {
        private const string MapPath = "Assets/DoodleUp/Data/LastShiftModularMap.json";
        private const string ShipPrefabPath = "Assets/DoodleUp/Prefabs/LastShiftShipGraybox.prefab";

        /// <summary>머리 위 면으로 셀 높이 띠. 아래는 설비, 위는 외피·탑이라 그 사이만 본다.</summary>
        private const float MinCover = 2.6f;
        private const float MaxCover = 4.0f;

        /// <summary>방 한 변에서 볼 표본 수. 보 하나가 방을 가로지르는 것과 구분하려면 격자여야 한다.</summary>
        private const int Samples = 5;

        /// <summary>벽·구석 마감에 표본이 물리지 않도록 발자국에서 들이는 거리.</summary>
        private const float Inset = 0.6f;

        [Test]
        public void EveryPressurisedSpaceIsRoofed()
        {
            var map = LoadMap();
            var ship = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath));
            try
            {
                var covers = CollectCovers(ship);
                Assert.That(covers.Count, Is.GreaterThan(0), "머리 위 면이 배 전체에 하나도 없다");

                foreach (var space in AllSpaces(map))
                {
                    var open = new List<Vector2>();
                    foreach (var point in Grid(space.bounds))
                    {
                        if (InsideEvaShaft(point)) continue;   // 승강 샤프트는 뚫려 있어야 한다
                        if (!IsCovered(covers, point)) open.Add(point);
                    }

                    Assert.That(open, Is.Empty,
                        $"{space.id} 천장에 구멍이 있다 — {open.Count}/{Samples * Samples} 표본에서 " +
                        $"머리 위가 비었다(첫 자리 {(open.Count > 0 ? open[0].ToString() : "-")}). " +
                        "지도의 ceilingShell 대상에서 빠졌거나 판이 발자국을 다 못 덮는다.");
                }
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }

        /// <summary>
        /// 승강 샤프트는 <b>천장이 뚫린 채</b>여야 하고, 그 위 탑은 다시 막혀 있어야 한다.
        /// 둘 중 하나만 맞으면 승강기가 판에 막히거나(아래) 통 안에서 우주가 보인다(위).
        /// </summary>
        [Test]
        public void TheEvaShaftStaysOpenAndItsTrunkIsClosed()
        {
            var ship = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath));
            try
            {
                var covers = CollectCovers(ship);
                Assert.That(IsCovered(covers, Vector2.zero), Is.False,
                    "샤프트 한가운데가 방 천장 높이에서 막혀 있다 — 승강기가 그 판에 걸린다");

                var trunk = CollectCovers(ship, LastShiftEvaShaft.TopHatchY - 0.3f, LastShiftEvaShaft.TopHatchY + 0.6f);
                var wall = LastShiftEvaShaft.HalfExtent - Inset;
                foreach (var point in new[]
                         {
                             new Vector2(wall, wall), new Vector2(-wall, wall),
                             new Vector2(wall, -wall), new Vector2(-wall, -wall)
                         })
                    Assert.That(IsCovered(trunk, point), Is.True,
                        $"탑 지붕이 {point} 에서 비었다 — 샤프트 안에서 위를 보면 별이 보인다");
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }

        /// <summary>
        /// 방을 두르는 <b>세로 면이 천장까지 닿는가</b>. 판을 깔아도 벽 윗변과 천장 밑면 사이에
        /// 띠가 남으면 그 띠로 다시 우주가 보인다 — 실제 렌더에서 방 둘레를 따라 별이 한 줄
        /// 났다. 그래서 덮였는지(위 검사)와 <b>닿았는지</b>를 따로 잰다.
        /// </summary>
        [Test]
        public void TheRoomEnvelopeReachesTheCeiling()
        {
            var map = LoadMap();
            var ship = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath));
            try
            {
                Physics.SyncTransforms();
                var covers = CollectCovers(ship);
                var all = new List<MapSpace>(AllSpaces(map));
                var leaks = new List<string>();
                foreach (var space in all)
                foreach (var probe in Perimeter(space.bounds))
                {
                    // 이웃 방으로 트인 자리는 막혀 있으면 안 되는 자리다.
                    if (Occupied(all, probe.origin + probe.outward * 1.0f)) continue;

                    // <b>선언값이 아니라 실제로 덮은 높이</b> 바로 밑에서 바깥을 본다. 판이
                    // 선언값과 다른 높이에 앉아 있으면 광선이 천장 판 <b>속</b>에서 출발해
                    // 아무것도 못 맞힌다 — 숙소가 실제로 그랬다(큐브 3.0 / 지도 3.2).
                    var roof = RoofAt(covers, probe.origin);
                    if (float.IsPositiveInfinity(roof)) continue;
                    var direction = new Vector3(probe.outward.x, 0f, probe.outward.y);
                    var top = roof;
                    while (top > 1.0f &&
                           !Physics.Raycast(new Vector3(probe.origin.x, top - 0.02f, probe.origin.y), direction, 1.5f))
                        top -= 0.01f;
                    if (top < roof - 0.005f)
                        leaks.Add($"{space.id}{probe.origin}→{probe.outward}roof={roof:F2}top={top:F2}");
                }

                Assert.That(leaks, Is.Empty,
                    $"천장 바로 밑이 {leaks.Count} 곳에서 바깥으로 뚫려 있다: " +
                    string.Join(", ", leaks.GetRange(0, Mathf.Min(12, leaks.Count))));
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }

        /// <summary>
        /// 둘레 표본. 문 구멍과 방끼리 트인 자리를 피해 <b>네 변의 사분점</b>만 본다 —
        /// 문 앞은 원래 뚫려 있어야 하는 자리라 여기서 세면 늘 실패한다.
        /// </summary>
        private static IEnumerable<(Vector2 origin, Vector2 outward)> Perimeter(float[] bounds)
        {
            const float standOff = 0.8f;
            foreach (var t in new[] { 0.25f, 0.75f })
            {
                var x = Mathf.Lerp(bounds[0], bounds[1], t);
                var z = Mathf.Lerp(bounds[2], bounds[3], t);
                yield return (new Vector2(x, bounds[2] + standOff), Vector2.down);
                yield return (new Vector2(x, bounds[3] - standOff), Vector2.up);
                yield return (new Vector2(bounds[0] + standOff, z), Vector2.left);
                yield return (new Vector2(bounds[1] - standOff, z), Vector2.right);
            }

            // 모서리는 변 표본이 절대 안 짚는다. 두 면이 <b>만나기만</b> 하고 안 닿으면
            // 거기 세로로 하늘이 선다 — 실제로 새는 화소의 광선이 전부 방 모서리로 나갔다.
            foreach (var sx in new[] { 1f, -1f })
            foreach (var sz in new[] { 1f, -1f })
            {
                var corner = new Vector2(sx > 0 ? bounds[1] : bounds[0], sz > 0 ? bounds[3] : bounds[2]);
                yield return (corner - new Vector2(sx, sz) * standOff, new Vector2(sx, sz).normalized);
            }
        }

        /// <summary>
        /// 천장 보와 등이 <b>방 안에</b> 달려 있는가. 검수에서 "조명과 보가 허공에 놓인
        /// 형태" 로 잡힌 자리다 — 킷 조각이 바닥 기준으로 짜여 있는데(보가 조각 원점에서
        /// <c>+2.95</c>) 규칙이 그 조각을 다시 천장 높이에 놓으면 지붕 위로 올라간다.
        /// </summary>
        [Test]
        public void CeilingFittingsHangInsideTheRoom()
        {
            var ship = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath));
            try
            {
                var outside = new List<string>();
                var seen = 0;
                foreach (var renderer in ship.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var owner = OwnerNamed(renderer.transform, "ceilings");
                    if (owner == null) continue;
                    seen++;
                    if (renderer.bounds.min.y > MaxCover)
                        outside.Add($"{owner}@{renderer.bounds.min.y:F2}");
                }

                Assert.That(seen, Is.GreaterThan(0), "ceilings 규칙 생성물이 프리팹에 없다");
                Assert.That(outside, Is.Empty,
                    $"천장 부속 {outside.Count} 개가 지붕 위에 떠 있다: " +
                    string.Join(", ", outside.GetRange(0, Mathf.Min(8, outside.Count))));
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }

        /// <summary>
        /// 천장 부속이 <b>그 방의</b> 판 밑에 있는가. 위 검사는 배 전체에 한 줄(<c>MaxCover</c>)을
        /// 긋기 때문에 방마다 천장이 다르면 못 본다 — 숙소를 <c>3.0</c> 으로 낮추면 본선 기준으로
        /// 걸린 보가 그 방에서만 판을 뚫는다. 규칙의 <c>positionY</c> 는 본선에 맞춰 적혀 있고
        /// 임포터가 방 천장 차이만큼 내리므로(<c>ceilingRelative</c>), 그 보정이 빠지면 여기서 걸린다.
        /// </summary>
        [Test]
        public void CeilingFittingsStayUnderTheirOwnRoomsRoof()
        {
            var map = LoadMap();
            var ship = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath));
            try
            {
                var spaces = new List<MapSpace>(AllSpaces(map));
                var pierced = new List<string>();
                var seen = 0;
                foreach (var renderer in ship.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (OwnerNamed(renderer.transform, "ceilings") == null) continue;
                    var bounds = renderer.bounds;
                    var centre = new Vector2(bounds.center.x, bounds.center.z);
                    var space = spaces.Find(s =>
                        centre.x > s.bounds[0] && centre.x < s.bounds[1] &&
                        centre.y > s.bounds[2] && centre.y < s.bounds[3]);
                    if (space == null) continue;
                    seen++;
                    if (bounds.max.y > space.ceiling + 0.01f)
                        pierced.Add($"{space.id}@{bounds.max.y:F2}>{space.ceiling:F2}");
                }

                Assert.That(seen, Is.GreaterThan(0), "방 안에 든 ceilings 생성물이 하나도 없다");
                Assert.That(pierced, Is.Empty,
                    $"천장 부속 {pierced.Count} 개가 자기 방 판을 뚫었다: " +
                    string.Join(", ", pierced.GetRange(0, Mathf.Min(8, pierced.Count))));
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }

        private static string OwnerNamed(Transform node, string prefix)
        {
            for (var t = node; t != null; t = t.parent)
                if (t.name.StartsWith(prefix + "_", System.StringComparison.Ordinal)) return t.name;
            return null;
        }

        /// <summary>
        /// 판을 만드는 규칙이 지도에 있는가. 위 검사는 <b>구운 프리팹</b>을 보므로,
        /// 규칙만 지워 두고 다시 안 구우면 통과해 버린다.
        /// </summary>
        [Test]
        public void TheCanonicalMapCarriesTheCeilingShellRule()
        {
            var map = LoadMap();
            var shell = System.Array.Find(map.placementRules, rule => rule.id == "ceilingShell");
            Assert.That(shell, Is.Not.Null, "정본 지도에 ceilingShell 규칙이 없다");
            Assert.That(shell.operation, Is.EqualTo("ceilingShell"));

            // 숙소가 여기 들어온 것이 2026-08-14 의 변경이다. 씬 빌더의 고정 구획 큐브가
            // 같은 방을 3.0 으로 한 번 더 세우고 있었고, 그 큐브를 걷으면서 천장 주인이
            // 이 규칙 하나가 됐다 — 빠지면 숙소만 지붕 없이 남는다.
            foreach (var id in new[] { "plaza", "cockpit", "power", "cooling", "lifeSupport", "quarters" })
                Assert.That(System.Array.IndexOf(shell.target, id), Is.GreaterThanOrEqualTo(0),
                    $"{id} 가 천장 대상에서 빠졌다");
        }

        /// <summary>
        /// 숙소가 <b>정본 지도 높이</b>로, 그리고 <b>한 겹으로</b> 서 있는가. 씬 빌더가 세우던
        /// 고정 구획 큐브(<c>Compartment_Quarters/Ceiling</c>·<c>Floor</c>·<c>Wall_*</c>)는 지도와
        /// 같은 자리를 두 번 세우고 있었다. 그 큐브가 다시 생기면 여기서 걸린다 — 위 덮임 검사는
        /// 판이 <b>둘</b>이어도 통과하므로 이름과 겹 수를 따로 센다.
        ///
        /// <b>높이가 같아졌다고 큐브가 돌아와도 되는 것이 아니다</b>(2026-08-14). 부속 대비를
        /// 되살리며 정본 지도의 <c>quarters.ceiling</c> 을 <c>3.0</c> 으로 낮췄으므로, 이제
        /// 큐브와 판이 같은 높이에 앉는다. 구별은 높이가 아니라 겹 수가 한다.
        /// </summary>
        [Test]
        public void TheQuartersCarriesNoLegacyCompartmentGeometry()
        {
            var map = LoadMap();
            var quarters = System.Array.Find(map.spaces, space => space.id == "quarters");
            Assert.That(quarters, Is.Not.Null, "정본 지도에 숙소 공간이 없다");

            var ship = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath));
            try
            {
                var root = FindDescendant(ship.transform, "Compartment_Quarters");
                Assert.That(root, Is.Not.Null, "드레싱이 붙을 숙소 루트가 없다");

                // 판은 전부 루트 <b>바로 아래</b>에 섰다. 소품까지 훑으면 드레싱이 언젠가
                // 같은 이름을 쓸 때 이 검사가 엉뚱한 것을 잡는다.
                var legacy = new List<string>();
                for (var index = 0; index < root.childCount; index++)
                {
                    var child = root.GetChild(index).name;
                    if (child is "Floor" or "Ceiling" ||
                        child.StartsWith("Wall_", System.StringComparison.Ordinal))
                        legacy.Add(child);
                }

                Assert.That(legacy, Is.Empty,
                    $"숙소에 legacy 구획 지오메트리가 {legacy.Count} 개 남았다: " +
                    string.Join(", ", legacy.GetRange(0, Mathf.Min(8, legacy.Count))) +
                    " — 정본 지도의 quarters 공간과 두 겹이 된다");

                // 이름만 세면 <b>다시 생긴</b> 것은 잡아도 <b>안 생긴</b> 것은 못 잡는다.
                // 그래서 판이 실제로 어느 높이에 앉았는지를 같이 잰다.
                //
                // <b>가장 낮은 면을 보면 안 된다.</b> 천장 보(<c>ceilings</c> 규칙)가 판보다
                // 아래 걸려 있어서 방 한가운데의 최저 면은 늘 보다 — 실측 <c>2.85</c>. 판을
                // 집으려면 밑면이 그 높이인 렌더러만 골라야 한다.
                var b = quarters.bounds;
                var centre = new Vector2((b[0] + b[1]) * 0.5f, (b[2] + b[3]) * 0.5f);

                // <b>덮였나가 아니라 몇 겹인가를 센다</b>(2026-08-14). 숙소 실내고를 지도에서
                // <c>3.0</c> 으로 낮추면서 걷어낸 큐브의 높이와 정본 높이가 같아졌다 — 예전처럼
                // "3.0 에 아무것도 없어야 한다" 로는 큐브가 돌아와도 구분이 안 된다. 같은 자리를
                // 두 번 세우는 것이 문제였으므로 그 자리를 덮은 판의 <b>개수</b>를 본다.
                var slabs = CountCovers(CollectCovers(ship, quarters.ceiling - 0.05f, quarters.ceiling + 0.05f), centre);
                Assert.That(slabs, Is.EqualTo(1),
                    $"숙소 한가운데 위 {quarters.ceiling:F2} 를 덮은 판이 {slabs} 장이다 — " +
                    "0 이면 ceilingShell 대상에서 숙소가 빠진 것이고, 2 이상이면 고정 구획 큐브가 돌아온 것이다");

                // 본선 높이에는 아무것도 없어야 한다. 여기 판이 서면 숙소가 다시 본선과 같은
                // 높이로 올라갔거나(지도 되돌림) 두 높이가 겹쳐 깔린 것이다.
                var mainRoof = LastShiftShipPhysics.CeilingInnerHeight;
                Assert.That(IsCovered(CollectCovers(ship, mainRoof - 0.05f, mainRoof + 0.05f), centre),
                    Is.False,
                    $"숙소 한가운데 위 {mainRoof:F2} 에 판이 있다 — 부속이 본선 실내고로 되돌아갔다");
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }

        private static Transform FindDescendant(Transform node, string name)
        {
            if (node.name == name) return node;
            for (var index = 0; index < node.childCount; index++)
            {
                var hit = FindDescendant(node.GetChild(index), name);
                if (hit != null) return hit;
            }

            return null;
        }

        /// <summary>이 자리를 덮은 면 중 <b>가장 낮은</b> 밑면. 없으면 무한대다.</summary>
        private static float RoofAt(IReadOnlyList<Bounds> covers, Vector2 point)
        {
            var lowest = float.PositiveInfinity;
            foreach (var bounds in covers)
                if (point.x >= bounds.min.x && point.x <= bounds.max.x &&
                    point.y >= bounds.min.z && point.y <= bounds.max.z && bounds.min.y < lowest)
                    lowest = bounds.min.y;
            return lowest;
        }

        private static bool Occupied(List<MapSpace> spaces, Vector2 point)
        {
            foreach (var space in spaces)
                if (point.x > space.bounds[0] && point.x < space.bounds[1] &&
                    point.y > space.bounds[2] && point.y < space.bounds[3]) return true;
            return false;
        }

        private static bool InsideEvaShaft(Vector2 point) =>
            Mathf.Abs(point.x) < LastShiftEvaShaft.HalfExtent + 0.05f &&
            Mathf.Abs(point.y) < LastShiftEvaShaft.HalfExtent + 0.05f;

        private static bool IsCovered(IReadOnlyList<Bounds> covers, Vector2 point)
        {
            foreach (var bounds in covers)
                if (point.x >= bounds.min.x && point.x <= bounds.max.x &&
                    point.y >= bounds.min.z && point.y <= bounds.max.z) return true;
            return false;
        }

        /// <summary>이 자리를 덮은 면의 <b>수</b>. 두 겹으로 깔린 것을 덮임 검사로는 못 본다.</summary>
        private static int CountCovers(IReadOnlyList<Bounds> covers, Vector2 point)
        {
            var count = 0;
            foreach (var bounds in covers)
                if (point.x >= bounds.min.x && point.x <= bounds.max.x &&
                    point.y >= bounds.min.z && point.y <= bounds.max.z) count++;
            return count;
        }

        private static List<Bounds> CollectCovers(GameObject ship, float low = MinCover, float high = MaxCover)
        {
            var covers = new List<Bounds>();
            foreach (var renderer in ship.GetComponentsInChildren<MeshRenderer>(true))
            {
                var bounds = renderer.bounds;
                if (bounds.min.y < low || bounds.min.y > high) continue;
                covers.Add(bounds);
            }
            return covers;
        }

        private static IEnumerable<Vector2> Grid(float[] bounds)
        {
            for (var i = 0; i < Samples; i++)
            for (var j = 0; j < Samples; j++)
                yield return new Vector2(
                    Mathf.Lerp(bounds[0] + Inset, bounds[1] - Inset, i / (Samples - 1f)),
                    Mathf.Lerp(bounds[2] + Inset, bounds[3] - Inset, j / (Samples - 1f)));
        }

        private static IEnumerable<MapSpace> AllSpaces(MapRoot map)
        {
            yield return new MapSpace { id = "plaza", bounds = map.plaza.bounds, ceiling = map.plaza.ceiling };
            foreach (var space in map.spaces) yield return space;
        }

        private static MapRoot LoadMap()
        {
            Assert.That(File.Exists(MapPath), Is.True, MapPath);
            var map = JsonUtility.FromJson<MapRoot>(File.ReadAllText(MapPath));
            Assert.That(map?.spaces, Is.Not.Null);
            return map;
        }

        [System.Serializable] private sealed class MapRoot { public MapPlaza plaza; public MapSpace[] spaces; public MapRule[] placementRules; }
        [System.Serializable] private sealed class MapPlaza { public float[] bounds; public float ceiling; }
        [System.Serializable] private sealed class MapSpace { public string id; public float[] bounds; public float ceiling; }
        [System.Serializable] private sealed class MapRule { public string id; public string operation; public string[] target; }
    }
}
