using System.Collections.Generic;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 구운 벽 뚫기(자유 배치 마지막 블로커). <b>여기서 지키는 것은 "표에 선 방에 실제로 걸어
    /// 들어갈 수 있다" 다.</b>
    ///
    /// 축 C 까지가 답한 것은 그 칸이 그 자리에 그 크기로 <b>선다</b> 였다. 그런데 부모가 선체나
    /// 고정 구획이면 그 벽은 배 프리팹에 이미 구워져 있어 구멍이 없고, 세운 방은 문 자리까지
    /// 이어지되 뚫고 들어갈 수 없었다 — 표에서는 걸어갈 수 있는 것으로 세어지는 방이다.
    ///
    /// 그래서 이 파일의 단언은 대부분 <b>"문 구멍 자리에 막는 것이 없다"</b> 한 문장이다.
    /// 판이 몇 조각으로 갈렸는지가 아니라 그 사각형이 비었는지를 재는 것이 요점이다 — 조각
    /// 수를 세면 자르는 방식을 바꿀 때마다 테스트가 같이 흔들리고, 정작 사람이 못 지나가는
    /// 것은 안 잡힌다.
    ///
    /// 되돌림을 같은 무게로 잰다. 배치 해제는 기항에서 일어나고 그때 벽이 안 메워지면 우주로
    /// 걸어 나가는 구멍이 남는다.
    /// </summary>
    public sealed class LastShiftBakedDoorwayTests
    {
        private const float Tolerance = 0.001f;
        private const float Thickness = LastShiftCompartments.PanelThickness;
        private const float Height = LastShiftCompartments.InteriorHeight;

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void CleanUp()
        {
            LastShiftBakedDoorways.Restore();
            LastShiftCompartments.ClearModules();
            foreach (var target in spawned)
                if (target != null) Object.DestroyImmediate(target);
            spawned.Clear();
        }

        // ── 고정 구획 벽 ────────────────────────────────────────────────────

        [Test]
        public void OpensABakedCompartmentWallWhereTheModuleDoorSits()
        {
            // 이 카드 전체가 이 한 줄이다 — 라운지 좌현 벽은 프리팹에 구워져 있고, 거기 붙인
            // 모듈은 지금까지 벽을 뚫고 들어갈 수 없었다.
            var ship = NewShip();
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            var wall = BakeCompartmentWall(ship, lounge);

            var spec = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            Register(spec);

            Assert.That(Blocks(ship, spec), Is.True, "표본이 애초에 안 막혀 있으면 이 테스트는 아무것도 안 잰다.");

            var report = LastShiftBakedDoorways.Open(ship.transform);

            Assert.That(report.Doorways, Is.EqualTo(1));
            Assert.That(report.Cut, Is.EqualTo(1));
            Assert.That(report.Missing, Is.Zero, "벽 주인을 못 찾았다 — 구획 루트 이름 규약이 갈렸다.");
            Assert.That(report.Slabs, Is.EqualTo(1));
            Assert.That(Blocks(ship, spec), Is.False,
                "판을 잘랐다는데 문 구멍이 아직 막혀 있다 — 표에는 있고 걸어 들어갈 수는 없는 방이다.");

            // 원본 판은 살아 있어야 한다. 통째로 지우고 새로 세우면 씬에서 그 판을 참조로
            // 물고 있는 쪽이 끊긴다.
            Assert.That(wall == null, Is.False);
            Assert.That(wall.activeSelf, Is.True);
        }

        [Test]
        public void LeavesALintelAboveTheOpening()
        {
            // 인방을 안 남기면 문 높이(2.2)에서 천장까지 그대로 뚫려 그림과 통행 가능 범위가
            // 어긋난다. 씬 빌더·조립기가 구멍을 낼 때 지키는 규칙과 같은 것을 여기서도 지킨다.
            var ship = NewShip();
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            BakeCompartmentWall(ship, lounge);

            var spec = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            Register(spec);
            LastShiftBakedDoorways.Open(ship.transform);

            var door = LastShiftDoorways.Of(spec);
            Assert.That(Occupied(ship, door, door.Center, LastShiftZoneDoor.OpeningHeight + 0.3f), Is.True,
                "문 위가 천장까지 뚫렸다 — 인방이 없다.");
            Assert.That(Occupied(ship, door, door.Center, LastShiftZoneDoor.OpeningHeight - 0.3f), Is.False,
                "문 구멍 안이 아직 막혀 있다.");
        }

        [Test]
        public void KeepsTheWallOnEitherSideOfTheOpening()
        {
            // 판을 문 폭만큼만 비워야 한다. 통째로 지우면 방 하나 붙였다고 벽 한 장이 사라지고,
            // 그 벽 뒤가 우주면 걸어 나가진다.
            var ship = NewShip();
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            BakeCompartmentWall(ship, lounge);

            var spec = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            Register(spec);
            LastShiftBakedDoorways.Open(ship.transform);

            var door = LastShiftDoorways.Of(spec);
            Assert.That(Occupied(ship, door, door.MinFree - 0.3f, 1f), Is.True, "문 한쪽 옆 벽이 사라졌다.");
            Assert.That(Occupied(ship, door, door.MaxFree + 0.3f, 1f), Is.True, "문 반대쪽 옆 벽이 사라졌다.");
        }

        // ── 선체 벽 ─────────────────────────────────────────────────────────

        [Test]
        public void OpensTheHullWallForAModuleThatHangsOffIt()
        {
            // 부모가 선체(ParentIndex < 0)면 벽 주인은 배 루트다. 구획 루트를 찾아 들어가는
            // 경로와 갈라져 있어 따로 잰다 — 선체 판은 구획 안이 아니라 배 바로 밑에 있다.
            var ship = NewShip();
            const float hullHeight = LastShiftShipDimensions.CeilingInnerHeight;
            BakeSlab(ship.transform, "OuterHull_Back",
                new Vector3(0f, hullHeight * 0.5f, LastShiftShipDimensions.SideWallZ),
                new Vector3(LastShiftShipDimensions.SideWallSpan, hullHeight, Thickness));

            var spec = CoolingSpur(LastShiftCompartments.NextModuleIndex);
            Register(spec);

            Assert.That(Blocks(ship, spec), Is.True);
            var report = LastShiftBakedDoorways.Open(ship.transform);

            Assert.That(report.Missing, Is.Zero);
            Assert.That(report.Cut, Is.EqualTo(1));
            Assert.That(Blocks(ship, spec), Is.False, "선체 우현 벽이 안 뚫렸다.");
        }

        // ── 안 자르는 것 ────────────────────────────────────────────────────

        [Test]
        public void DoesNotCutDecorThatDoesNotBlockAnyone()
        {
            // 갑판 띠·격자 같은 장식은 콜라이더가 없어 승무원을 안 막는다. 그것까지 자르면
            // 문 앞 갑판 표시에 구멍이 나고, 그건 고쳐야 할 것이 아니라 망가뜨린 것이다.
            var ship = NewShip();
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            var root = CompartmentRoot(ship, lounge);
            var decor = BakeSlab(root, "DeckBand",
                new Vector3(0f, Height * 0.5f, -lounge.WidthZ * 0.5f - Thickness * 0.5f),
                new Vector3(lounge.LengthX, Height, Thickness));
            Object.DestroyImmediate(decor.GetComponent<Collider>());

            var spec = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            Register(spec);
            var report = LastShiftBakedDoorways.Open(ship.transform);

            Assert.That(report.Slabs, Is.Zero, "콜라이더 없는 장식을 잘랐다.");
            Assert.That(decor.transform.childCount, Is.Zero);
            Assert.That(root.childCount, Is.EqualTo(1), "장식 옆에 조각이 생겼다.");
        }

        [Test]
        public void CountsAnAlreadyOpenWallAsClearInsteadOfCutting()
        {
            // 조립기가 세운 그레이박스 모듈은 자식 문 구멍을 같이 뚫어 둔다. 그 자리에 절단기가
            // 또 손대면 이미 뚫린 구멍을 한 번 더 자르게 되고, 되돌릴 것이 없는 기록이 쌓인다.
            var ship = NewShip();
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            CompartmentRoot(ship, lounge);

            var spec = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            Register(spec);
            var report = LastShiftBakedDoorways.Open(ship.transform);

            Assert.That(report.Doorways, Is.EqualTo(1));
            Assert.That(report.Clear, Is.EqualTo(1));
            Assert.That(report.Cut, Is.Zero);
            Assert.That(LastShiftBakedDoorways.CutCount, Is.Zero);
        }

        [Test]
        public void ReportsAModuleWhoseWallOwnerIsNotInTheScene()
        {
            // 씬을 안 세우고 표만 만졌을 때다. 조용히 넘기면 "표에는 있고 걸어 들어갈 수는
            // 없는 방" 이 아무 신호 없이 생긴다 — 이 카드가 고치는 것이 바로 그 상태다.
            var ship = NewShip();
            var spec = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            Register(spec);

            var report = LastShiftBakedDoorways.Open(ship.transform);

            Assert.That(report.Doorways, Is.EqualTo(1));
            Assert.That(report.Missing, Is.EqualTo(1));
            Assert.That(report.Doorways, Is.EqualTo(report.Cut + report.Clear + report.Missing));
        }

        [Test]
        public void SkipsLockedModules()
        {
            // 잠긴 문은 구멍이 아니라 메운 판이다(§15.2). 뚫으면 그레이박스에서 잠긴 방이
            // 열린 방과 구별이 안 된다.
            var ship = NewShip();
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            BakeCompartmentWall(ship, lounge);

            var open = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            var locked = new LastShiftCompartmentSpec(
                open.Index, open.MinX, open.MaxX, open.MinZ, open.MaxZ,
                open.DoorPlane, open.DoorPlaneCoordinate, open.DoorCenter,
                open.ParentIndex, LastShiftCompartmentAccess.Locked);
            Register(locked);

            var report = LastShiftBakedDoorways.Open(ship.transform);

            Assert.That(report.Doorways, Is.Zero);
            Assert.That(Blocks(ship, locked), Is.True, "잠긴 모듈 앞 벽이 뚫렸다.");
        }

        // ── 되돌림 ──────────────────────────────────────────────────────────

        [Test]
        public void RestorePutsTheWallBackExactly()
        {
            // 배치 해제는 기항에서 일어난다. 그때 벽이 안 메워지면 방을 걷어낸 자리에
            // 구멍만 남고, 선체 벽이면 그 구멍이 우주로 난다.
            var ship = NewShip();
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            var wall = BakeCompartmentWall(ship, lounge);
            var position = wall.transform.localPosition;
            var scale = wall.transform.localScale;
            var root = wall.transform.parent;

            var spec = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            Register(spec);
            LastShiftBakedDoorways.Open(ship.transform);
            Assert.That(root.childCount, Is.GreaterThan(1), "조각이 안 생겼다 — 되돌릴 것이 없다.");

            var restored = LastShiftBakedDoorways.Restore();

            Assert.That(restored, Is.EqualTo(1));
            Assert.That(LastShiftBakedDoorways.CutCount, Is.Zero);
            Assert.That(root.childCount, Is.EqualTo(1), "잘라 만든 조각이 남았다.");
            Assert.That(wall.transform.localPosition, Is.EqualTo(position).Using(Vector3Comparer));
            Assert.That(wall.transform.localScale, Is.EqualTo(scale).Using(Vector3Comparer));
            Assert.That(Blocks(ship, spec), Is.True, "메웠는데 문 자리가 아직 비어 있다.");
        }

        [Test]
        public void OpeningTwiceDoesNotStackCuts()
        {
            // Open 은 먼저 되돌린다. 안 그러면 배치 확정 두 번에 같은 벽이 두 번 잘려
            // 조각이 겹쳐 서고, 되돌리면 그중 한 겹만 사라진다.
            var ship = NewShip();
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            var wall = BakeCompartmentWall(ship, lounge);
            var root = wall.transform.parent;

            var spec = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            Register(spec);

            LastShiftBakedDoorways.Open(ship.transform);
            var after = root.childCount;
            LastShiftBakedDoorways.Open(ship.transform);

            Assert.That(root.childCount, Is.EqualTo(after));
            Assert.That(LastShiftBakedDoorways.CutCount, Is.EqualTo(1));
            Assert.That(Blocks(ship, spec), Is.False);
        }

        [Test]
        public void TwoModulesOnTheSameWallEachGetTheirOwnOpening()
        {
            // 두 번째 문이 첫 번째가 만든 조각을 물 수 있다. 후보를 한 번만 모아 두고 조각을
            // 안 붙이면 두 번째 문이 그 조각을 못 보고 지나가 한쪽만 뚫린다.
            var ship = NewShip();
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            var wall = BakeCompartmentWall(ship, lounge);
            var root = wall.transform.parent;

            var first = SpurOnLounge(LastShiftCompartments.NextModuleIndex, lounge.MinX + 1f);
            Register(first);
            var second = SpurOnLounge(LastShiftCompartments.NextModuleIndex, lounge.MaxX - 1f);
            Register(second);

            var report = LastShiftBakedDoorways.Open(ship.transform);

            Assert.That(report.Doorways, Is.EqualTo(2));
            Assert.That(report.Cut, Is.EqualTo(2));
            Assert.That(Blocks(ship, first), Is.False, "선미 쪽 문이 안 뚫렸다.");
            Assert.That(Blocks(ship, second), Is.False, "선수 쪽 문이 안 뚫렸다.");

            LastShiftBakedDoorways.Restore();
            Assert.That(root.childCount, Is.EqualTo(1), "두 번 자른 벽이 한 장으로 안 돌아왔다.");
            Assert.That(Blocks(ship, first), Is.True);
            Assert.That(Blocks(ship, second), Is.True);
        }

        // ── 조립기와의 배선 ─────────────────────────────────────────────────

        [Test]
        public void AssemblerRebuildOpensAndClearCloses()
        {
            // 조립기가 방을 다 세운 <b>뒤에</b> 뚫어야 한다. 먼저 뚫으면 부모가 모듈일 때
            // 자른 판을 그 뒤에 다시 세우게 되고 구멍이 도로 메워진다.
            var ship = NewShip();
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            BakeCompartmentWall(ship, lounge);

            var spec = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            Register(spec);

            Assert.That(LastShiftModuleAssembler.Rebuild(ship.transform, null), Is.EqualTo(1));
            Assert.That(Blocks(ship, spec), Is.False, "방은 섰는데 부모 벽이 안 뚫렸다.");

            LastShiftModuleAssembler.Clear(ship.transform);
            Assert.That(Blocks(ship, spec), Is.True, "방을 걷어냈는데 벽에 구멍이 남았다.");
        }

        // ── 문틀 표 ─────────────────────────────────────────────────────────

        [Test]
        public void TheDoorwayTableGrowsWithTheModuleTable()
        {
            // 축 B 가 §6 에 남긴 항목이다. 표가 정적이면 모듈 문 앞은 드레싱 검사에서 영영
            // 안 보이고, 구운 소품이 새 문을 막고 있어도 통과한다.
            var before = LastShiftDoorways.All.Length;
            var spec = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            var index = Register(spec);

            var all = LastShiftDoorways.All;
            Assert.That(all.Length, Is.EqualTo(before + 1));
            Assert.That(LastShiftDoorways.Revision, Is.EqualTo(LastShiftCompartments.Revision));

            var door = all.Single(d => d.Name == LastShiftCompartments.NameOf(spec));
            Assert.That(door.PlaneAxis, Is.EqualTo(spec.DoorPlane));
            Assert.That(door.Plane, Is.EqualTo(spec.DoorPlaneCoordinate).Within(Tolerance));
            Assert.That(door.Center, Is.EqualTo(spec.DoorCenter).Within(Tolerance));

            LastShiftCompartments.TryRemove(index);
            Assert.That(LastShiftDoorways.All.Length, Is.EqualTo(before),
                "모듈을 뺐는데 문이 표에 남았다 — 아무 방도 안 붙은 자리를 계속 비워 두라고 요구한다.");
        }

        [Test]
        public void LockedModulesStayOutOfTheDoorwayTable()
        {
            var before = LastShiftDoorways.All.Length;
            var open = SternSpur(LastShiftCompartments.NextModuleIndex, (int)LastShiftCompartment.Lounge);
            Register(new LastShiftCompartmentSpec(
                open.Index, open.MinX, open.MaxX, open.MinZ, open.MaxZ,
                open.DoorPlane, open.DoorPlaneCoordinate, open.DoorCenter,
                open.ParentIndex, LastShiftCompartmentAccess.Locked));

            Assert.That(LastShiftDoorways.All.Length, Is.EqualTo(before),
                "잠긴 모듈이 문 표에 들어갔다 — 메운 판 앞을 통행 예약 구역으로 잡는다.");
        }

        // ── 표본 ────────────────────────────────────────────────────────────

        private static int Register(in LastShiftCompartmentSpec candidate)
        {
            Assert.That(LastShiftCompartments.TryRegister(candidate, out var index, out var verdict),
                Is.True, $"표본이 판정기에 물린다({verdict.Rejection}) — 테스트가 재려는 것과 무관한 사유다.");
            return index;
        }

        /// <summary>라운지 좌현 면에서 바깥으로 뻗는 칸. 표 테스트의 같은 이름 표본과 같은 자리다.</summary>
        private static LastShiftCompartmentSpec SternSpur(int index, int parentIndex)
        {
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            return new LastShiftCompartmentSpec(
                index, lounge.MinX, lounge.MinX + 3f, lounge.MinZ - 3f, lounge.MinZ,
                LastShiftDoorPlane.AlongZ, lounge.MinZ, lounge.MinX + 1.5f,
                parentIndex, LastShiftCompartmentAccess.Open);
        }

        /// <summary>
        /// 선체 우현 긴 벽에 문을 내고 선수 쪽으로 눕는 칸. 표 테스트의 같은 이름 표본과 같은
        /// 자리이고, 이 자리를 고른 이유는 정본 구획 열한 개 중 어느 것도 여기 없기 때문이다.
        /// </summary>
        private static LastShiftCompartmentSpec CoolingSpur(int index)
        {
            var doorX = LastShiftShipDimensions.ZoneCenterX(LastShiftZone.Cooling);
            var minZ = LastShiftShipDimensions.SideWallZ;
            return new LastShiftCompartmentSpec(
                index, doorX - 9f, doorX + 1f, minZ, minZ + 2f,
                LastShiftDoorPlane.AlongZ, minZ, doorX,
                -1, LastShiftCompartmentAccess.Open);
        }

        /// <summary>라운지 좌현 면의 <paramref name="doorX"/> 에 문을 내는 칸. 겹치지 않게 얕게 눕힌다.</summary>
        private static LastShiftCompartmentSpec SpurOnLounge(int index, float doorX)
        {
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            return new LastShiftCompartmentSpec(
                index, doorX - 1f, doorX + 1f, lounge.MinZ - 2f, lounge.MinZ,
                LastShiftDoorPlane.AlongZ, lounge.MinZ, doorX,
                (int)LastShiftCompartment.Lounge, LastShiftCompartmentAccess.Open);
        }

        // ── 씬 ──────────────────────────────────────────────────────────────

        private GameObject NewShip()
        {
            var ship = new GameObject("Ship");
            spawned.Add(ship);
            return ship;
        }

        /// <summary>씬 빌더가 세우는 계층을 그대로 흉내낸다 — <c>Ship/Compartments/Compartment_*</c>.</summary>
        private static Transform CompartmentRoot(GameObject ship, in LastShiftCompartmentSpec spec)
        {
            var yard = ship.transform.Find("Compartments");
            if (yard == null)
            {
                var created = new GameObject("Compartments");
                created.transform.SetParent(ship.transform, false);
                yard = created.transform;
            }

            var name = LastShiftCompartments.NameOf(spec);
            var root = yard.Find(name);
            if (root != null) return root;

            var made = new GameObject(name);
            made.transform.SetParent(yard, false);
            made.transform.localPosition = new Vector3(spec.CenterX, 0f, spec.CenterZ);
            return made.transform;
        }

        /// <summary>
        /// 이 구획의 좌현(<c>ZMin</c>) 면을 통짜 판으로 굽는다. 씬 빌더
        /// <c>CreateWallWithOpenings</c> 가 구멍 없이 세울 때와 같은 자리·크기다.
        /// </summary>
        private static GameObject BakeCompartmentWall(GameObject ship, in LastShiftCompartmentSpec spec)
        {
            var root = CompartmentRoot(ship, spec);
            return BakeSlab(root, "Wall_ZMin_0",
                new Vector3(0f, Height * 0.5f, -spec.WidthZ * 0.5f - Thickness * 0.5f),
                new Vector3(spec.LengthX + 2f * Thickness, Height, Thickness));
        }

        private static GameObject BakeSlab(
            Transform parent, string name, Vector3 localPosition, Vector3 scale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = scale;
            return cube;
        }

        // ── 측정 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 이 모듈 문이 아직 막혀 있는가. <b>조각 수가 아니라 사각형이 비었는지를 잰다</b> —
        /// 자르는 방식이 바뀌어도 사람이 지나갈 수 있는지는 안 흔들려야 한다.
        ///
        /// 구멍 안 세 점을 본다. 한가운데만 보면 문 폭의 절반을 막은 판을 놓친다.
        /// </summary>
        private static bool Blocks(GameObject ship, in LastShiftCompartmentSpec spec)
        {
            var door = LastShiftDoorways.Of(spec);
            var inset = LastShiftZoneDoor.OpeningWidth * 0.25f;
            foreach (var free in new[] { door.Center - inset, door.Center, door.Center + inset })
            foreach (var y in new[] { 0.3f, 1f, LastShiftZoneDoor.OpeningHeight - 0.1f })
                if (Occupied(ship, door, free, y)) return true;

            return false;
        }

        /// <summary>
        /// 문 평면 위 한 자리(자유축 <paramref name="free"/>, 높이 <paramref name="y"/>)가 판에 물렸는가.
        ///
        /// <b>점이 아니라 얇은 상자로 잰다.</b> 문 평면 좌표는 벽 판의 안쪽 <b>면</b>이라
        /// (구획 면과 판 두께의 규약) 점으로 재면 언제나 판 경계 위에 놓여 답이 부동소수점
        /// 나름이 된다. 문을 지나는 축으로 판 두께만큼 두께를 준 상자와 겹치는지를 본다.
        /// </summary>
        private static bool Occupied(GameObject ship, in LastShiftDoorway door, float free, float y)
        {
            const float probe = 0.02f;
            var alongX = door.PlaneAxis == LastShiftDoorPlane.AlongX;
            var center = alongX
                ? new Vector3(door.Plane, y, free)
                : new Vector3(free, y, door.Plane);
            var size = alongX
                ? new Vector3(Thickness * 2f, probe, probe)
                : new Vector3(probe, probe, Thickness * 2f);
            var window = new Bounds(center, size);

            foreach (var renderer in ship.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (renderer.GetComponent<Collider>() == null) continue;
                if (renderer.bounds.Intersects(window)) return true;
            }

            return false;
        }

        private static readonly IEqualityComparer<Vector3> Vector3Comparer = new ApproximateVector3();

        private sealed class ApproximateVector3 : IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < Tolerance * Tolerance;
            public int GetHashCode(Vector3 value) => value.GetHashCode();
        }
    }
}
