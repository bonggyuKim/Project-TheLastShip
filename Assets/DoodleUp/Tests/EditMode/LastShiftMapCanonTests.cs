using System.IO;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 정본 지도(<c>LastShiftModularMap.json</c>)가 <b>코드 좌표 정본과 어긋나지 않는가</b>.
    ///
    /// 이 검사가 생긴 이유. 스폰 자리를 두 곳이 들고 있었다 —
    /// <see cref="LastShiftShipDimensions.SpawnPoint"/> 가 <c>-8.6</c>, 지도의
    /// <c>cockpitCamera.spawn</c> 이 <c>-7.6</c> 이었다. 런타임은 지도 쪽을 쓰고
    /// (<c>LastShiftNetworkSession</c> 이 <c>LastShiftMapSpawnPose</c> 가 있으면 그것을 먼저
    /// 본다) 접속 승인 응답은 코드 쪽을 보내서, 같은 파일 안에서도 값이 갈렸다.
    /// PlayMode 검사 둘이 <c>-7.746</c> 로 실패하고서야 드러났다.
    ///
    /// 두 정본이 있으면 언제든 다시 갈린다. 여기서 <b>같아야 한다</b>고 못박는다.
    /// </summary>
    public sealed class LastShiftMapCanonTests
    {
        private const string MapPath = "Assets/DoodleUp/Data/LastShiftModularMap.json";

        [Test]
        public void MapSpawnMatchesTheDimensionCanon()
        {
            Assert.That(File.Exists(MapPath), Is.True, MapPath);
            var map = JsonUtility.FromJson<MapRoot>(File.ReadAllText(MapPath));
            Assert.That(map?.cockpitCamera?.spawn, Is.Not.Null, "지도에 cockpitCamera.spawn 이 없다");
            Assert.That(map.cockpitCamera.spawn.Length, Is.EqualTo(3));

            var mapSpawn = new Vector3(
                map.cockpitCamera.spawn[0], map.cockpitCamera.spawn[1], map.cockpitCamera.spawn[2]);
            Assert.That(Vector3.Distance(mapSpawn, LastShiftShipDimensions.SpawnPoint), Is.LessThan(0.01f),
                $"지도 스폰 {mapSpawn} 이 좌표 정본 {LastShiftShipDimensions.SpawnPoint} 과 다르다 — " +
                "런타임은 지도를 쓰고 검사는 정본을 보므로 이 차이가 그대로 실패가 된다.");
        }

        [Test]
        public void MapSpawnStandsInsideTheCockpitFootprint()
        {
            // 값이 같기만 하고 방 밖이면 소용없다. 두 정본을 맞출 때 어느 쪽으로 맞출지
            // 판단이 필요한데, 그 판단의 하한이 이것이다.
            var footprint = LastShiftPlazaLayout.Of(LastShiftPlazaSpace.CockpitRoom);
            var spawn = LastShiftShipDimensions.SpawnPoint;
            Assert.That(spawn.x, Is.GreaterThan(footprint.MinX).And.LessThan(footprint.MaxX),
                "스폰이 조종석 발자국 밖이다");
            Assert.That(spawn.z, Is.GreaterThan(footprint.MinZ).And.LessThan(footprint.MaxZ),
                "스폰이 조종석 발자국 밖이다");
        }

        [System.Serializable] private sealed class MapRoot { public MapCamera cockpitCamera; }
        [System.Serializable] private sealed class MapCamera { public float[] spawn; public float[] lookAt; }
    }
}
