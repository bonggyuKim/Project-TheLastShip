using System.Collections.Generic;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 배치 판정기(<see cref="LastShiftPlacementRules"/>)를 잰다.
    ///
    /// <b>이 파일과 RG-1 테스트 둘의 역할이 다르다.</b> <see cref="LastShiftRg1GuardrailTests"/> 와
    /// <see cref="LastShiftPlazaRg1Tests"/> 는 <b>값</b>을 지킨다 — 승격이 이탈 거리를 한 자리도
    /// 안 움직였다는 것이 그 둘의 고정값 열댓 개가 그대로 통과하는 것으로 증명된다. 여기서 지키는
    /// 것은 <b>판정</b>이다 — 겹침·선체 침범·사슬 순환·이탈 초과를 실제로 물리는가, 그리고 물리지
    /// 말아야 할 것(<c>W-1</c>)을 안 물리는가.
    ///
    /// 재는 대상이 자유 배치라 표가 아니라 <b>후보</b>다. 지금 서 있는 배에는 후보를 만드는
    /// 코드가 없으므로 여기서 직접 세운다 — <c>docs/tech/free-placement-runtime-chain-estimate-v1.md</c>
    /// §7 이 이 조각을 맨 앞에 둔 이유가 그것이다. <b>자유 배치가 하나도 안 붙은 지금 상태에서
    /// 검증되는 유일한 조각이다.</b>
    /// </summary>
    public sealed class LastShiftPlacementVerdictTests
    {
        private const float Tolerance = 0.01f;

        private static LastShiftPlacement[] CanonicalTable() =>
            LastShiftPlacementRules.TableOf(LastShiftCompartments.Specs);

        // ── 정본 고정 표가 자기 판정기를 통과한다 ────────────────────────────

        [Test]
        public void EveryCanonicalCompartmentPassesItsOwnJudge()
        {
            // 판정기가 지금 서 있는 배를 물리면 그건 판정기가 틀린 것이다. 이 테스트가 이 카드의
            // 최소 조건이고, 자유 배치가 붙기 전에 확인할 수 있는 것의 전부이기도 하다.
            var table = CanonicalTable();

            for (var index = 0; index < table.Length; index++)
            {
                var compartment = (LastShiftCompartment)index;
                var verdict = LastShiftPlacementRules.Evaluate(
                    table, table[index], ignoreIndex: index, includeImpassable: true,
                    spine: LastShiftPairSpine.AlongLength);

                Assert.That(verdict.Accepted, Is.True,
                    $"{compartment} 가 판정기에 물린다({verdict.Rejection}). 정본 좌표가 판정기를 " +
                    "통과 못 하면 판정기 쪽이 틀렸거나 구획표가 이미 위반이다.");

                Assert.That(verdict.DoorDepth, Is.EqualTo(LastShiftCompartments.DoorDepth(compartment)),
                    $"{compartment} 사슬 깊이가 정본 DoorDepth 와 다르다 — 두 자가 갈라졌다.");

                Assert.That(verdict.EgressSeconds, Is.LessThan(LastShiftPlacementRules.TraverseLimitSeconds),
                    $"{compartment} 이탈이 {verdict.EgressSeconds:F2}초다 — RG-1(1) 위반이고 " +
                    "LastShiftRg1GuardrailTests 가 먼저 걸렸어야 한다.");

                // W-1 은 언제나 이탈 이상이다 — 후보 셋 중 (나) 가 이탈값 자체이기 때문이다.
                Assert.That(verdict.LongestPairMeters, Is.GreaterThanOrEqualTo(verdict.EgressMeters - Tolerance),
                    $"{compartment} 의 W-1 이 이탈보다 짧다 — 최대 후보에서 (나) 가 빠졌다는 뜻이다.");
            }
        }

        [Test]
        public void TheJudgeReadsTheSameEgressTheGuardrailTestsPin()
        {
            // 자 하나 대조. LastShiftRg1GuardrailTests 가 래칫으로 박아 둔 값을 판정기로 다시
            // 읽는다 — 같은 수가 나와야 두 파일이 같은 자를 쓰고 있다는 것이 여기서도 보인다.
            //
            // <b>M-2 로 대상이 셋에서 하나가 됐다.</b> 예전 셋(서버/통신실·수경재배·의무실)은
            // 기항 개방 대상이었고, 그 계열이 폐지되면서(조항 K-2) 배에 남은 것은 숙소뿐이다.
            var table = CanonicalTable();

            Assert.That(
                LastShiftPlacementRules.TryEgress(
                    table, table[(int)LastShiftCompartment.Quarters], out var actual, out var zone),
                Is.True, "숙소 사슬이 선체에 안 닿는다.");

            Assert.That(actual, Is.EqualTo(19.00f).Within(Tolerance),
                $"숙소 이탈이 {actual:F2}m 다 — LastShiftRg1GuardrailTests 의 래칫 19.00m 와 " +
                "갈렸다. 두 파일 중 하나만 갱신됐다는 뜻이다.");
            Assert.That(zone, Is.EqualTo(LastShiftZone.LifeSupport),
                "숙소 구역 귀속이 바뀌었다 — 사슬 뿌리의 선체 문이 옮겨간 것이다.");
        }

        [Test]
        public void TheStraightLineSpineNeverReadsShorterThanTheAlongLengthOne()
        {
            // 자를 둘 남긴 근거. x 차는 실거리의 한 성분이므로 근사가 짧은 쪽으로만 틀린다 —
            // 그래서 z 로 벌어진 표에서 AlongLength 를 쓰면 과소평가가 되고, 자유 배치처럼
            // 어디에 놓일지 모르는 상황의 기본값은 StraightLine 이어야 한다.
            var table = CanonicalTable();
            var along = LastShiftPlacementRules.LongestPairPerZone(
                table, includeImpassable: true, LastShiftPairSpine.AlongLength);
            var straight = LastShiftPlacementRules.LongestPairPerZone(
                table, includeImpassable: true, LastShiftPairSpine.StraightLine);

            for (var zone = (LastShiftZone)0; (int)zone < LastShiftZoneAtlas.ZoneCount; zone++)
                Assert.That(straight[(int)zone], Is.GreaterThanOrEqualTo(along[(int)zone] - Tolerance),
                    $"{LastShiftZoneAtlas.ShortLabelOf(zone)} 에서 실거리 자가 x 근사보다 짧게 읽혔다 — " +
                    "그러면 두 자를 남긴 근거가 무너진다.");
        }

        // ── 물려야 하는 것 ──────────────────────────────────────────────────

        [Test]
        public void AModuleDroppedOnAnExistingOneIsRejectedForOverlap()
        {
            var table = CanonicalTable();
            var quarters = table[(int)LastShiftCompartment.Quarters];

            // 숙소 자리에 그대로 놓는다. ignoreIndex 를 안 주므로 자기 자신이 아니라 남이다.
            var verdict = LastShiftPlacementRules.Evaluate(table, quarters);

            Assert.That(verdict.Rejection.HasFlag(LastShiftPlacementRejection.OverlapsPlacement), Is.True,
                "숙소 자리에 겹쳐 놓았는데 겹침이 안 잡힌다.");
            Assert.That(verdict.OverlappingIndex, Is.EqualTo((int)LastShiftCompartment.Quarters),
                "겹친 상대를 못 짚는다 — 배치 UI 가 무엇을 강조할지 모르게 된다.");
        }

        [Test]
        public void TouchingWallsAreNotAnOverlap()
        {
            // 사슬로 이어 붙인 방은 언제나 한 면을 공유한다. 닫힌 구간 비교를 쓰면 사슬 전체가
            // 통째로 물린다 — 그래서 위 EveryCanonicalCompartment... 와 짝인 검사다.
            //
            // <b>표본을 표에서 못 뽑는다.</b> M-2 로 고정 표가 하나가 되면서 "면을 공유하는
            // 두 방" 이 시작 배에 없어졌다. 그 짝은 이제 언제나 배치가 만들므로 여기서도
            // 배치처럼 세운다 — 숙소 좌현 면에 붙는 칸과 그 칸에 다시 붙는 칸이다.
            var quarters = LastShiftCompartments.Of(LastShiftCompartment.Quarters);
            var first = new LastShiftPlacement(
                quarters.MinX, quarters.MinX + 3f, quarters.MinZ - 3f, quarters.MinZ,
                new Vector3(quarters.MinX + 1.5f, 0f, quarters.MinZ),
                (int)LastShiftCompartment.Quarters);
            var second = new LastShiftPlacement(
                quarters.MinX, quarters.MinX + 3f, quarters.MinZ - 6f, quarters.MinZ - 3f,
                new Vector3(quarters.MinX + 1.5f, 0f, quarters.MinZ - 3f),
                LastShiftCompartments.FixedCount);

            Assert.That(LastShiftPlacementRules.Overlaps(first, second), Is.False,
                "두 칸은 문이 놓인 면 하나를 공유할 뿐인데 겹침으로 읽힌다.");
        }

        [Test]
        public void AModuleInsideTheHullIsRejectedForIntrusion()
        {
            var table = CanonicalTable();
            var inside = new LastShiftPlacement(
                -2f, 2f, -2f, 2f,
                new Vector3(-2f, 0f, 0f), (int)LastShiftCompartment.Quarters);

            var verdict = LastShiftPlacementRules.Evaluate(table, inside);

            Assert.That(verdict.Rejection.HasFlag(LastShiftPlacementRejection.OverlapsHullInterior), Is.True,
                "선체 한가운데 놓은 모듈이 안 물린다 — 방·통로가 이미 타일링한 자리다.");
        }

        [Test]
        public void ACyclicParentChainIsRejectedInsteadOfLoopingForever()
        {
            // §9.5 가 아니라고 답한 대안 경로가 순환으로 실수로 만들어지는 자리다. 판정기가
            // 이것을 못 잡으면 자유 배치는 첫 오배치에서 멈춰 선다.
            var loop = new[]
            {
                new LastShiftPlacement(0f, 4f, 20f, 24f, new Vector3(0f, 0f, 22f), 1),
                new LastShiftPlacement(4f, 8f, 20f, 24f, new Vector3(4f, 0f, 22f), 0)
            };

            var verdict = LastShiftPlacementRules.Evaluate(loop, loop[0], ignoreIndex: 0);

            Assert.That(verdict.Rejection.HasFlag(LastShiftPlacementRejection.ChainBroken), Is.True,
                "부모 사슬이 서로를 가리키는데 사슬 판정이 통과한다.");
            Assert.That(verdict.DoorDepth, Is.EqualTo(-1),
                "끊긴 사슬의 깊이는 -1 이어야 한다 — 정본 DoorDepth 와 같은 규약이다.");
        }

        [Test]
        public void AParentOutsideTheTableIsRejectedInsteadOfThrowing()
        {
            var table = CanonicalTable();
            var orphan = new LastShiftPlacement(
                40f, 44f, -2f, 2f, new Vector3(40f, 0f, 0f), table.Length + 3);

            var verdict = LastShiftPlacementRules.Evaluate(table, orphan);

            Assert.That(verdict.Rejection.HasFlag(LastShiftPlacementRejection.ChainBroken), Is.True,
                "표 밖을 가리키는 부모가 예외가 아니라 판정으로 돌아와야 한다 — 커서를 움직이는 " +
                "중에 던지면 배치 UI 가 죽는다.");
        }

        [Test]
        public void ALongEnoughChainTripsTheEgressGuardrail()
        {
            // <b>이것이 판정기가 있어야 하는 이유다.</b> 승격 전에는 이 판정이 Editor 전용
            // 어셈블리 안에만 있어서, 런타임이 사슬을 아무리 길게 이어도 아무것도 안 봤다.
            var chain = SternChain(links: 6);

            var first = LastShiftPlacementRules.Evaluate(chain, chain[0], ignoreIndex: 0);
            Assert.That(first.Accepted, Is.True,
                $"선체 직결 한 칸이 물린다({first.Rejection}) — 사슬 길이와 무관한 다른 사유가 섞였다.");
            Assert.That(first.DoorDepth, Is.EqualTo(1));

            var last = LastShiftPlacementRules.Evaluate(chain, chain[^1], ignoreIndex: chain.Length - 1);
            Assert.That(last.DoorDepth, Is.EqualTo(chain.Length),
                "사슬 깊이가 링크 수만큼 안 세어진다.");
            Assert.That(last.Rejection.HasFlag(LastShiftPlacementRejection.EgressOverLimit), Is.True,
                $"여섯 칸 사슬 끝의 이탈이 {last.EgressSeconds:F2}초인데 안 물린다 — " +
                $"한도는 {LastShiftPlacementRules.TraverseLimitSeconds}초다(RG-1(1)).");
            Assert.That(last.EgressSeconds,
                Is.GreaterThanOrEqualTo(LastShiftPlacementRules.TraverseLimitSeconds));

            Debug.Log($"[LAST_SHIFT_PLACEMENT] chainLinks={chain.Length} " +
                      $"depth={last.DoorDepth} egress={last.EgressMeters:F2}m {last.EgressSeconds:F2}s " +
                      $"w1={last.LongestPairMeters:F2}m zone={last.Zone} result={last.Rejection}");
        }

        [Test]
        public void TheDefaultDepthCapIsTheDecidedNumber()
        {
            // 깊이 상한은 이제 조문에 있다 — docs/free-placement-chain-depth-cap-v1.md §3.
            // 기본값이 안 물면 배치 UI 는 상한이 없는 것과 같아지므로, 여기서 기본값 자체를 건다.
            var atCap = CoolingSpur(links: LastShiftPlacementRules.MaxDoorDepth);
            var justUnder = LastShiftPlacementRules.Evaluate(
                atCap, atCap[^1], ignoreIndex: atCap.Length - 1);

            Assert.That(justUnder.DoorDepth, Is.EqualTo(LastShiftPlacementRules.MaxDoorDepth));
            Assert.That(justUnder.Rejection.HasFlag(LastShiftPlacementRejection.ChainTooDeep), Is.False,
                $"상한과 같은 깊이가 물린다({justUnder.Rejection}) — 상한은 초과할 때만 무는 값이다.");

            var overCap = CoolingSpur(links: LastShiftPlacementRules.MaxDoorDepth + 1);
            var tooDeep = LastShiftPlacementRules.Evaluate(
                overCap, overCap[^1], ignoreIndex: overCap.Length - 1);

            Assert.That(tooDeep.Rejection.HasFlag(LastShiftPlacementRejection.ChainTooDeep), Is.True,
                $"상한을 한 칸 넘겼는데 기본값이 안 문다(depth={tooDeep.DoorDepth}).");

            // 깊이를 빼고 다른 사유만 보고 싶은 도구는 여전히 있다 — 그 자리가 남아 있어야 한다.
            var unbounded = LastShiftPlacementRules.Evaluate(
                overCap, overCap[^1], ignoreIndex: overCap.Length - 1,
                maxDoorDepth: LastShiftPlacementRules.UnboundedDoorDepth);
            Assert.That(unbounded.Rejection.HasFlag(LastShiftPlacementRejection.ChainTooDeep), Is.False,
                "UnboundedDoorDepth 를 명시적으로 줬는데 깊이로 물었다.");
        }

        [Test]
        public void TheDepthCapBitesWhereTheEgressGuardrailDoesNot()
        {
            // <b>이것이 깊이 상한이 따로 있어야 하는 이유 전부다.</b> RG-1(1) 이 실제로 물리는
            // 것은 스파인이 긴 구역(조종석·산소실, 14m)뿐이다. 냉각실은 스파인이 2.5m 라
            // 보행 예산이 남아돌고, 작은 방을 이으면 열몇 칸을 이어도 이탈 판정을 통과한다 —
            // 그 배는 "막다른 방" 이 아니라 복도 한 줄이다(docs/free-placement-chain-depth-cap-v1.md §2).
            var spur = CoolingSpur(links: LastShiftPlacementRules.MaxDoorDepth + 1);
            var verdict = LastShiftPlacementRules.Evaluate(spur, spur[^1], ignoreIndex: spur.Length - 1);

            Assert.That(verdict.Zone, Is.EqualTo(LastShiftZone.Cooling),
                "표본이 잘못됐다 — 냉각실에 안 붙었으면 스파인이 짧다는 전제가 성립 안 한다.");
            Assert.That(verdict.EgressSeconds,
                Is.LessThan(LastShiftPlacementRules.TraverseLimitSeconds),
                $"이탈이 {verdict.EgressSeconds:F2}초로 이미 물린다 — 그러면 이 사슬은 깊이 상한이 " +
                "없어도 막혔을 것이고, 이 테스트가 아무것도 안 가른다.");
            Assert.That(verdict.Rejection, Is.EqualTo(LastShiftPlacementRejection.ChainTooDeep),
                $"깊이 말고 다른 사유가 섞였다({verdict.Rejection}) — 두 자가 겹치면 이 표본이 " +
                "증명하려는 것이 흐려진다.");

            Debug.Log($"[LAST_SHIFT_PLACEMENT] depthCap={LastShiftPlacementRules.MaxDoorDepth} " +
                      $"depth={verdict.DoorDepth} egress={verdict.EgressMeters:F2}m " +
                      $"{verdict.EgressSeconds:F2}s zone={verdict.Zone} result={verdict.Rejection}");
        }

        [Test]
        public void CanonicalDepthLeavesTheWholeCapToThePlayer()
        {
            // 상한 6 은 "시작 배 최대 깊이 4 + 2" 로 뽑은 수였다(§3). <b>M-2 가 그 입력을
            // 4 에서 1 로 내렸다</b> — 선수·선미 사슬이 통째로 빠지고 숙소가 선체에 직결하면서
            // 시작 배에 사슬이라는 것이 없어졌다.
            //
            // <b>그래서 상한을 안 내린다.</b> 유도식이 "시작 깊이 + 확장 여지" 인데 시작 깊이가
            // 1 이 됐으므로 여지가 2 에서 5 로 늘었고, 그 여지를 쓰는 것이 이제 플레이어다.
            // 상한을 1+2=3 으로 좁히면 자유 배치가 세 칸에서 막힌다 — 맵 개편 §3.4 가
            // "뿌리 자유면이 늘어 깊은 사슬을 만들 이유가 줄어든다" 고 적은 것은 상한이
            // 덜 걸린다는 뜻이지 상한을 좁혀도 된다는 뜻이 아니다.
            var deepest = LastShiftCompartments.Specs.Max(
                spec => LastShiftCompartments.DoorDepth(spec.Compartment));

            Assert.That(deepest, Is.EqualTo(1),
                "시작 배 최대 깊이가 1 이 아니다 — 고정 표에 사슬이 다시 생겼다.");
            Assert.That(LastShiftPlacementRules.MaxDoorDepth - deepest, Is.GreaterThanOrEqualTo(2),
                $"시작 배 최대 깊이가 {deepest} 인데 상한이 {LastShiftPlacementRules.MaxDoorDepth} 다 — " +
                "가장 깊은 사슬 끝에 두 칸을 못 붙이면 확장 자유도가 시작 상태로 봉인된다.");
        }

        // ── 물리지 말아야 하는 것 ────────────────────────────────────────────

        [Test]
        public void TheLongestWalkIsReportedWithoutBeingAJudgement()
        {
            // 측정법 v1.1 §2.5. W-1 은 한도가 없고 래칫만 있다 — 판정기가 이걸 물면 조문에 없는
            // 제약이 코드에서 생긴다.
            var chain = SternChain(links: 2);
            var verdict = LastShiftPlacementRules.Evaluate(chain, chain[^1], ignoreIndex: chain.Length - 1);

            Assert.That(verdict.Accepted, Is.True,
                $"두 칸 사슬이 물린다({verdict.Rejection}).");
            Assert.That(verdict.LongestPairMeters, Is.GreaterThan(0f),
                "판정은 통과했는데 W-1 이 안 재졌다 — 신호로 쓸 값이 없다.");
            Assert.That(verdict.LongestPairMeters, Is.GreaterThanOrEqualTo(verdict.EgressMeters - Tolerance));
        }

        [Test]
        public void ZoneAttributionFollowsTheChainRootNotTheModulesOwnPosition()
        {
            // 자유 배치 확장 검토 조항 F-1. 모듈이 어느 구역 오버레이에 등록되는지는 <b>사슬
            // 뿌리가 선체에 내는 문</b>이 정한다 — 모듈 자신이 어디 떠 있는지가 아니다. 둘을
            // 섞으면 문을 닫아도 격리가 안 되는 배가 나온다(타당성 검토 §11-1).
            var bowRoot = new LastShiftPlacement(
                -LastShiftShipDimensions.HalfLength - 6f, -LastShiftShipDimensions.HalfLength,
                -2f, 2f,
                new Vector3(-LastShiftShipDimensions.HalfLength, 0f, 0f), -1);

            // 뿌리에 붙되 몸통은 선미 쪽으로 멀리 나간 모듈. 자기 x 로 읽으면 산소실이다.
            var sternBody = new LastShiftPlacement(
                LastShiftShipDimensions.HalfLength + 4f, LastShiftShipDimensions.HalfLength + 8f,
                20f, 24f,
                new Vector3(-LastShiftShipDimensions.HalfLength - 3f, 0f, 2f), 0);

            var table = new[] { bowRoot, sternBody };
            var verdict = LastShiftPlacementRules.Evaluate(table, sternBody, ignoreIndex: 1);

            var byOwnPosition = LastShiftZoneAtlas.Resolve(
                new Vector3(LastShiftShipDimensions.HalfLength + 6f, 0f, 22f));

            Assert.That(byOwnPosition, Is.EqualTo(LastShiftZone.LifeSupport),
                "표본이 잘못됐다 — 몸통이 산소실 x 에 안 놓였으면 이 테스트가 아무것도 안 가른다.");
            Assert.That(verdict.Zone, Is.EqualTo(LastShiftZone.Cockpit),
                "구역 귀속이 사슬 뿌리가 아니라 모듈 자기 좌표를 따랐다 — 조항 F-1 위반이다.");
            Assert.That(verdict.HullDoor.x,
                Is.EqualTo(-LastShiftShipDimensions.HalfLength).Within(Tolerance));
        }

        [Test]
        public void MovingAModuleDoesNotPairItWithThePlaceItLeft()
        {
            // ignoreIndex 를 안 빼면 옮기기 전 자리와 옮긴 자리가 둘 다 세어져 W-1 이 부풀어
            // 오른다 — 커서를 끄는 내내 값이 틀린다.
            var table = CanonicalTable();
            var moving = (int)LastShiftCompartment.Quarters;

            var inPlace = LastShiftPlacementRules.LongestPairWith(
                table, table[moving], includeImpassable: true,
                spine: LastShiftPairSpine.AlongLength, ignoreIndex: moving);
            var zone = LastShiftZoneAtlas.Resolve(
                LastShiftCompartments.Of(LastShiftCompartment.Quarters).DoorPosition);
            var whole = LastShiftPlacementRules.LongestPairPerZone(
                table, includeImpassable: true, LastShiftPairSpine.AlongLength)[(int)zone];

            Assert.That(inPlace, Is.EqualTo(whole).Within(Tolerance),
                $"제자리에 다시 놓은 숙소의 W-1 이 {inPlace:F2}m 로 표 전체 값 {whole:F2}m 와 " +
                "다르다 — 옮기기 전 자리가 같이 세어졌다는 뜻이다.");
        }

        // ── 표본 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 산소실 끝벽에서 선미로 곧게 뻗는 사슬. 칸마다 <c>8m</c> 이므로 사슬 거리만으로
        /// 링크당 <c>8m</c> 이 쌓인다 — 구역 스파인이 얼마든 여섯 칸이면 한도를 넘는다.
        /// </summary>
        /// <summary>
        /// 냉각실 우현 벽에서 바깥으로 뻗는 <c>2m</c> 짜리 방의 사슬. <b>스파인이 짧은 구역을
        /// 고른 것이 이 표본의 전부다</b> — 냉각실은 <c>x ∈ [0, 5]</c> 라 선체 문에서 구역
        /// 끝까지가 <c>2.5m</c> 고, 보행 예산 <c>36.8m</c> 대부분이 사슬에 남는다.
        /// <see cref="SternChain"/>(<c>8m</c> 방, 산소실 밖)은 반대로 이탈이 먼저 물린다.
        /// </summary>
        private static LastShiftPlacement[] CoolingSpur(int links)
        {
            const float roomDepth = 2f;
            var doorX = LastShiftShipDimensions.ZoneCenterX(LastShiftZone.Cooling);

            var spur = new List<LastShiftPlacement>(links);
            for (var link = 0; link < links; link++)
            {
                var minZ = LastShiftShipDimensions.SideWallZ + link * roomDepth;
                spur.Add(new LastShiftPlacement(
                    doorX - 2f, doorX + 2f, minZ, minZ + roomDepth,
                    new Vector3(doorX, 0f, minZ), link - 1));
            }

            return spur.ToArray();
        }

        private static LastShiftPlacement[] SternChain(int links)
        {
            var chain = new List<LastShiftPlacement>(links);
            for (var link = 0; link < links; link++)
            {
                var minX = LastShiftShipDimensions.HalfLength + link * 8f;
                chain.Add(new LastShiftPlacement(
                    minX, minX + 8f, -2f, 2f,
                    new Vector3(minX, 0f, 0f), link - 1));
            }

            return chain.ToArray();
        }
    }
}
