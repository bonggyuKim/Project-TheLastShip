using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 모듈 조립(자유 배치 축 C). <b>여기서 지키는 것은 "표에 들어온 칸과 씬에 선 방이 같다" 다.</b>
    ///
    /// 축 D 는 무엇을 놓아도 되는지를, 축 A 는 그 방의 압력이 어디서 나오는지를, 축 B 는 표가
    /// 늘어나도 정본이 안 흔들리는지를 잰다. 이 파일이 답하는 물음은 그 셋 뒤에 있다 —
    /// <b>표가 허락한 칸이 실제로 그 자리에, 그 방향으로, 그 크기로 서는가.</b>
    ///
    /// 회전이 이 파일의 절반이다. <c>90°</c> 4단에서 발자국과 문을 <b>같은 회전에서 같이</b>
    /// 봐야 한다는 것이 조립기 설계의 요지이고, 그게 뒤집히면 문이 부모 반대쪽을 보고 선 방이
    /// 나온다 — 표에서는 걸어갈 수 있는 것으로 세어지는 방이다.
    ///
    /// 정적 표를 만지므로 <see cref="LastShiftCompartments.ClearModules"/> 가 매 테스트 뒤에
    /// 붙고, 세운 씬 오브젝트도 같이 걷어낸다.
    /// </summary>
    public sealed class LastShiftModuleAssemblyTests
    {
        private const float Tolerance = 0.001f;

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void CleanUp()
        {
            LastShiftCompartments.ClearModules();
            foreach (var target in spawned)
                if (target != null) Object.DestroyImmediate(target);
            spawned.Clear();
        }

        // ── 발자국 ──────────────────────────────────────────────────────────

        [Test]
        public void AFootprintReadsTheDoorFaceOffTheSpec()
        {
            // 네 면을 다 훑는다. DoorPlane 하나가 두 면을 덮으므로(AlongX 가 MinX·MaxX) 축만
            // 보고 면을 정하면 절반이 180° 뒤집힌다.
            var cases = new (LastShiftDoorPlane Plane, float Coordinate, LastShiftModuleFace Face)[]
            {
                (LastShiftDoorPlane.AlongX, 10f, LastShiftModuleFace.MinX),
                (LastShiftDoorPlane.AlongX, 14f, LastShiftModuleFace.MaxX),
                (LastShiftDoorPlane.AlongZ, 20f, LastShiftModuleFace.MinZ),
                (LastShiftDoorPlane.AlongZ, 26f, LastShiftModuleFace.MaxZ)
            };

            foreach (var (plane, coordinate, expected) in cases)
            {
                var spec = new LastShiftCompartmentSpec(
                    LastShiftCompartments.FixedCount, 10f, 14f, 20f, 26f,
                    plane, coordinate, plane == LastShiftDoorPlane.AlongX ? 24f : 13f,
                    -1, LastShiftCompartmentAccess.Open);
                var footprint = LastShiftModuleFootprint.Of(spec);

                Assert.That(footprint.DoorFace, Is.EqualTo(expected),
                    $"{plane} {coordinate} 를 {expected} 로 안 읽었다 — 같은 축의 두 면이 안 갈렸다.");
                Assert.That(footprint.LengthX, Is.EqualTo(4f).Within(Tolerance));
                Assert.That(footprint.WidthZ, Is.EqualTo(6f).Within(Tolerance));

                // 오프셋은 방 중심 기준 상대값이다(중심은 x=12, z=23). 절대 좌표가 새면
                // 프리팹이 자기가 어디 놓일지 알아야 하는 물건이 된다.
                Assert.That(footprint.DoorOffset, Is.EqualTo(1f).Within(Tolerance),
                    "문 중심을 방 중심 기준 상대값으로 안 읽었다.");
            }
        }

        [Test]
        public void ADoorWiderThanItsOwnFaceIsNotAValidAnchor()
        {
            // 문 폭이 면을 넘치면 모서리에 틈이 남아 그레이박스가 안 닫힌다. 프리팹은 표를 안
            // 거치므로 DoorSitsOnOwnBoundary 가 고정 구획에 걸어 주는 것을 여기서 한 번 더 건다.
            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;

            Assert.That(new LastShiftModuleFootprint(4f, 4f, LastShiftModuleFace.MinX, 0f).DoorFits,
                Is.True);
            Assert.That(new LastShiftModuleFootprint(4f, 4f, LastShiftModuleFace.MinX, 2f - half).DoorFits,
                Is.True, "문이 면 끝에 딱 붙은 것은 유효하다 — 틈이 안 남는다.");
            Assert.That(new LastShiftModuleFootprint(4f, 4f, LastShiftModuleFace.MinX, 2f).DoorFits,
                Is.False, "문 절반이 면 밖으로 나갔는데 유효로 읽혔다.");
            Assert.That(new LastShiftModuleFootprint(1f, 1f, LastShiftModuleFace.MinZ, 0f).DoorFits,
                Is.False, "문보다 좁은 방이 유효로 읽혔다.");
            Assert.That(new LastShiftModuleFootprint(0f, 4f, LastShiftModuleFace.MinX, 0f).DoorFits,
                Is.False, "부피 없는 방이 유효로 읽혔다.");
        }

        // ── 회전 ────────────────────────────────────────────────────────────

        [Test]
        public void QuarterTurnsAgreeWithUnityYaw()
        {
            // 정수 회전을 쓰는 이유가 이 테스트다 — 값은 Quaternion 과 같아야 하고, 잔차는
            // 없어야 한다. 갈리면 문점 비교 허용오차가 조용히 먹힌다.
            var samples = new[] { new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-2f, 3.5f) };

            for (var turns = 0; turns < 4; turns++)
            foreach (var sample in samples)
            {
                var byInteger = LastShiftModuleAssembler.Rotate(sample, turns);
                var byQuaternion = Quaternion.Euler(0f, turns * 90f, 0f) *
                                   new Vector3(sample.x, 0f, sample.y);

                Assert.That(byInteger.x, Is.EqualTo(byQuaternion.x).Within(0.0001f),
                    $"turns={turns} {sample} 의 x 가 유니티 yaw 와 다르다.");
                Assert.That(byInteger.y, Is.EqualTo(byQuaternion.z).Within(0.0001f),
                    $"turns={turns} {sample} 의 z 가 유니티 yaw 와 다르다.");
            }

            Assert.That(LastShiftModuleAssembler.Rotate(new Vector2(1f, 0f), 4),
                Is.EqualTo(new Vector2(1f, 0f)), "네 바퀴가 제자리로 안 돌아왔다.");
        }

        // ── 맞춤 ────────────────────────────────────────────────────────────

        [Test]
        public void APrefabAuthoredForTheSpecFitsWithoutRotating()
        {
            var spec = CoolingSpur(LastShiftCompartments.FixedCount, link: 0, parentIndex: -1);

            Assert.That(
                LastShiftModuleAssembler.TryFit(
                    LastShiftModuleFootprint.Of(spec), spec, out var turns, out var fit),
                Is.True);
            Assert.That(turns, Is.Zero, "제 치수로 만든 프리팹이 안 돌아야 하는데 돌았다.");
            Assert.That(fit, Is.EqualTo(LastShiftModuleFit.Fits));
        }

        [Test]
        public void APrefabAuthoredSidewaysFitsByRotating()
        {
            // 프리팹은 문을 MinX 에 두고 z 로 길게 누워 있고, 표의 칸은 문을 MinZ 에 두고 x 로
            // 길게 눕는다. 270° 에서 MinX 가 MinZ 로 가고 발자국의 x·z 가 맞바뀐다.
            var spec = CoolingSpur(LastShiftCompartments.FixedCount, link: 0, parentIndex: -1);
            var sideways = new LastShiftModuleFootprint(
                spec.WidthZ, spec.LengthX, LastShiftModuleFace.MinX, -4f);

            Assert.That(LastShiftModuleAssembler.TryFit(sideways, spec, out var turns, out var fit),
                Is.True, $"눕힌 프리팹이 안 맞았다({fit}).");
            Assert.That(turns, Is.EqualTo(3), "270° 가 아닌 회전이 골라졌다 — 문이 다른 면에 선다.");
        }

        [Test]
        public void AFootprintThatNeverMatchesIsReportedAsFootprintMismatch()
        {
            var spec = CoolingSpur(LastShiftCompartments.FixedCount, link: 0, parentIndex: -1);
            var wrongSize = new LastShiftModuleFootprint(3f, 3f, LastShiftModuleFace.MinZ, 0f);

            Assert.That(LastShiftModuleAssembler.TryFit(wrongSize, spec, out _, out var fit), Is.False);
            Assert.That(fit, Is.EqualTo(LastShiftModuleFit.FootprintMismatch),
                "치수가 틀린 것과 방향이 틀린 것이 안 갈렸다 — 로그를 보고 누가 고쳐야 하는지 모른다.");
        }

        [Test]
        public void ASquareRoomWithTheDoorInTheWrongPlaceIsReportedAsDoorMismatch()
        {
            // 정사각 방은 네 회전 다 발자국이 맞는다. 발자국만 보고 먼저 맞은 회전을 쓰면
            // 문이 엉뚱한 면에 서고, 그게 이 테스트가 막는 것이다.
            var spec = new LastShiftCompartmentSpec(
                LastShiftCompartments.FixedCount, 10f, 14f, 20f, 24f,
                LastShiftDoorPlane.AlongX, 10f, 22f, -1, LastShiftCompartmentAccess.Open);
            var offCenter = new LastShiftModuleFootprint(4f, 4f, LastShiftModuleFace.MinX, 1f);

            Assert.That(LastShiftModuleAssembler.TryFit(offCenter, spec, out _, out var fit), Is.False);
            Assert.That(fit, Is.EqualTo(LastShiftModuleFit.DoorMismatch));
        }

        [Test]
        public void AnAnchorThatCannotHoldItsOwnDoorIsRejectedBeforeRotating()
        {
            var spec = CoolingSpur(LastShiftCompartments.FixedCount, link: 0, parentIndex: -1);
            var tooNarrow = new LastShiftModuleFootprint(1f, 1f, LastShiftModuleFace.MinZ, 0f);

            Assert.That(LastShiftModuleAssembler.TryFit(tooNarrow, spec, out _, out var fit), Is.False);
            Assert.That(fit, Is.EqualTo(LastShiftModuleFit.AnchorInvalid));
        }

        [Test]
        public void ThePaletteIsSearchedInOrderAndAnchorlessEntriesAreSkipped()
        {
            var spec = CoolingSpur(LastShiftCompartments.FixedCount, link: 0, parentIndex: -1);
            var target = LastShiftModuleFootprint.Of(spec);

            var anchorless = Spawn(new GameObject("Anchorless"));
            var wrongSize = SpawnPrefab("WrongSize", 3f, 3f, LastShiftModuleFace.MinZ, 0f);
            var fitting = SpawnPrefab("Fitting", target.LengthX, target.WidthZ,
                target.DoorFace, target.DoorOffset);

            var palette = Palette(anchorless, wrongSize, fitting);

            Assert.That(LastShiftModuleAssembler.TryPick(palette, spec, out var picked, out var turns),
                Is.True);
            Assert.That(picked, Is.SameAs(fitting),
                "앵커 없는 항목이나 치수가 틀린 항목이 골라졌다.");
            Assert.That(turns, Is.Zero);

            Assert.That(
                LastShiftModuleAssembler.TryPick(Palette(anchorless, wrongSize), spec, out _, out _),
                Is.False, "맞는 프리팹이 없는데 골랐다고 답했다.");
            Assert.That(LastShiftModuleAssembler.TryPick(null, spec, out _, out _), Is.False);
        }

        // ── 조립 ────────────────────────────────────────────────────────────

        [Test]
        public void AGreyboxModuleStandsAtTheSpecCenterAndLeavesItsOwnDoorFaceToTheParent()
        {
            var index = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            var spec = LastShiftCompartments.At(index);
            var root = Build(spec, null);

            Assert.That(root.name, Is.EqualTo(LastShiftCompartments.ModuleName(index)));
            Assert.That(root.transform.localPosition.x, Is.EqualTo(spec.CenterX).Within(Tolerance));
            Assert.That(root.transform.localPosition.y, Is.EqualTo(0f).Within(Tolerance),
                "바닥이 y=0 이 아니면 아트 프리팹 원점 규약과 그레이박스가 갈린다.");
            Assert.That(root.transform.localPosition.z, Is.EqualTo(spec.CenterZ).Within(Tolerance));

            Assert.That(Child(root, "Floor"), Is.Not.Null);
            Assert.That(Child(root, "Ceiling"), Is.Not.Null);

            // 문이 MinZ 에 있으므로 그 면은 부모가 세운다. 여기서 세우면 방이 문 자리를 판으로
            // 메우고, 표에서는 여전히 걸어갈 수 있는 방으로 세어진다.
            Assert.That(spec.DoorPlane, Is.EqualTo(LastShiftDoorPlane.AlongZ));
            Assert.That(ChildCountStartingWith(root, "Wall_ZMin"), Is.Zero,
                "자기 안쪽 문이 놓인 면을 세웠다.");
            Assert.That(ChildCountStartingWith(root, "Wall_XMin"), Is.GreaterThan(0));
            Assert.That(ChildCountStartingWith(root, "Wall_XMax"), Is.GreaterThan(0));
            Assert.That(ChildCountStartingWith(root, "Wall_ZMax"), Is.GreaterThan(0));
        }

        [Test]
        public void AGreyboxWallIsCutWhereAChildModuleDoorSits()
        {
            var root = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            var leaf = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 1, parentIndex: root));

            // 잎의 문은 뿌리의 MaxZ 면에 앉는다. 뿌리가 그 면을 통짜로 세우면 사슬 두 칸째가
            // 벽에 막힌 방이 되고, 표는 그걸 걸어갈 수 있는 것으로 센다.
            var leafSpec = LastShiftCompartments.At(leaf);
            var rootSpec = LastShiftCompartments.At(root);
            Assert.That(leafSpec.DoorPlaneCoordinate, Is.EqualTo(rootSpec.MaxZ).Within(Tolerance));

            var built = Build(rootSpec, null);

            Assert.That(Child(built, "Wall_ZMax_0"), Is.Not.Null);
            Assert.That(Child(built, "Wall_ZMax_1"), Is.Not.Null,
                "구멍 양옆 두 장이 아니다 — 벽이 안 잘렸다.");
            Assert.That(Child(built, "Wall_ZMax_2"), Is.Null, "판이 한 장 더 섰다.");
            Assert.That(Child(built, "Wall_ZMax_Lintel_0"), Is.Not.Null,
                "인방이 없다 — 문 높이에서 천장까지 뚫려 그림과 통행 범위가 어긋난다.");
        }

        [Test]
        public void ALockedChildDoesNotGetAHole()
        {
            var root = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            var locked = CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 1, parentIndex: root);
            Register(new LastShiftCompartmentSpec(
                locked.Index, locked.MinX, locked.MaxX, locked.MinZ, locked.MaxZ,
                locked.DoorPlane, locked.DoorPlaneCoordinate, locked.DoorCenter,
                locked.ParentIndex, LastShiftCompartmentAccess.Locked));

            var built = Build(LastShiftCompartments.At(root), null);

            Assert.That(Child(built, "Wall_ZMax_0"), Is.Not.Null);
            Assert.That(Child(built, "Wall_ZMax_1"), Is.Null,
                "잠긴 문에 구멍이 뚫렸다 — 그레이박스에서 잠긴 문은 메운 판이다.");
            Assert.That(Child(built, "Wall_ZMax_Lintel_0"), Is.Null);
        }

        [Test]
        public void APrefabStandsInsteadOfTheGreyboxAndCarriesTheChosenYaw()
        {
            var index = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            var spec = LastShiftCompartments.At(index);

            // 눕혀 만든 프리팹을 준다. 조립기가 270° 를 골라야 문이 부모 쪽을 본다.
            var sideways = SpawnPrefab("Sideways", spec.WidthZ, spec.LengthX,
                LastShiftModuleFace.MinX, -4f);
            var root = Build(spec, Palette(sideways));

            var shell = Child(root, "Shell");
            Assert.That(shell, Is.Not.Null, "프리팹을 줬는데 껍데기가 안 섰다.");
            Assert.That(Child(root, "Floor"), Is.Null,
                "프리팹이 섰는데 그레이박스도 같이 섰다 — 방이 두 겹이다.");
            Assert.That(shell.localEulerAngles.y, Is.EqualTo(270f).Within(0.05f));
            Assert.That(shell.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void TheAssemblerRefusesFixedCompartments()
        {
            // 고정 열하나는 배 프리팹에 구워져 있다. 여기서 또 세우면 같은 방이 두 겹으로 서고
            // 문이 이중으로 막힌다.
            Assert.Throws<System.ArgumentException>(
                () => Build(LastShiftCompartments.Of(LastShiftCompartment.Quarters), null));
        }

        // ── 다시 세우기 ─────────────────────────────────────────────────────

        [Test]
        public void RebuildStandsEveryModuleRowAndNothingElse()
        {
            Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            Register(SternSpur(LastShiftCompartments.NextModuleIndex, link: 0,
                parentIndex: (int)LastShiftCompartment.Quarters));

            var parent = Spawn(new GameObject("Ship"));
            var built = LastShiftModuleAssembler.Rebuild(parent.transform, null);

            Assert.That(built, Is.EqualTo(LastShiftCompartments.ModuleCount));
            var yard = parent.transform.Find(LastShiftModuleAssembler.YardName);
            Assert.That(yard, Is.Not.Null);
            Assert.That(yard.childCount, Is.EqualTo(LastShiftCompartments.ModuleCount),
                "고정 구획까지 셌거나 모듈 하나를 빠뜨렸다.");
        }

        [Test]
        public void RebuildingTwiceDoesNotStackRooms()
        {
            // 표는 해제할 때 뒤 칸을 당기므로 칸과 씬 오브젝트를 짝지어 두지 않는다. 그 대가로
            // Rebuild 는 반드시 먼저 비워야 한다 — 안 비우면 판이 겹쳐 서고, 겹친 벽은 씬에서
            // z-파이팅으로만 드러난다.
            Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));

            var parent = Spawn(new GameObject("Ship"));
            LastShiftModuleAssembler.Rebuild(parent.transform, null);
            var yard = parent.transform.Find(LastShiftModuleAssembler.YardName);

            LastShiftModuleAssembler.Rebuild(parent.transform, null);

            Assert.That(yard.childCount, Is.EqualTo(1), "두 번 세워 방이 두 겹이 됐다.");
            Assert.That(parent.transform.Find(LastShiftModuleAssembler.YardName), Is.SameAs(yard),
                "칸 자체를 다시 만들었다 — 그 Transform 을 참조로 물고 있는 쪽이 끊긴다.");
        }

        [Test]
        public void AnEmptyTableStandsAnEmptyYard()
        {
            var parent = Spawn(new GameObject("Ship"));

            Assert.That(LastShiftModuleAssembler.Rebuild(parent.transform, null), Is.Zero);
            Assert.That(parent.transform.Find(LastShiftModuleAssembler.YardName).childCount, Is.Zero,
                "모듈이 없는 배에 방이 섰다.");
        }

        // ── 표본 ────────────────────────────────────────────────────────────

        private static int Register(in LastShiftCompartmentSpec candidate)
        {
            Assert.That(LastShiftCompartments.TryRegister(candidate, out var index, out var verdict),
                Is.True, $"표본이 판정기에 물린다({verdict.Rejection}) — 조립과 무관한 사유다.");
            return index;
        }

        private GameObject Build(in LastShiftCompartmentSpec spec, LastShiftModulePalette palette) =>
            Spawn(LastShiftModuleAssembler.Build(spec, null, palette));

        /// <summary>축 B 테스트와 같은 표본이다. 냉각실 우현 벽에서 선수 쪽으로 길게 눕는 칸.</summary>
        private static LastShiftCompartmentSpec CoolingSpur(int index, int link, int parentIndex)
        {
            const float roomDepth = 2f;
            var doorX = LastShiftShipDimensions.ZoneCenterX(LastShiftZone.Cooling);
            var minZ = LastShiftShipDimensions.SideWallZ + link * roomDepth;

            return new LastShiftCompartmentSpec(
                index, doorX - 9f, doorX + 1f, minZ, minZ + roomDepth,
                LastShiftDoorPlane.AlongZ, minZ, doorX,
                parentIndex, LastShiftCompartmentAccess.Open);
        }

        /// <summary>숙소 좌현 면에서 바깥으로 뻗는 칸. 구명정과 의무실을 둘 다 피한다.</summary>
        private static LastShiftCompartmentSpec SternSpur(int index, int link, int parentIndex)
        {
            const float roomDepth = 3f;
            var quarters = LastShiftCompartments.Of(LastShiftCompartment.Quarters);
            var maxZ = quarters.MinZ - link * roomDepth;

            return new LastShiftCompartmentSpec(
                index, quarters.MinX, quarters.MinX + 3f, maxZ - roomDepth, maxZ,
                LastShiftDoorPlane.AlongZ, maxZ, quarters.MinX + 1.5f,
                parentIndex, LastShiftCompartmentAccess.Open);
        }

        private LastShiftModulePalette Palette(params GameObject[] prefabs)
        {
            var palette = ScriptableObject.CreateInstance<LastShiftModulePalette>();
            palette.Configure(prefabs, null, null, null);
            return palette;
        }

        private GameObject SpawnPrefab(
            string name, float lengthX, float widthZ, LastShiftModuleFace face, float offset)
        {
            var prefab = Spawn(new GameObject(name));
            prefab.AddComponent<LastShiftModuleAnchor>().Configure(lengthX, widthZ, face, offset);
            return prefab;
        }

        private GameObject Spawn(GameObject target)
        {
            spawned.Add(target);
            return target;
        }

        private static Transform Child(GameObject root, string name) => root.transform.Find(name);

        private static int ChildCountStartingWith(GameObject root, string prefix)
        {
            var count = 0;
            for (var index = 0; index < root.transform.childCount; index++)
                if (root.transform.GetChild(index).name.StartsWith(prefix)) count++;
            return count;
        }
    }
}
