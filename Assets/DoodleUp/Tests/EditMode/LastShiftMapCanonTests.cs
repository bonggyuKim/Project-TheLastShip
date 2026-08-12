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
        public void MapSpawnStandsInsideTheQuartersFootprint()
        {
            // 값이 같기만 하고 방 밖이면 소용없다. 두 정본을 맞출 때 어느 쪽으로 맞출지
            // 판단이 필요한데, 그 판단의 하한이 이것이다.
            // <b>깨어나는 방은 숙소다.</b> 온보딩 1단계가 "기상(숙소)" 인데 스폰이 조종석에
            // 있었다 — 암전이 걷히면 이미 조종석에 서 있었다(사용자 지적 2026-08-12).
            var footprint = LastShiftPlazaLayout.Of(LastShiftPlazaSpace.Quarters);
            var spawn = LastShiftShipDimensions.SpawnPoint;
            Assert.That(spawn.x, Is.GreaterThan(footprint.MinX).And.LessThan(footprint.MaxX),
                "스폰이 숙소 발자국 밖이다");
            Assert.That(spawn.z, Is.GreaterThan(footprint.MinZ).And.LessThan(footprint.MaxZ),
                "스폰이 숙소 발자국 밖이다");

            // 문까지 걸을 거리가 남아야 첫 이동(AI_W_06)과 문 사거리(AI_W_07)가 생긴다.
            var door = new Vector3(footprint.MinX + LastShiftShipDimensions.QuartersDoorInset,
                spawn.y, footprint.MinZ);
            Assert.That(Vector3.Distance(spawn, door), Is.GreaterThan(2f),
                "문 바로 앞에서 깨면 걸어갈 거리가 없다");
        }

        /// <summary>
        /// <b>남의 화면에서는 머리가 보인다.</b> 1인칭에서 자기 머리를 접는 것은 소유자
        /// 인스턴스에만 거는 표현이고, 뼈 스케일은 복제되지 않는다. 그 전제가 성립하려면
        /// 프리팹이 <b>펴진 머리</b>로 저장돼 있어야 한다 — 접힌 채 저장되면 모든 클라이언트가
        /// 머리 없는 동료를 보게 되고, 소유자 쪽 코드로는 그 사실이 드러나지 않는다.
        /// </summary>
        [Test]
        public void PlayerPrefabShipsWithTheHeadUnfolded()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/DoodleUp/Prefabs/LastShiftNetworkPlayer.prefab");
            Assert.That(prefab, Is.Not.Null);

            Transform head = null;
            foreach (var bone in prefab.GetComponentsInChildren<Transform>(true))
                if (bone.name == "head") head = bone;
            Assert.That(head, Is.Not.Null, "head 뼈가 없다 — 1인칭 머리 접기가 이 이름에 걸려 있다");
            Assert.That(head.localScale, Is.EqualTo(Vector3.one),
                $"프리팹 머리가 {head.localScale} 로 저장돼 있다 — 동료 화면에서도 머리가 사라진다");
        }

        [System.Serializable] private sealed class MapRoot { public MapCamera cockpitCamera; }
        [System.Serializable] private sealed class MapCamera { public float[] spawn; public float[] lookAt; }
    }
}
