using System;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 이중 구획표(자유 배치 축 B). <b>여기서 지키는 것은 "표가 늘어나도 정본이 안 흔들린다" 다.</b>
    ///
    /// 정본 좌표를 지키는 것은 <see cref="LastShiftCompartmentLayoutTests"/> 고, 배치를 물리는 자를
    /// 지키는 것은 <see cref="LastShiftPlacementVerdictTests"/> 다. 이 파일이 답하는 물음은 그 둘
    /// 사이에 있다 — <c>[0, FixedCount)</c> 를 enum 영역으로 못 박고 그 위를 여는 것이
    /// <c>Of(enum)</c> 을 부르는 자리와 <c>Specs</c> 를 훑는 자리에 무엇을 하는가.
    ///
    /// 근거 문서는 <c>docs/tech/free-placement-compartment-table-v1.md</c> 이고, 그 설계의
    /// 출처는 <c>free-placement-runtime-chain-estimate-v1.md</c> §3 이다.
    /// </summary>
    public sealed class LastShiftCompartmentTableTests
    {
        private const float Tolerance = 0.01f;

        [TearDown]
        public void ClearModules() => LastShiftCompartments.ClearModules();

        // ── 고정 영역 ───────────────────────────────────────────────────────

        [Test]
        public void TheFixedRegionIsExactlyTheEnum()
        {
            // 정적 생성자 검사를 FixedCount 로 좁혔으므로 그 검사가 실제로 무엇을 거는지
            // 여기서 다시 건다 — 좁힌 검사가 아무것도 안 걸면 좁힌 것이 아니라 지운 것이다.
            var values = Enum.GetValues(typeof(LastShiftCompartment)).Cast<LastShiftCompartment>().ToArray();

            Assert.That(LastShiftCompartments.FixedCount, Is.EqualTo(values.Length),
                "enum 값이 늘었는데 FixedCount 가 안 따라왔다.");
            Assert.That(LastShiftCompartments.FixedSpecs.Length,
                Is.EqualTo(LastShiftCompartments.FixedCount));

            foreach (var value in values)
            {
                var spec = LastShiftCompartments.Of(value);
                Assert.That(spec.Index, Is.EqualTo((int)value),
                    $"{value} 의 Index 가 enum 값과 다르다 — ParentIndex 가 가리키는 자리가 어긋난다.");
                Assert.That(spec.IsFixed, Is.True, $"{value} 가 고정 구획으로 안 읽힌다.");
            }
        }

        [Test]
        public void AnEmptyTableIsTheFixedTable()
        {
            Assert.That(LastShiftCompartments.ModuleCount, Is.Zero);
            Assert.That(LastShiftCompartments.Count, Is.EqualTo(LastShiftCompartments.FixedCount));
            Assert.That(LastShiftCompartments.Specs,
                Is.SameAs(LastShiftCompartments.FixedSpecs),
                "모듈이 없는데 표를 새로 잡았다 — 자유 배치가 안 붙은 배는 예전과 같은 배열을 봐야 한다.");
        }

        // ── append 영역 ─────────────────────────────────────────────────────

        [Test]
        public void ARegisteredModuleLandsPastTheEnumRegion()
        {
            var index = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));

            Assert.That(index, Is.EqualTo(LastShiftCompartments.FixedCount),
                "첫 모듈이 enum 영역 바로 뒤가 아닌 자리에 들어갔다.");
            Assert.That(LastShiftCompartments.Count, Is.EqualTo(LastShiftCompartments.FixedCount + 1));
            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(1));

            var spec = LastShiftCompartments.At(index);
            Assert.That(spec.IsFixed, Is.False, "모듈이 고정 구획으로 읽힌다.");
            Assert.That(spec.Index, Is.EqualTo(index));
        }

        [Test]
        public void OfStillAnswersTheEnumAndRefusesTheAppendRegion()
        {
            // <b>이 표를 이중으로 만든 이유 전부가 이 한 줄이다.</b> Of(enum) 을 부르는 자리
            // 서른일곱과 그 값을 리터럴로 물고 있는 넷이 표가 늘어도 안 바뀐다.
            var before = LastShiftCompartments.Of(LastShiftCompartment.Hangar);
            Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));

            var after = LastShiftCompartments.Of(LastShiftCompartment.Hangar);
            Assert.That(after.MinX, Is.EqualTo(before.MinX).Within(Tolerance));
            Assert.That(after.Index, Is.EqualTo(before.Index));
            Assert.That(LastShiftCompartments.FixedSpecs.Length,
                Is.EqualTo(LastShiftCompartments.FixedCount),
                "모듈이 고정 표에 섞였다 — 그러면 enum 영역이라는 말 자체가 깨진다.");

            // append 영역 인덱스를 enum 으로 캐스팅해 넣는 것은 물음 자체가 틀린 것이라
            // 조용히 답이 나오면 안 된다.
            Assert.That(() => LastShiftCompartments.Of((LastShiftCompartment)LastShiftCompartments.FixedCount),
                Throws.TypeOf<IndexOutOfRangeException>(),
                "모듈 인덱스를 Of 에 넣었는데 안 터진다 — 조용히 관측실로 읽히면 좌표가 통째로 틀린다.");
        }

        [Test]
        public void ModulesAreNotAllCalledEscapePod()
        {
            // NameOf 의 default 가 "Compartment_EscapePod" 였다. 그대로 뒀으면 배치한 모듈
            // 열 개가 전부 구명정 이름을 달고 씬에 서고, 이름으로 찾는 검증이 통째로 무너진다.
            var index = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            var spec = LastShiftCompartments.At(index);

            Assert.That(LastShiftCompartments.NameOf(spec), Is.EqualTo($"Compartment_Module_{index}"));
            Assert.That(LastShiftCompartments.NameOf(spec),
                Is.Not.EqualTo(LastShiftCompartments.NameOf(LastShiftCompartment.EscapePod)));
            Assert.That(LastShiftCompartments.NameOf(LastShiftCompartments.Of(LastShiftCompartment.EscapePod)),
                Is.EqualTo("Compartment_EscapePod"),
                "고정 구획 이름이 같이 바뀌었다 — 씬 오브젝트 이름을 전제하는 검증이 통째로 깨진다.");
        }

        [Test]
        public void TheChainReachesTheHullThroughTheAppendRegion()
        {
            var root = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            var leaf = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 1, parentIndex: root));

            Assert.That(LastShiftCompartments.DoorDepth(root), Is.EqualTo(1),
                "선체 직결 모듈의 깊이가 1 이 아니다.");
            Assert.That(LastShiftCompartments.DoorDepth(leaf), Is.EqualTo(2),
                "모듈에 붙은 모듈의 깊이가 안 세어진다 — 사슬이 append 영역을 못 넘는다.");
        }

        [Test]
        public void AModuleOnAFixedCompartmentIsMeasuredFromTheHull()
        {
            // 모듈의 부모가 고정 구획일 때가 자유 배치의 기본형이다. 깊이가 부모 + 1 이
            // 아니면 사슬이 두 영역 경계에서 끊긴 것이다.
            var lounge = (int)LastShiftCompartment.Lounge;
            var index = Register(SternSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: lounge));

            Assert.That(LastShiftCompartments.DoorDepth(index),
                Is.EqualTo(LastShiftCompartments.DoorDepth(LastShiftCompartment.Lounge) + 1));
        }

        // ── 판정기를 건너뛰는 등록 경로가 없다 ───────────────────────────────

        [Test]
        public void ARejectedCandidateNeverEntersTheTable()
        {
            // 겹치거나 사슬이 끊긴 방이 표에 들어가면 그 뒤의 모든 이탈·최장 쌍 계산이 그
            // 방을 진짜로 걸어갈 수 있는 것으로 센다. 그래서 등록은 판정을 통과해야만 한다.
            var hangar = LastShiftCompartments.Of(LastShiftCompartment.Hangar);
            var onTopOfHangar = new LastShiftCompartmentSpec(
                LastShiftCompartments.NextModuleIndex,
                hangar.MinX, hangar.MaxX, hangar.MinZ, hangar.MaxZ,
                hangar.DoorPlane, hangar.DoorPlaneCoordinate, hangar.DoorCenter,
                hangar.ParentIndex, LastShiftCompartmentAccess.Open);

            var overlayBefore = LastShiftPlacedModules.ActiveCount;

            Assert.That(
                LastShiftCompartments.TryRegister(onTopOfHangar, out var index, out var verdict),
                Is.False, "격납고 자리에 겹쳐 놓았는데 표에 들어갔다.");
            Assert.That(verdict.Rejection.HasFlag(LastShiftPlacementRejection.OverlapsPlacement), Is.True);
            Assert.That(index, Is.EqualTo(-1));
            Assert.That(LastShiftCompartments.ModuleCount, Is.Zero);
            Assert.That(LastShiftPlacedModules.ActiveCount, Is.EqualTo(overlayBefore),
                "물린 후보가 구역 오버레이에는 등록됐다 — 표에 없는 방이 압력을 갖는다.");
        }

        [Test]
        public void JudgingDoesNotRegister()
        {
            // 배치 커서는 매 프레임 재기만 한다. 재는 것이 등록이면 커서를 끄는 것만으로
            // 표가 부풀어 오른다.
            var candidate = CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1);

            Assert.That(LastShiftCompartments.Judge(candidate).Accepted, Is.True);
            Assert.That(LastShiftCompartments.ModuleCount, Is.Zero);
        }

        [Test]
        public void ACandidateBuiltForAnotherSlotIsRefusedLoudly()
        {
            // 후보 제원의 Index 가 실제로 들어갈 자리와 다르면 자기 자신을 부모로 가리키는
            // 사슬이나 남의 자리를 덮는 이름이 조용히 생긴다.
            Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));

            var stale = CoolingSpur(LastShiftCompartments.FixedCount, link: 1, parentIndex: -1);
            Assert.That(
                () => LastShiftCompartments.TryRegister(stale, out _, out _),
                Throws.ArgumentException);
        }

        // ── 표와 구역 오버레이가 같이 움직인다 ───────────────────────────────

        [Test]
        public void RegisteringCoversTheFootprintWithTheChainRootZone()
        {
            // 조항 F-1. 표에만 넣고 오버레이를 안 걸면 발자국은 있는데 압력이 선체 밴드에서
            // 나오는 방이 생긴다 — 문을 닫아도 격리가 안 되는 배가 그것이다(타당성 §11-1).
            var index = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            var spec = LastShiftCompartments.At(index);
            var inside = new Vector3(spec.MinX + 0.5f, 0.5f, spec.CenterZ);

            Assert.That(LastShiftZoneAtlas.ResolveHull(inside), Is.Not.EqualTo(LastShiftZone.Cooling),
                "표본이 잘못됐다 — 이 점이 선체 밴드만으로도 냉각실이면 오버레이가 걸렸는지 안 갈린다.");
            Assert.That(LastShiftZoneAtlas.Resolve(inside), Is.EqualTo(LastShiftZone.Cooling),
                "등록한 모듈 안이 사슬 뿌리 구역으로 안 읽힌다 — 표와 오버레이가 따로 논다.");
        }

        [Test]
        public void RemovingAModuleGivesTheFootprintBackToTheHull()
        {
            var index = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            var spec = LastShiftCompartments.At(index);
            var inside = new Vector3(spec.MinX + 0.5f, 0.5f, spec.CenterZ);
            var byHull = LastShiftZoneAtlas.ResolveHull(inside);

            Assert.That(LastShiftCompartments.TryRemove(index), Is.True);
            Assert.That(LastShiftCompartments.ModuleCount, Is.Zero);
            Assert.That(LastShiftCompartments.Specs, Is.SameAs(LastShiftCompartments.FixedSpecs),
                "마지막 모듈을 뺐는데 길이만 같은 사본이 남았다 — 그 뒤로 고정 표를 두 벌 드는 셈이다.");
            Assert.That(LastShiftZoneAtlas.Resolve(inside), Is.EqualTo(byHull),
                "표에서 뺐는데 구역 오버레이가 그 자리를 계속 덮고 있다.");
        }

        [Test]
        public void ClearingModulesReleasesEveryOverlayHandle()
        {
            var root = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 1, parentIndex: root));
            Assume.That(LastShiftPlacedModules.ActiveCount, Is.EqualTo(2));

            LastShiftCompartments.ClearModules();

            Assert.That(LastShiftCompartments.Count, Is.EqualTo(LastShiftCompartments.FixedCount));
            Assert.That(LastShiftPlacedModules.ActiveCount, Is.Zero,
                "표는 비웠는데 오버레이에 모듈이 남았다 — 도메인 리로드를 끈 에디터에서 지난 판의 " +
                "모듈이 다음 판의 진공 판정에 그대로 남는다.");
        }

        // ── 해제 ────────────────────────────────────────────────────────────

        [Test]
        public void AModuleWithAChildCannotBeRemoved()
        {
            // 부모를 먼저 빼면 자식이 표 밖을 가리키거나(사슬 끊김) 당겨진 엉뚱한 부모에
            // 붙는다. 잎부터 빼는 것은 부르는 쪽 몫이고, 그 규약을 여기서 건다.
            var root = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            var leaf = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 1, parentIndex: root));

            Assert.That(LastShiftCompartments.TryRemove(root), Is.False,
                "자식이 달린 모듈이 빠졌다.");
            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(2));

            Assert.That(LastShiftCompartments.TryRemove(leaf), Is.True);
            Assert.That(LastShiftCompartments.TryRemove(root), Is.True,
                "잎을 뺀 뒤에도 부모가 안 빠진다.");
        }

        [Test]
        public void FixedCompartmentsCannotBeRemoved()
        {
            Assert.That(LastShiftCompartments.TryRemove((int)LastShiftCompartment.Hangar), Is.False,
                "고정 구획이 빠졌다 — enum 값은 남고 표만 짧아진 배가 된다.");
            Assert.That(LastShiftCompartments.Count, Is.EqualTo(LastShiftCompartments.FixedCount));
        }

        [Test]
        public void RemovingAModulePullsTheLaterOnesAndTheirParents()
        {
            // 빈 칸(무덤)을 안 남기는 대가가 이것이다 — 인덱스가 당겨지므로 뒤 칸의 부모도
            // 같이 당겨져야 사슬이 그대로 산다. 안 당기면 뒤 모듈이 자기 부모 대신 그
            // 앞 칸에 붙는다.
            var lounge = (int)LastShiftCompartment.Lounge;
            var doomed = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            var keptRoot = Register(SternSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: lounge));
            var keptLeaf = Register(SternSpur(LastShiftCompartments.NextModuleIndex, link: 1, parentIndex: keptRoot));

            Assume.That(LastShiftCompartments.DoorDepth(keptLeaf),
                Is.EqualTo(LastShiftCompartments.DoorDepth(LastShiftCompartment.Lounge) + 2));

            Assert.That(LastShiftCompartments.TryRemove(doomed), Is.True);

            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(2));
            Assert.That(LastShiftCompartments.At(keptRoot - 1).ParentIndex, Is.EqualTo(lounge),
                "고정 구획을 가리키던 부모가 같이 당겨졌다 — 고정 영역은 안 움직인다.");
            Assert.That(LastShiftCompartments.At(keptLeaf - 1).ParentIndex, Is.EqualTo(keptRoot - 1),
                "뒤 모듈의 부모가 안 당겨졌다 — 사슬이 앞 칸으로 잘못 붙었다.");
            Assert.That(LastShiftCompartments.At(keptLeaf - 1).Index, Is.EqualTo(keptLeaf - 1),
                "당겨진 칸의 Index 가 자기 자리와 다르다.");
            Assert.That(LastShiftCompartments.DoorDepth(keptLeaf - 1),
                Is.EqualTo(LastShiftCompartments.DoorDepth(LastShiftCompartment.Lounge) + 2),
                "당겨진 뒤 사슬 깊이가 달라졌다.");
        }

        [Test]
        public void EveryMutationMovesTheRevision()
        {
            // Specs 참조를 들고 있는 쪽(판정기 입력·씬 조립기)이 자기 사본이 낡았는지 묻는
            // 유일한 자리다. 안 오르면 등록·해제 뒤에도 옛 배열을 계속 옳다고 본다.
            var start = LastShiftCompartments.Revision;

            var index = Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            Assert.That(LastShiftCompartments.Revision, Is.EqualTo(start + 1));

            LastShiftCompartments.TryRemove(index);
            Assert.That(LastShiftCompartments.Revision, Is.EqualTo(start + 2));

            Register(CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1));
            LastShiftCompartments.ClearModules();
            Assert.That(LastShiftCompartments.Revision, Is.EqualTo(start + 4));
        }

        // ── 고정만 훑는 자리가 안 흔들린다 ───────────────────────────────────

        [Test]
        public void SweepsThatMustStayFixedDoNotSeeModules()
        {
            // §3.2 가 축 B 작업량의 실체라고 한 부분이다 — foreach 로 도는 자리마다 "고정만"
            // 인지 "전부" 인지를 정해야 한다. 선체 골조·문틀은 배와 함께 태어난 것만 본다.
            var candidate = CoolingSpur(LastShiftCompartments.NextModuleIndex, link: 0, parentIndex: -1);
            var doorwaysBefore = LastShiftDoorways.All.Length;
            var framesBefore = LastShiftHullFrames.IsFree(candidate.CenterX, candidate.CenterZ);

            var spec = LastShiftCompartments.At(Register(candidate));

            Assert.That(LastShiftDoorways.All.Length, Is.EqualTo(doorwaysBefore),
                "문틀 표가 모듈을 먹었다 — 이 표는 정적 생성자가 한 번 짓는 것이라 그럴 수 없다.");
            Assert.That(LastShiftDoorways.All.Any(d => d.Name == LastShiftCompartments.NameOf(spec)),
                Is.False);
            Assert.That(LastShiftHullFrames.IsFree(spec.CenterX, spec.CenterZ), Is.EqualTo(framesBefore),
                "모듈을 등록하니 골조 자리 판정이 바뀌었다 — 골조는 씬을 세울 때 이미 구워졌으므로 " +
                "답만 바뀌고 씬은 안 바뀐다.");
        }

        // ── 표본 ────────────────────────────────────────────────────────────

        private static int Register(in LastShiftCompartmentSpec candidate)
        {
            Assert.That(LastShiftCompartments.TryRegister(candidate, out var index, out var verdict),
                Is.True, $"표본이 판정기에 물린다({verdict.Rejection}) — 테스트가 재려는 것과 무관한 사유다.");
            return index;
        }

        /// <summary>
        /// 냉각실 우현 벽에 문을 내고 선수 쪽으로 길게 눕는 칸. 정본 구획 열한 개 중 어느
        /// 것도 이 <c>z</c> 대역의 이 <c>x</c> 구간에 없다(서버실은 <c>x ≈ -15</c>, 수경재배는
        /// <c>x ≈ +11</c> 이다).
        /// </summary>
        private static LastShiftCompartmentSpec CoolingSpur(int index, int link, int parentIndex)
        {
            const float roomDepth = 2f;
            var doorX = LastShiftShipDimensions.ZoneCenterX(LastShiftZone.Cooling);
            var minZ = LastShiftShipDimensions.SideWallZ + link * roomDepth;

            // 선수 쪽으로 길게 눕혀 둔 것이 이 표본의 요지다 — 몸통이 자기 사슬 뿌리와 다른
            // x 밴드에 걸쳐야 구역 오버레이가 걸렸는지가 갈린다(조항 F-1).
            return new LastShiftCompartmentSpec(
                index, doorX - 9f, doorX + 1f, minZ, minZ + roomDepth,
                LastShiftDoorPlane.AlongZ, minZ, doorX,
                parentIndex, LastShiftCompartmentAccess.Open);
        }

        /// <summary>
        /// 라운지 좌현 면에서 바깥으로 뻗는 칸. <paramref name="link"/> 는 그 방향으로 몇 칸째다.
        /// 구명정(선미로 더 나간 자리)과 의무실(우현)을 둘 다 피하려고 좌현으로 뺐다.
        /// </summary>
        private static LastShiftCompartmentSpec SternSpur(int index, int link, int parentIndex)
        {
            const float roomDepth = 3f;
            var lounge = LastShiftCompartments.Of(LastShiftCompartment.Lounge);
            var maxZ = lounge.MinZ - link * roomDepth;

            return new LastShiftCompartmentSpec(
                index, lounge.MinX, lounge.MinX + 3f, maxZ - roomDepth, maxZ,
                LastShiftDoorPlane.AlongZ, maxZ, lounge.MinX + 1.5f,
                parentIndex, LastShiftCompartmentAccess.Open);
        }
    }
}
