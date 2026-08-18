using System.IO;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEditor;
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

        /// <summary>
        /// 방 높이의 두 정본이 같은가. 지도의 <c>ceiling</c> 이 정본이고
        /// <see cref="LastShiftPlazaLayout"/> 의 <c>Height</c> 가 그 사본인데, 사본을 아무도 안
        /// 읽는다는 이유로 갈린 채 방치된 적이 있다(코드 <c>3.0</c> / 데이터 <c>3.2</c>). 그 어긋남이
        /// 천장 조사에서 하루를 먹었다 — 안 읽히는 값이라도 갈리면 <b>읽는 사람</b>을 속인다.
        ///
        /// 대비 자체도 같이 못박는다. 부속(숙소)이 본선보다 낮은 것은 연출 규약이고(§2.2),
        /// 지도에서 한 줄 고치면 조용히 사라지는 종류의 것이다.
        /// </summary>
        [Test]
        public void MapCeilingsMatchTheLayoutCopyAndKeepTheAnnexLower()
        {
            var map = JsonUtility.FromJson<MapRoot>(File.ReadAllText(MapPath));
            Assert.That(map?.spaces, Is.Not.Null, "지도에 spaces 가 없다");

            Assert.That(map.plaza.ceiling,
                Is.EqualTo(LastShiftPlazaLayout.Of(LastShiftPlazaSpace.Plaza).Height).Within(0.001f),
                "광장 천장이 지도와 사본에서 다르다");

            foreach (var space in map.spaces)
            {
                var copy = LastShiftPlazaLayout.Of(SpaceOf(space.id)).Height;
                Assert.That(space.ceiling, Is.EqualTo(copy).Within(0.001f),
                    $"{space.id} 천장이 지도 {space.ceiling:F2} / 사본 {copy:F2} 로 갈렸다");
            }

            var quarters = System.Array.Find(map.spaces, s => s.id == "quarters");
            Assert.That(quarters, Is.Not.Null, "지도에 숙소가 없다");
            Assert.That(quarters.ceiling, Is.LessThan(map.plaza.ceiling - 0.1f),
                $"숙소 천장 {quarters.ceiling:F2} 가 본선 {map.plaza.ceiling:F2} 과 안 벌어졌다 — " +
                "문을 지날 때 천장이 내려앉는 것이 부속/본선 대비의 연출이다");
        }

        private static LastShiftPlazaSpace SpaceOf(string id) => id switch
        {
            "cockpit" => LastShiftPlazaSpace.CockpitRoom,
            "lifeSupport" => LastShiftPlazaSpace.LifeSupportRoom,
            "power" => LastShiftPlazaSpace.PowerRoom,
            "cooling" => LastShiftPlazaSpace.CoolingRoom,
            "quarters" => LastShiftPlazaSpace.Quarters,
            _ => LastShiftPlazaSpace.Plaza
        };

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

        [Test]
        public void CockpitSightlineFixturesMatchTheRadialRoom()
        {
            var map = JsonUtility.FromJson<MapRoot>(File.ReadAllText(MapPath));
            var portWindow = System.Array.Find(map.placementRules, rule => rule.id == "cockpitWindowPort");
            var starboardWindow = System.Array.Find(map.placementRules, rule => rule.id == "cockpitWindowStarboard");
            var mirror = System.Array.Find(map.placementRules, rule => rule.id == "cockpitMirror");
            var nose = System.Array.Find(map.placementRules, rule => rule.id == "noseCap");

            Assert.That(portWindow, Is.Not.Null);
            Assert.That(starboardWindow, Is.Not.Null);
            Assert.That(portWindow.position[0], Is.EqualTo(-16.2f).Within(0.01f));
            Assert.That(starboardWindow.position[0], Is.EqualTo(-16.2f).Within(0.01f));
            Assert.That(portWindow.position[2], Is.EqualTo(-2f).Within(0.01f));
            Assert.That(starboardWindow.position[2], Is.EqualTo(2f).Within(0.01f));
            Assert.That(mirror, Is.Not.Null);
            Assert.That(mirror.position[0], Is.EqualTo(-13f).Within(0.01f));
            Assert.That(mirror.position[2], Is.EqualTo(3.93f).Within(0.01f), "거울은 조종석 우현 측벽이어야 한다");
            Assert.That(nose, Is.Not.Null);
            Assert.That(nose.operation, Is.EqualTo("assembleNoseCap"));
            Assert.That(nose.position[0], Is.LessThan(-18f), "노즈 캡은 조종석 선수 외피 밖으로 돌출해야 한다");
        }

        [Test]
        public void QuartersHasASecondBunkSetOnTheEndWallClearOfTheDoorLane()
        {
            var map = JsonUtility.FromJson<MapRoot>(File.ReadAllText(MapPath));
            var bunk = System.Array.Find(map.placementRules, rule => rule.id == "quartersBunkSecond");

            Assert.That(bunk, Is.Not.Null, "숙소 4인분을 채울 두 번째 2단 침상 조가 없다.");
            Assert.That(bunk.assetId, Is.EqualTo("LPK_Quarters_Bunk"));
            Assert.That(bunk.position[0], Is.EqualTo(10.25f).Within(0.01f));
            Assert.That(bunk.position[1], Is.EqualTo(0.12f).Within(0.01f),
                "침상 밑면은 갑판 윗면에 맞아야 한다.");
            Assert.That(bunk.position[2], Is.EqualTo(11.5f).Within(0.01f),
                "침상은 문 반대편 z=MaxZ 끝벽에 0.05m 띄워 붙어야 한다.");
        }

        /// <summary>
        /// <b>침상은 눕지 않는다.</b> 이 검사가 생긴 이유는 같은 회전이 두 번 잘못 들어왔기
        /// 때문이다. <c>LPK_Quarters_Bunk</c> 의 시각 표현은 원래 Blender 에서 나온
        /// <c>LPK_Quarters_Bunk.fbx</c> 였고, 그 축을 맞추려면 로컬 X <c>-90도</c> 보정이
        /// 필요했다. PR #17(<c>d3a80e3</c>) 이 그 시각 표현을 <c>LSDress_Bunk</c> 로 갈았는데,
        /// 이쪽은 <b>Unity 안에서 Y-up 으로 직접 짜인 프리미티브 조합</b>이라 보정이 필요 없고
        /// 그래서 회전을 무회전으로 두는 것이 맞았다. 이후 <c>fa1f601</c> 이 그것을
        /// "슬롯 단위 관례" 로 오인해 <c>-90도</c> 를 되살렸다 — 문에서만 눈으로 확인하고
        /// 침상은 에셋이 바뀐 사실을 못 본 채로.
        ///
        /// 그 결과 <c>(x, y, z) -> (x, z, -y)</c> 로 프레임·매트리스·베개의 높이가 전부 0 으로
        /// 주저앉아, 침상이 바닥에 박힌 좁고 긴 판재로 보였다. 회전값만 못박으면 다음에 또
        /// "복원" 될 수 있으므로 <b>결과 형태</b>로 검사한다 — 침상은 폭이 가장 길고, 깊이가
        /// 그다음이고, 높이가 가장 낮다. 눕히면 깊이와 높이가 뒤집혀 이 순서가 깨진다.
        /// </summary>
        [Test]
        public void QuartersBunkVisualStandsUprightInsteadOfLyingOnItsSide()
        {
            const string bunkPrefabPath =
                "Assets/DoodleUp/Prefabs/LastShiftModularKit/LPK_Quarters_Bunk.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(bunkPrefabPath);
            Assert.That(prefab, Is.Not.Null, bunkPrefabPath);

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, "침상 프리팹에 렌더러가 하나도 없다.");

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            var size = bounds.size;

            Assert.That(size.x, Is.GreaterThan(size.z),
                $"침상은 폭(x)이 깊이(z)보다 길어야 한다 — 실제 {size}.");
            Assert.That(size.z, Is.GreaterThan(size.y),
                $"침상이 옆으로 누웠다: 깊이(z) {size.z:0.###} 가 높이(y) {size.y:0.###} 이하다 — " +
                "ProductionVisual 에 로컬 X -90도 보정이 다시 들어갔는지 본다. " +
                "그 보정은 FBX 시각 표현에만 필요했고 지금 쓰는 LSDress_Bunk 에는 필요 없다.");
            Assert.That(bounds.min.y, Is.GreaterThan(-0.01f),
                $"침상이 원점 아래로 파고든다 — 갑판 위에 놓이면 바닥에 박힌다. 실제 {bounds.min}.");
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
                if (bone.name == LastShiftCrewBody.HeadBoneName) head = bone;
            Assert.That(head, Is.Not.Null, $"{LastShiftCrewBody.HeadBoneName} 뼈가 없다 — 1인칭 머리 접기가 이 이름에 걸려 있다");

            // 성분마다 오차를 두고 본다. <c>Is.EqualTo(Vector3.one)</c> 는 NUnit 이
            // <see cref="Vector3.Equals(object)"/>(성분 정확비교)로 판정해서, FBX 를 다시
            // 익스포트할 때마다 붙는 <c>0.99999994</c> 급 잔차에 걸린다. 그때 나오는 메시지가
            // <c>Expected: (1.00, 1.00, 1.00) But was: (1.00, 1.00, 1.00)</c> 라 원인이 안 보인다.
            //
            // 이 테스트가 막으려는 것은 잔차가 아니라 <b>0 으로 접힌 채 저장된 머리</b>다.
            var scale = head.localScale;
            var detail = $"프리팹 머리가 ({scale.x:R}, {scale.y:R}, {scale.z:R}) 로 저장돼 있다" +
                " — 동료 화면에서도 머리가 사라진다";
            Assert.That(scale.x, Is.EqualTo(1f).Within(0.001f), detail);
            Assert.That(scale.y, Is.EqualTo(1f).Within(0.001f), detail);
            Assert.That(scale.z, Is.EqualTo(1f).Within(0.001f), detail);
        }

        [System.Serializable] private sealed class MapRoot { public MapCamera cockpitCamera; public MapPlaza plaza; public MapSpace[] spaces; public MapRule[] placementRules; }
        [System.Serializable] private sealed class MapCamera { public float[] spawn; public float[] lookAt; }
        [System.Serializable] private sealed class MapPlaza { public float ceiling; }
        [System.Serializable] private sealed class MapSpace { public string id; public float ceiling; }
        [System.Serializable] private sealed class MapRule { public string id; public string assetId; public string operation; public float[] position; }
    }
}
