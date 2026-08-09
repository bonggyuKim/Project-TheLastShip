using System.Linq;
using DoodleUp.Editor;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 저장된 씬의 부품 정위치가 좌표 정본과 <b>같은 구역</b>에 있는지 못박는다.
    ///
    /// <b>왜 PlayMode 가 아니라 여기인가.</b> 이 성질이 깨졌을 때 실제로 터진 것은 CT-05
    /// 산소 PlayMode 다섯 건이었고, 실패 문구는 "승무원이 파공 구역 안에 서 있어야 한다" 였다 —
    /// 승무원은 제자리에 있었고 <b>파공 구역이 옮겨 간 것</b>이 원인이라, 증상만 보고는 산소
    /// 코드를 뒤지게 된다. 실제로 이 계열은 그렇게 세 번 재발했다. 원인 자리에서 잡으면
    /// 방 배치를 옮긴 커밋이 그 자리에서 빨개진다.
    ///
    /// <b>씬을 열어 읽는다.</b> 상수끼리 비교하면 코드 안에서만 도는 검사가 되어, 정확히
    /// 이번에 일어난 일("코드는 옮겼고 씬은 안 구웠다")을 못 잡는다.
    /// </summary>
    public sealed class LastShiftSceneItemPlacementTests
    {
        /// <summary>부품이 있어야 할 구역. 이게 곧 게임 규칙이다 — 봉합판이 산소실에 있어야
        /// 파공 수리가 "산소실까지 간다" 가 되고, <see cref="LastShiftSandboxController.BreachZone"/>
        /// 도 그 자리에서 나온다.</summary>
        private static readonly (LastShiftItemRole Role, LastShiftZone Zone)[] Expected =
        {
            (LastShiftItemRole.Battery, LastShiftZone.Power),
            (LastShiftItemRole.CoolingCanister, LastShiftZone.Cooling),
            (LastShiftItemRole.PatchPlate, LastShiftZone.LifeSupport),
            (LastShiftItemRole.Tether, LastShiftZone.Cockpit)
        };

        [Test]
        public void CanonicalItemPositionsLandInTheirOwnZone()
        {
            foreach (var (role, zone) in Expected)
            {
                var nominal = LastShiftSceneBuilder.NominalPositionOf(role);
                Assert.That(LastShiftZoneAtlas.Resolve(nominal), Is.EqualTo(zone),
                    $"{role} 의 정위치 정본 {nominal} 이 {zone} 밖이다.");
            }
        }

        [Test]
        public void SavedSceneItemsMatchCanonicalPositions()
        {
            var scene = EditorSceneManager.OpenScene(LastShiftNetworkSceneBuilder.ScenePath, OpenSceneMode.Single);
            var items = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LastShiftGrabbable>(true))
                .ToArray();
            Assert.That(items.Select(item => item.Role), Is.EquivalentTo(Expected.Select(entry => entry.Role)),
                "씬 부품 구성이 부품표와 다르다.");

            foreach (var item in items)
            {
                var nominal = LastShiftSceneBuilder.NominalPositionOf(item.Role);
                // 좌표와 직렬화된 정위치를 둘 다 본다. 실행 중에는 Awake 가 transform 에서
                // 정위치를 다시 잡고, 에디터에서는 직렬화된 값이 그대로 읽히기 때문이다.
                Assert.That(item.transform.position, Is.EqualTo(nominal).Using(Vector3EqualityComparer.Instance),
                    $"{item.Role} 의 씬 좌표가 정본과 다르다 — 방 배치를 옮기고 씬을 안 다시 맞췄다. " +
                    "Last Shift/SP-02A/Realign Scene Items 로 되맞춘다.");
                Assert.That(item.NominalPosition, Is.EqualTo(nominal).Using(Vector3EqualityComparer.Instance),
                    $"{item.Role} 의 직렬화된 정위치가 정본과 다르다.");
            }
        }

        /// <summary>
        /// 파공 구역이 산소실로 풀리는지를 <see cref="LastShiftSandboxController.BreachZone"/> 과
        /// 같은 식으로 확인한다. 위 두 검사가 통과해도 이 파생이 어긋나면 CT-05 가 깨지므로,
        /// 실제로 읽히는 값 자체를 한 줄 더 못박는다.
        /// </summary>
        [Test]
        public void BreachZoneResolvesToLifeSupport()
        {
            var patch = LastShiftSceneBuilder.NominalPositionOf(LastShiftItemRole.PatchPlate);
            Assert.That(LastShiftZoneAtlas.Resolve(patch), Is.EqualTo(LastShiftZone.LifeSupport),
                "봉합판이 산소실 밖이면 파공 구역이 조종석으로 풀린다 — CT-05 사망 경로가 통째로 무너진다.");
        }

        /// <summary>부동소수 비교. 씬 직렬화가 좌표를 소수 자리에서 잘라도 통과해야 한다.</summary>
        private sealed class Vector3EqualityComparer : System.Collections.IEqualityComparer
        {
            public static readonly Vector3EqualityComparer Instance = new();

            bool System.Collections.IEqualityComparer.Equals(object x, object y) =>
                x is Vector3 a && y is Vector3 b && (a - b).sqrMagnitude <= 1e-6f;

            int System.Collections.IEqualityComparer.GetHashCode(object obj) => obj.GetHashCode();
        }
    }
}
