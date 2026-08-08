using System.Collections.Generic;
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

        // ── 정본 열한 개가 자기 판정기를 통과한다 ────────────────────────────

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
            // 승격 대조(§5.3). 기항 개방 대상 셋의 재계산값을 판정기로 다시 읽는다 — 같은 수가
            // 나와야 사본 둘을 지운 것이 값 갈이가 아니라는 것이 이 파일 안에서도 보인다.
            var table = CanonicalTable();
            var expected = new (LastShiftCompartment Compartment, float Meters, LastShiftZone Zone)[]
            {
                (LastShiftCompartment.ServerRoom, 16.32f, LastShiftZone.Cockpit),
                (LastShiftCompartment.Hydroponics, 14.71f, LastShiftZone.LifeSupport),
                (LastShiftCompartment.MedBay, 25.44f, LastShiftZone.LifeSupport)
            };

            foreach (var (compartment, meters, expectedZone) in expected)
            {
                Assert.That(
                    LastShiftPlacementRules.TryEgress(
                        table, table[(int)compartment], out var actual, out var zone),
                    Is.True, $"{compartment} 사슬이 선체에 안 닿는다.");

                Assert.That(actual, Is.EqualTo(meters).Within(Tolerance),
                    $"{compartment} 이탈이 {actual:F2}m 다 — 승격 전 EditMode 사본이 내던 " +
                    $"{meters:F2}m 에서 움직였다(docs/rg1-recalc-voyage-port-unlock-v1.md §2.1).");
                Assert.That(zone, Is.EqualTo(expectedZone),
                    $"{compartment} 구역 귀속이 바뀌었다 — 사슬 뿌리의 선체 문이 옮겨간 것이다.");
            }
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
            var hangar = table[(int)LastShiftCompartment.Hangar];

            // 격납고 자리에 그대로 놓는다. ignoreIndex 를 안 주므로 자기 자신이 아니라 남이다.
            var verdict = LastShiftPlacementRules.Evaluate(table, hangar);

            Assert.That(verdict.Rejection.HasFlag(LastShiftPlacementRejection.OverlapsPlacement), Is.True,
                "격납고 자리에 겹쳐 놓았는데 겹침이 안 잡힌다.");
            Assert.That(verdict.OverlappingIndex, Is.EqualTo((int)LastShiftCompartment.Hangar),
                "겹친 상대를 못 짚는다 — 배치 UI 가 무엇을 강조할지 모르게 된다.");
        }

        [Test]
        public void TouchingWallsAreNotAnOverlap()
        {
            // 사슬로 이어 붙인 구획은 언제나 한 면을 공유한다. 닫힌 구간 비교를 쓰면 정본 열한 개가
            // 통째로 물린다 — 그래서 위 EveryCanonicalCompartment... 와 짝인 검사다.
            var table = CanonicalTable();
            var cargo = table[(int)LastShiftCompartment.CargoBay];
            var workshop = table[(int)LastShiftCompartment.Workshop];

            Assert.That(LastShiftPlacementRules.Overlaps(cargo, workshop), Is.False,
                "화물칸과 정비창은 문이 놓인 면 하나를 공유할 뿐인데 겹침으로 읽힌다.");
        }

        [Test]
        public void AModuleInsideTheHullIsRejectedForIntrusion()
        {
            var table = CanonicalTable();
            var inside = new LastShiftPlacement(
                -2f, 2f, -2f, 2f,
                new Vector3(-2f, 0f, 0f), (int)LastShiftCompartment.CargoBay);

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
        public void ChainDepthIsReportedButNotLegislated()
        {
            // 깊이 상한은 기획이 안 정했다(§9.4 는 "막다른 방" 만 요구했다). 그래서 판정기는
            // 기본값에서 깊이로 안 물리고, 부르는 쪽이 수를 주면 그때만 문다.
            var chain = SternChain(links: 4);
            var deepest = chain[^1];

            var unbounded = LastShiftPlacementRules.Evaluate(chain, deepest, ignoreIndex: chain.Length - 1);
            Assert.That(unbounded.Rejection.HasFlag(LastShiftPlacementRejection.ChainTooDeep), Is.False,
                "기본값이 깊이로 물었다 — 조문에 없는 수를 판정기가 만들어 낸 것이다.");

            var bounded = LastShiftPlacementRules.Evaluate(
                chain, deepest, ignoreIndex: chain.Length - 1, maxDoorDepth: 2);
            Assert.That(bounded.Rejection.HasFlag(LastShiftPlacementRejection.ChainTooDeep), Is.True,
                "깊이 상한을 줬는데 안 문다.");
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
            var moving = (int)LastShiftCompartment.MedBay;

            var inPlace = LastShiftPlacementRules.LongestPairWith(
                table, table[moving], includeImpassable: true,
                spine: LastShiftPairSpine.AlongLength, ignoreIndex: moving);
            var zone = LastShiftZoneAtlas.Resolve(
                LastShiftCompartments.Of(LastShiftCompartment.Lavatory).DoorPosition);
            var whole = LastShiftPlacementRules.LongestPairPerZone(
                table, includeImpassable: true, LastShiftPairSpine.AlongLength)[(int)zone];

            Assert.That(inPlace, Is.EqualTo(whole).Within(Tolerance),
                $"제자리에 다시 놓은 의무실의 W-1 이 {inPlace:F2}m 로 표 전체 값 {whole:F2}m 와 " +
                "다르다 — 옮기기 전 자리가 같이 세어졌다는 뜻이다.");
        }

        // ── 표본 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 산소실 끝벽에서 선미로 곧게 뻗는 사슬. 칸마다 <c>8m</c> 이므로 사슬 거리만으로
        /// 링크당 <c>8m</c> 이 쌓인다 — 구역 스파인이 얼마든 여섯 칸이면 한도를 넘는다.
        /// </summary>
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
