using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// <c>Resolve()</c> 오버레이(<see cref="LastShiftPlacedModules"/>)를 잰다.
    ///
    /// <b>여기서 지키는 것은 두 가지다.</b> 하나는 오버레이가 <b>비어 있을 때 아무것도 안 바꾼다</b>는
    /// 것 — 자유 배치가 하나도 안 붙은 지금 배에서 이 변경이 무해하다는 증거가 그것이고, 나머지
    /// EditMode 전부가 이 조건 위에서 돈다. 다른 하나는 조항 F-1 이다 — 모듈의 구역은 배치 시점
    /// 사슬 뿌리가 정하고, <b>모듈 자기 좌표로 다시 읽지 않는다.</b> 그게 뒤집히면 문을 닫아도
    /// 격리가 안 되는 배가 나온다(타당성 검토 §11-1).
    ///
    /// 정적 상태를 만지므로 <see cref="LastShiftPlacedModules.Clear"/> 가 매 테스트 앞뒤에 붙는다.
    /// 하나라도 새면 이 배의 진공 판정 전체가 다음 테스트로 흘러간다.
    /// </summary>
    public sealed class LastShiftPlacedModuleOverlayTests
    {
        private const float Tolerance = 0.001f;

        [SetUp]
        public void ClearBefore() => LastShiftPlacedModules.Clear();

        [TearDown]
        public void ClearAfter() => LastShiftPlacedModules.Clear();

        /// <summary>선체 안팎을 고루 훑는 표본. 구역 수를 안 박으려고 구역에서 파생시킨다.</summary>
        private static Vector3[] HullSamples()
        {
            var samples = new Vector3[LastShiftZoneAtlas.ZoneCount * 3 + 2];
            var next = 0;
            for (var zone = (LastShiftZone)0; (int)zone < LastShiftZoneAtlas.ZoneCount; zone++)
            {
                samples[next++] = new Vector3(LastShiftShipDimensions.ZoneMinX(zone), 0f, 0f);
                samples[next++] = new Vector3(LastShiftShipDimensions.ZoneCenterX(zone), 1f, 2f);
                samples[next++] = new Vector3(LastShiftShipDimensions.ZoneMaxX(zone), 0f, -2f);
            }

            samples[next++] = new Vector3(-LastShiftShipDimensions.InteriorLength, 0f, 0f);
            samples[next] = new Vector3(LastShiftShipDimensions.InteriorLength, 0f, 0f);
            return samples;
        }

        // ── 오버레이가 비어 있을 때 ──────────────────────────────────────────

        [Test]
        public void EmptyOverlayLeavesResolveIdenticalToHullBands()
        {
            // 이 카드가 기존 383개를 하나도 안 건드린다는 주장의 전부가 이 한 줄이다.
            Assert.That(LastShiftPlacedModules.Count, Is.Zero);
            Assert.That(LastShiftPlacedModules.ActiveCount, Is.Zero);

            foreach (var sample in HullSamples())
                Assert.That(LastShiftZoneAtlas.Resolve(sample),
                    Is.EqualTo(LastShiftZoneAtlas.ResolveHull(sample)),
                    $"{sample} 에서 오버레이가 비었는데도 답이 갈렸다 — 오버레이가 기본값을 갖고 있다.");
        }

        [Test]
        public void EmptyOverlayResolvesNothing()
        {
            Assert.That(LastShiftPlacedModules.TryResolve(Vector3.zero, out _), Is.False);
        }

        // ── 등록한 모듈이 선체 밴드를 덮는다 ──────────────────────────────────

        /// <summary>
        /// 선체 옆(<c>z</c> 바깥)으로 뻗은 모듈. 선체 밴드로 읽으면 <c>x</c> 만 보므로 조종석이
        /// 되는데, 사슬은 산소실 문에 붙어 있다 — 오버레이가 없을 때 정확히 이게 틀린다.
        /// </summary>
        private static (float MinX, float MaxX, float MinZ, float MaxZ) BowSideModule() => (
            LastShiftShipDimensions.ZoneMinX(LastShiftZone.Cockpit) + 1f,
            LastShiftShipDimensions.ZoneMinX(LastShiftZone.Cockpit) + 6f,
            LastShiftShipDimensions.SideWallZ + 1f,
            LastShiftShipDimensions.SideWallZ + 7f);

        [Test]
        public void RegisteredModuleOverridesHullBandInsideItsFootprint()
        {
            var box = BowSideModule();
            var inside = new Vector3((box.MinX + box.MaxX) * 0.5f, 1f, (box.MinZ + box.MaxZ) * 0.5f);

            Assert.That(LastShiftZoneAtlas.ResolveHull(inside), Is.EqualTo(LastShiftZone.Cockpit),
                "표본이 선체 밴드로는 조종석이어야 이 테스트가 의미가 있다 — 아니면 좌표를 다시 잡아야 한다.");

            LastShiftPlacedModules.Register(box.MinX, box.MaxX, box.MinZ, box.MaxZ, LastShiftZone.LifeSupport);

            Assert.That(LastShiftZoneAtlas.Resolve(inside), Is.EqualTo(LastShiftZone.LifeSupport),
                "등록한 모듈 안인데 선체 밴드가 이겼다 — 오버레이가 Resolve 앞에 안 섰다.");
            Assert.That(LastShiftZoneAtlas.ResolveHull(inside), Is.EqualTo(LastShiftZone.Cockpit),
                "ResolveHull 이 오버레이를 봤다 — 조항 F-1 의 뿌리 판정이 자기 참조가 된다.");
        }

        [Test]
        public void ZoneIsFrozenAtRegistrationNotReadFromOwnCoordinates()
        {
            // 조항 F-1. 모듈 발자국은 조종석 밴드에 있고 구역은 산소실이다 — 두 값이 어긋난 채로
            // 살아 있어야 "사슬 뿌리가 정한다"가 성립한다. 좌표에서 다시 읽는 구현은 여기서 죽는다.
            var box = BowSideModule();
            LastShiftPlacedModules.Register(box.MinX, box.MaxX, box.MinZ, box.MaxZ, LastShiftZone.LifeSupport);

            Assert.That(LastShiftPlacedModules.TryGet(0, out var module), Is.True);
            Assert.That(module.Zone, Is.EqualTo(LastShiftZone.LifeSupport));
            Assert.That(LastShiftZoneAtlas.ResolveHull(new Vector3(module.MinX, 0f, module.MinZ)),
                Is.Not.EqualTo(module.Zone),
                "발자국과 등록 구역이 같아져 버렸다 — 이 테스트가 아무것도 안 지키게 된다.");
        }

        [Test]
        public void PointOutsideFootprintFallsBackToHullBands()
        {
            var box = BowSideModule();
            LastShiftPlacedModules.Register(box.MinX, box.MaxX, box.MinZ, box.MaxZ, LastShiftZone.LifeSupport);

            var justOutsideZ = new Vector3((box.MinX + box.MaxX) * 0.5f, 1f, box.MaxZ + 0.5f);
            var justOutsideX = new Vector3(box.MinX - 0.5f, 1f, (box.MinZ + box.MaxZ) * 0.5f);

            Assert.That(LastShiftZoneAtlas.Resolve(justOutsideZ),
                Is.EqualTo(LastShiftZoneAtlas.ResolveHull(justOutsideZ)));
            Assert.That(LastShiftZoneAtlas.Resolve(justOutsideX),
                Is.EqualTo(LastShiftZoneAtlas.ResolveHull(justOutsideX)));
        }

        [Test]
        public void FootprintFacesBelongToTheModule()
        {
            // 벽에 붙어 선 승무원은 방 안에 있다. 이 규칙을 안 정해 두면 벽에 기댄 채 숨이 갈린다.
            var box = BowSideModule();
            LastShiftPlacedModules.Register(box.MinX, box.MaxX, box.MinZ, box.MaxZ, LastShiftZone.LifeSupport);

            foreach (var corner in new[]
                     {
                         new Vector3(box.MinX, LastShiftPlacedModules.DefaultFloorY, box.MinZ),
                         new Vector3(box.MaxX, LastShiftPlacedModules.DefaultFloorY, box.MaxZ),
                         new Vector3(box.MinX, LastShiftPlacedModules.DefaultCeilingY, box.MaxZ)
                     })
                Assert.That(LastShiftZoneAtlas.Resolve(corner), Is.EqualTo(LastShiftZone.LifeSupport),
                    $"{corner} 는 모듈 경계면인데 밖으로 샜다.");
        }

        [Test]
        public void BelowDeckIsNotSwallowedByAModule()
        {
            // 갑판 아래는 덕트·에어록 소관이다. 모듈이 y 를 안 보면 덕트 안 승무원이 머리 위
            // 모듈의 압력을 받아 산소를 안 태운다 — LastShiftBypassDuct 가 막아 둔 것과 같은 구멍이다.
            var box = BowSideModule();
            LastShiftPlacedModules.Register(box.MinX, box.MaxX, box.MinZ, box.MaxZ, LastShiftZone.LifeSupport);

            var underDeck = new Vector3(
                (box.MinX + box.MaxX) * 0.5f,
                LastShiftPlacedModules.DefaultFloorY - 1f,
                (box.MinZ + box.MaxZ) * 0.5f);

            Assert.That(LastShiftPlacedModules.TryResolve(underDeck, out _), Is.False);
            Assert.That(LastShiftZoneAtlas.Resolve(underDeck),
                Is.EqualTo(LastShiftZoneAtlas.ResolveHull(underDeck)));
        }

        [Test]
        public void SharedWallGoesToTheEarlierRegistration()
        {
            // 맞닿은 면은 겹침이 아니라 판정기를 통과한다 — 그래서 여기만 동점이 나고, 그 답이
            // 흔들리면 같은 좌표가 두 구역을 오간다.
            var box = BowSideModule();
            LastShiftPlacedModules.Register(box.MinX, box.MaxX, box.MinZ, box.MaxZ, LastShiftZone.LifeSupport);
            LastShiftPlacedModules.Register(box.MaxX, box.MaxX + 5f, box.MinZ, box.MaxZ, LastShiftZone.Power);

            var sharedWall = new Vector3(box.MaxX, 1f, (box.MinZ + box.MaxZ) * 0.5f);
            Assert.That(LastShiftZoneAtlas.Resolve(sharedWall), Is.EqualTo(LastShiftZone.LifeSupport));
        }

        // ── 등록·해제·이동 ───────────────────────────────────────────────────

        [Test]
        public void RemoveGivesTheCoordinateBackToTheHull()
        {
            var box = BowSideModule();
            var handle = LastShiftPlacedModules.Register(
                box.MinX, box.MaxX, box.MinZ, box.MaxZ, LastShiftZone.LifeSupport);
            var inside = new Vector3((box.MinX + box.MaxX) * 0.5f, 1f, (box.MinZ + box.MaxZ) * 0.5f);

            Assert.That(LastShiftPlacedModules.Remove(handle), Is.True);
            Assert.That(LastShiftPlacedModules.Remove(handle), Is.False, "두 번 해제가 통과했다.");
            Assert.That(LastShiftPlacedModules.ActiveCount, Is.Zero);
            Assert.That(LastShiftPlacedModules.Count, Is.Zero, "꼬리 칸이 안 걷혔다 — 매 tick 루프가 안 짧아진다.");
            Assert.That(LastShiftZoneAtlas.Resolve(inside), Is.EqualTo(LastShiftZoneAtlas.ResolveHull(inside)));
        }

        [Test]
        public void RemovedSlotIsReusedAndHandlesStayValid()
        {
            var box = BowSideModule();
            var first = LastShiftPlacedModules.Register(
                box.MinX, box.MaxX, box.MinZ, box.MaxZ, LastShiftZone.LifeSupport);
            var second = LastShiftPlacedModules.Register(
                box.MaxX + 1f, box.MaxX + 5f, box.MinZ, box.MaxZ, LastShiftZone.Power);

            LastShiftPlacedModules.Remove(first);
            var third = LastShiftPlacedModules.Register(
                box.MaxX + 6f, box.MaxX + 9f, box.MinZ, box.MaxZ, LastShiftZone.Cooling);

            Assert.That(third, Is.EqualTo(first), "빈 칸을 안 재사용하고 표를 늘렸다.");
            Assert.That(LastShiftPlacedModules.TryGet(second, out var kept), Is.True,
                "살아 있는 핸들이 해제 때문에 어긋났다 — 배열을 당겨왔다는 뜻이다.");
            Assert.That(kept.Zone, Is.EqualTo(LastShiftZone.Power));
        }

        [Test]
        public void ReplaceMovesTheFootprintWithoutChangingTheHandle()
        {
            var box = BowSideModule();
            var handle = LastShiftPlacedModules.Register(
                box.MinX, box.MaxX, box.MinZ, box.MaxZ, LastShiftZone.LifeSupport);
            var wasInside = new Vector3((box.MinX + box.MaxX) * 0.5f, 1f, (box.MinZ + box.MaxZ) * 0.5f);

            Assert.That(LastShiftPlacedModules.TryReplace(
                handle, box.MinX + 20f, box.MaxX + 20f, box.MinZ, box.MaxZ, LastShiftZone.Power), Is.True);

            Assert.That(LastShiftZoneAtlas.Resolve(wasInside),
                Is.EqualTo(LastShiftZoneAtlas.ResolveHull(wasInside)), "옮기기 전 자리가 아직 덮여 있다.");
            Assert.That(LastShiftZoneAtlas.Resolve(new Vector3(wasInside.x + 20f, 1f, wasInside.z)),
                Is.EqualTo(LastShiftZone.Power));
            Assert.That(LastShiftPlacedModules.ActiveCount, Is.EqualTo(1), "옮기기가 칸을 하나 더 먹었다.");
        }

        [Test]
        public void TableGrowsPastInitialCapacity()
        {
            // 상한을 넘겨도 답이 안 바뀌는지. N=20 은 검토가 잡은 상한이지 코드가 강제하는 값이 아니다.
            const int placements = 40;
            var baseX = LastShiftShipDimensions.ZoneMinX(LastShiftZone.Cockpit);
            for (var index = 0; index < placements; index++)
                LastShiftPlacedModules.Register(
                    baseX + index * 2f, baseX + index * 2f + 1f,
                    LastShiftShipDimensions.SideWallZ + 1f, LastShiftShipDimensions.SideWallZ + 3f,
                    LastShiftZone.LifeSupport);

            Assert.That(LastShiftPlacedModules.ActiveCount, Is.EqualTo(placements));
            for (var index = 0; index < placements; index++)
                Assert.That(LastShiftZoneAtlas.Resolve(new Vector3(
                        baseX + index * 2f + 0.5f, 1f, LastShiftShipDimensions.SideWallZ + 2f)),
                    Is.EqualTo(LastShiftZone.LifeSupport),
                    $"{index} 번째 등록이 표를 늘리면서 사라졌다.");
        }

        // ── 판정기와의 경계 ──────────────────────────────────────────────────

        [Test]
        public void VerdictZoneStaysOnTheHullEvenWhenAModuleCoversTheRootDoor()
        {
            // 조항 F-1 의 실제 위험. 등록된 모듈이 선체 문 좌표를 덮으면, 판정기가 Resolve 를
            // 부르는 구현에서는 다음 배치의 구역이 그 모듈에서 나온다 — 등록 순서가 배의 격리
            // 구조를 정하게 된다. 판정기는 언제나 선체를 본다.
            var table = LastShiftPlacementRules.TableOf(LastShiftCompartments.Specs);
            var probe = table[0];
            Assert.That(LastShiftPlacementRules.TryChainToHull(table, probe, out _, out var hullDoor, out _), Is.True);

            var before = LastShiftPlacementRules.Evaluate(table, probe, ignoreIndex: 0, includeImpassable: true);

            var contrary = before.Zone == LastShiftZone.LifeSupport ? LastShiftZone.Cockpit : LastShiftZone.LifeSupport;
            LastShiftPlacedModules.Register(
                hullDoor.x - 2f, hullDoor.x + 2f, hullDoor.z - 2f, hullDoor.z + 2f, contrary);

            Assert.That(LastShiftZoneAtlas.Resolve(hullDoor), Is.EqualTo(contrary),
                "표본 좌표가 모듈 안이 아니다 — 이 테스트가 아무것도 안 지킨다.");

            var after = LastShiftPlacementRules.Evaluate(table, probe, ignoreIndex: 0, includeImpassable: true);
            Assert.That(after.Zone, Is.EqualTo(before.Zone),
                "오버레이가 판정기의 구역 귀속을 흔들었다 — 판정기가 ResolveHull 이 아니라 Resolve 를 부른다.");
            Assert.That(after.EgressMeters, Is.EqualTo(before.EgressMeters).Within(Tolerance),
                "구역이 흔들리면서 이탈 거리까지 따라 움직였다 — RG-1(1) 이 등록 순서에 딸려간다.");
        }

        [Test]
        public void CanonicalCompartmentsStayJudgedWhileModulesAreRegistered()
        {
            // 오버레이가 켜진 채로도 정본 열한 개가 자기 판정기를 통과해야 한다.
            var table = LastShiftPlacementRules.TableOf(LastShiftCompartments.Specs);
            LastShiftPlacedModules.Register(
                LastShiftShipDimensions.ZoneMinX(LastShiftZone.Cockpit),
                LastShiftShipDimensions.ZoneMaxX(LastShiftZone.LifeSupport),
                LastShiftShipDimensions.SideWallZ + 1f, LastShiftShipDimensions.SideWallZ + 9f,
                LastShiftZone.Cooling);

            for (var index = 0; index < table.Length; index++)
            {
                var verdict = LastShiftPlacementRules.Evaluate(
                    table, table[index], ignoreIndex: index, includeImpassable: true,
                    spine: LastShiftPairSpine.AlongLength);
                Assert.That(verdict.Accepted, Is.True,
                    $"{(LastShiftCompartment)index} 가 오버레이 등록 뒤에 물렸다({verdict.Rejection}).");
            }
        }

        [Test]
        public void RegisterFromPlacementKeepsTheVerdictZone()
        {
            var table = LastShiftPlacementRules.TableOf(LastShiftCompartments.Specs);
            var verdict = LastShiftPlacementRules.Evaluate(
                table, table[0], ignoreIndex: 0, includeImpassable: true);

            var handle = LastShiftPlacedModules.Register(table[0], verdict.Zone);

            Assert.That(LastShiftPlacedModules.TryGet(handle, out var module), Is.True);
            Assert.That(module.Zone, Is.EqualTo(verdict.Zone));
            Assert.That(module.MinX, Is.EqualTo(table[0].MinX).Within(Tolerance));
            Assert.That(module.MaxZ, Is.EqualTo(table[0].MaxZ).Within(Tolerance));
        }
    }
}
