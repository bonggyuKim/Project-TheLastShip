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
            var exceptions = Load().Props
                .Where(p => (p.semantics & LastShiftDressingSemantics.RoomSystemReadout) != 0)
                .ToArray();

            Assert.That(exceptions, Is.Not.Empty, "고유 시스템 표시가 하나도 없다 — 브리프 §1.3 이 " +
                                                  "인정한 예외(수경재배 식물 열화, 서버 LED, 구명정 발진 상태등)가 사라졌다.");
            foreach (var prop in exceptions)
                Assert.That(prop.justification, Is.Not.Empty, $"{prop.space}/{prop.id} 에 사유가 없다.");
        }
    }
}
