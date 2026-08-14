using System.Collections.Generic;
using DoodleUp.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 전력실 배전반이 <b>방 설비 안에 들어가 있지 않은가</b>.
    ///
    /// 이 검사가 생긴 이유. 빌더가 <c>BusCabinet</c>(<c>LSDress_BusPanel</c>) 을
    /// <c>(PowerCenterX, 뒷벽+0.55)</c> 에 박아 두는데, 정본 지도가 <b>같은 뒷벽</b>에 방 설비
    /// <c>LPK_Power_Switchgear</c> 를 세운다. 구운 배에서 둘이 <c>x</c> 는 같고 <c>z</c> 는
    /// <c>8cm</c> 차이라 배전반이 통째로 설비 안에 있었다 — 조종석 콘솔에서 이미 한 번 나온
    /// 실수의 재발이다.
    ///
    /// <b>임포터의 keep-out 검사는 이걸 못 잡는다.</b>
    /// <c>LastShiftModularKitImporter.ReportDressingInsideFeatures</c> 는 <c>ZoneDressing</c>
    /// 밑만 훑는데 배전반은 배 루트의 직계라 대상 밖이고, 그래서 <c>clashes=0</c> 이 계속
    /// "깨끗하다" 로 읽혔다. 여기서는 <b>구워진 배</b>에서 두 상자를 직접 잰다.
    /// </summary>
    public sealed class LastShiftPowerBusPanelPlacementTests
    {
        private const string ShipPrefabPath = "Assets/DoodleUp/Prefabs/LastShiftShipGraybox.prefab";
        private const string BusCabinetName = "BusCabinet";
        private const string PowerFeatureName = "Power_Feature";

        [Test]
        public void BusCabinetKeepsOutOfTheRoomFeature()
        {
            var ship = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath));
            try
            {
                var cabinet = Find(ship, BusCabinetName);
                var feature = Find(ship, PowerFeatureName);

                var cabinetBox = LastShiftSceneBuilder.CombinedRendererBounds(cabinet);
                var featureBox = LastShiftSceneBuilder.CombinedRendererBounds(feature);

                // 임포터가 드레싱에 쓰는 것과 같은 여유다. 스치기만 해도 어긋난 것으로 본다.
                var grown = featureBox;
                grown.Expand(LastShiftModularKitImporter.DressingKeepOut * 2f);

                Assert.That(grown.Intersects(cabinetBox), Is.False,
                    $"{BusCabinetName} 이 {PowerFeatureName} 안에 있다 — " +
                    $"cabinet={cabinetBox.center:F2}/{cabinetBox.size:F2} " +
                    $"feature={featureBox.center:F2}/{featureBox.size:F2}. " +
                    "배전반은 우현 벽으로, 설비는 문 맞은편 뒷벽으로 갈라야 한다.");
            }
            finally
            {
                Object.DestroyImmediate(ship);
            }
        }

        private static GameObject Find(GameObject ship, string name)
        {
            var matches = new List<Transform>();
            foreach (var child in ship.GetComponentsInChildren<Transform>(true))
                if (child.name == name) matches.Add(child);

            Assert.That(matches, Has.Count.EqualTo(1),
                $"구운 배에서 {name} 을 하나 찾아야 한다 — 찾은 수 {matches.Count}. " +
                "이름이 바뀌었거나 배가 오래된 빌더로 구워졌다.");
            return matches[0].gameObject;
        }
    }
}
