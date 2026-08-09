using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 실제로 씬에 들어가는 드레싱 에셋을 본다. <see cref="LastShiftDressingRulesTests"/> 가
    /// 검사기의 계약을 고정한다면 여기는 <b>내용</b>을 고정한다 — 씬 빌드도 같은 검사를
    /// 하지만 빌드는 분 단위라, 위반이 들어온 커밋을 테스트가 먼저 잡아야 한다.
    /// </summary>
    public sealed class LastShiftDressingSetTests
    {
        private static LastShiftDressingSet Load()
        {
            var set = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
            Assert.That(set, Is.Not.Null,
                $"{LastShiftDressingSet.AssetPath} 가 없다 — 드레싱이 통째로 빠진 씬은 자동화 " +
                "로그에서 정상 빌드와 구분이 안 된다.");
            return set;
        }

        [Test]
        public void ShippedDressingSetSatisfiesTheBriefConstraints()
        {
            var violations = LastShiftDressingRules.Validate(Load().Props);
            Assert.That(violations, Is.Empty,
                string.Join("\n", violations.Select(v => v.ToString())));
        }

        [Test]
        public void EveryCompartmentHasDressing()
        {
            // 방 하나가 통째로 비면 그레이박스에서는 "아직 안 채운 방" 과 "채웠는데 데이터가
            // 안 붙은 방" 이 똑같이 보인다.
            var props = Load().Props;
            foreach (LastShiftCompartment compartment in System.Enum.GetValues(typeof(LastShiftCompartment)))
                Assert.That(
                    props.Any(p => p.space.kind == LastShiftDressingSpaceKind.Compartment &&
                                   p.space.compartment == compartment),
                    Is.True, $"{LastShiftCompartments.NameOf(compartment)} 에 소품이 하나도 없다.");
        }

        [Test]
        public void BothStateCueRoomsAreDressed()
        {
            // 냉각실만, 또는 전력실만 단서를 가지면 둘 중 하나가 상태 없는 방으로 읽힌다.
            var props = Load().Props;
            foreach (var zone in new[] { LastShiftZone.Cooling, LastShiftZone.Power })
                Assert.That(
                    props.Any(p => p.space.kind == LastShiftDressingSpaceKind.Zone &&
                                   p.space.zone == zone &&
                                   (p.semantics & LastShiftDressingSemantics.StateResponsive) != 0),
                    Is.True, $"{zone} 에 상태 단서가 없다.");
        }

        [Test]
        public void EveryReadoutExceptionCarriesItsReason()
        {
            // 검증기도 같은 것을 보지만, 여기서 한 번 더 세는 이유는 사유가 <b>비어 있지만
            // 않으면</b> 통과하기 때문이다. 실제로 뭐라고 적혀 있는지는 사람이 읽어야 하고,
            // 그 읽을 대상이 몇 개인지가 리뷰에서 먼저 보여야 한다.
            //
            // <b>M-2 에서 이 목록이 비었다.</b> 브리프 §1.3 이 인정한 예외 넷(수경재배 식물
            // 열화 · 서버 LED · 의무실 · 구명정 발진 상태등)은 셋이 카탈로그로 가고 하나가
            // 배에서 제거되면서 전부 배를 떠났다(맵 개편 §3.2) — 그래서 "하나는 있어야 한다"
            // 를 못 건다. 대신 <b>있으면 사유가 있어야 한다</b>만 남긴다. 그게 원래 이
            // 테스트가 지키던 것이고, 예외가 다시 생길 때 그대로 작동한다.
            var exceptions = Load().Props
                .Where(p => (p.semantics & LastShiftDressingSemantics.RoomSystemReadout) != 0)
                .ToArray();

            foreach (var prop in exceptions)
                Assert.That(prop.justification, Is.Not.Empty, $"{prop.space}/{prop.id} 에 사유가 없다.");

            // 그리고 고정 구획에는 붙으면 안 된다 — 예외를 인정받은 방이 배에 하나도 없다
            // (LastShiftDressingRules.AllowsRoomSystemReadout 가 지금 언제나 false 다).
            foreach (var prop in exceptions)
                Assert.That(prop.space.kind, Is.Not.EqualTo(LastShiftDressingSpaceKind.Compartment),
                    $"{prop.space}/{prop.id} 가 고정 구획에 고유 시스템 계기를 달았다 — " +
                    "지금 그 예외를 쓸 수 있는 방은 없다.");
        }
    }
}
