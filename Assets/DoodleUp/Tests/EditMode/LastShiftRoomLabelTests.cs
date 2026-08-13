using System;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 방 이름표 — 2026-08-13 플레이테스트("처음 하는 사람이 어느 방이 어딘지 모름")의 고침.
    ///
    /// <b>문구가 옳은지는 기계가 못 잰다.</b> 그래서 재는 것은 셋이다 — <b>여섯 방이 전부 이름을
    /// 갖는가</b>, <b>그 이름이 HUD 에서 쓰는 이름과 같은 말인가</b>, <b>이름표가 자기 방 안에
    /// 서는가</b>. 앞의 둘은 문구가 갈리는 사고를 잡고, 셋째는 이름이 이웃 방 위로 넘어가
    /// "어느 방 이름인지 모른다" 로 되돌아가는 것을 잡는다.
    /// </summary>
    public sealed class LastShiftRoomLabelTests
    {
        /// <summary>세로가 짧은 흔한 화면. 지도 자리가 여기서 가장 작아진다.</summary>
        private static readonly Vector2 Screen = new(1920f, 1080f);

        private static LastShiftPlazaSpace[] AllSpaces =>
            Enum.GetValues(typeof(LastShiftPlazaSpace)).Cast<LastShiftPlazaSpace>().ToArray();

        /// <summary>
        /// <b>여섯 방이 전부 이름을 갖는다.</b> 하나라도 비면 지도에 이름 없는 사각형이
        /// 남고, 그 사각형이 정확히 이 카드가 고치려는 그것이다.
        /// </summary>
        [Test]
        public void EveryRoomHasAName()
        {
            foreach (var space in AllSpaces)
                Assert.That(LastShiftRoomLabels.NameOf(space), Is.Not.Empty, $"{space} 이름이 비었다");

            Assert.That(LastShiftRoomLabels.ShaftName, Is.Not.Empty);
        }

        /// <summary>
        /// <b>이름이 여섯 다 다르다.</b> 광장·숙소가 조종석 구역에 속해 있어서(조항 <c>S-1</c>)
        /// 구역 이름으로 부르면 셋이 "조종석" 이 되는데, 그 상태의 지도는 같은 이름 셋을 보여
        /// 주므로 이름이 없는 것보다 나쁘다.
        /// </summary>
        [Test]
        public void RoomNamesAreDistinct()
        {
            var names = AllSpaces.Select(LastShiftRoomLabels.NameOf).ToArray();

            Assert.That(names.Distinct().Count(), Is.EqualTo(names.Length),
                $"같은 이름을 쓰는 방이 있다 — {string.Join(", ", names)}");
            Assert.That(names, Does.Not.Contain(LastShiftRoomLabels.ShaftName),
                "승강구 이름을 방이 같이 쓴다");
        }

        /// <summary>
        /// <b>기능실 넷의 이름이 HUD 구역 칸과 같은 말이다.</b> 두 화면이 같은 방을 다른 이름으로
        /// 부르면, 이름을 붙여서 없애려던 혼동이 그 자리에서 다시 생긴다 — 파생으로 얻고 있으므로
        /// 이 검사는 <b>파생을 끊고 표를 따로 적는 것</b>을 잡는다.
        /// </summary>
        [Test]
        public void FunctionRoomNamesMatchTheHudZoneLabels()
        {
            foreach (var space in new[]
                     {
                         LastShiftPlazaSpace.CockpitRoom, LastShiftPlazaSpace.LifeSupportRoom,
                         LastShiftPlazaSpace.PowerRoom, LastShiftPlazaSpace.CoolingRoom
                     })
            {
                var zone = LastShiftPlazaLayout.Of(space).Zone;
                Assert.That(LastShiftRoomLabels.NameOf(space),
                    Is.EqualTo(LastShiftZoneAtlas.ShortLabelOf(zone)),
                    $"{space} 이름이 HUD 구역 칸과 다르다");
            }
        }

        /// <summary>
        /// <b>승강구 이름이 튜토리얼 <c>3</c>단계 제목과 같은 말이다.</b> 띠에서 "중앙 승강구" 로
        /// 읽은 사람이 지도에서 그 말을 찾아야 두 화면이 이어진다.
        /// </summary>
        [Test]
        public void TheShaftNameMatchesTheTutorialTitle()
        {
            Assert.That(LastShiftRoomLabels.ShaftName,
                Is.EqualTo(LastShiftTutorialCopy.Of(LastShiftTutorialStep.CentralLift).Title),
                "승강구 이름과 튜토리얼 3단계 제목이 다른 말이다");
        }

        /// <summary>
        /// <b>이름표가 자기 방 안에 선다.</b> 이름은 방 사각형 위쪽 안쪽 띠라, 방 여섯 전부에서
        /// 그 띠가 테두리를 안 넘어야 한다 — 넘으면 그 이름이 이웃 방 것으로 읽힌다.
        /// </summary>
        [Test]
        public void TheNameBandStaysInsideItsRoom()
        {
            foreach (var room in RoomRects())
            {
                var band = LastShiftMapView.RoomNameRect(room.Rect);

                Assert.That(band.yMin, Is.GreaterThanOrEqualTo(room.Rect.yMin),
                    $"{room.Space} 이름이 방 위 테두리를 넘는다");
                Assert.That(band.yMax, Is.LessThanOrEqualTo(room.Rect.yMax),
                    $"{room.Space} 이름이 방 아래 테두리를 넘는다");
                Assert.That(band.center.x, Is.EqualTo(room.Rect.center.x).Within(0.01f),
                    $"{room.Space} 이름이 방 가운데에서 좌우로 밀려 있다");
            }
        }

        /// <summary>
        /// <b>승강구 이름이 코어 밖 아래에 선다.</b> 코어는 <c>4 x 4m</c> 라 이름 다섯 자가 안
        /// 들어가고, 안에 넣으면 코어 색 사각형과 겹쳐 둘 다 안 읽힌다. 광장 이름이 광장 위쪽
        /// 띠에 있으므로 아래로 내야 그 둘이 안 겹친다.
        /// </summary>
        [Test]
        public void TheShaftNameSitsBelowTheCoreAndInsideThePlaza()
        {
            var plan = LastShiftMapView.Schematic(Screen);
            var core = plan.ToScreenRect(
                -LastShiftPlazaLayout.CoreHalfExtent, LastShiftPlazaLayout.CoreHalfExtent,
                -LastShiftPlazaLayout.CoreHalfExtent, LastShiftPlazaLayout.CoreHalfExtent);
            var plaza = plan.ToScreenRect(
                LastShiftPlazaLayout.PlazaMinX, LastShiftPlazaLayout.PlazaMaxX,
                LastShiftPlazaLayout.PlazaMinZ, LastShiftPlazaLayout.PlazaMaxZ);

            var band = LastShiftMapView.ShaftNameRect(core);

            Assert.That(band.yMin, Is.GreaterThanOrEqualTo(core.yMax),
                "승강구 이름이 코어 사각형 위에 겹친다");
            Assert.That(band.yMax, Is.LessThanOrEqualTo(plaza.yMax),
                "승강구 이름이 광장 밖으로 나간다");
            Assert.That(band.center.x, Is.EqualTo(core.center.x).Within(0.01f),
                "승강구 이름이 코어 중심에서 밀려 있다");
            Assert.That(band.Overlaps(LastShiftMapView.RoomNameRect(plaza)), Is.False,
                "승강구 이름과 광장 이름이 겹친다");
        }

        /// <summary>
        /// <b>조작줄이 지도 키를 말한다.</b> 방 이름이 뜨는 화면이 지도 하나인데 그 키가 어디에도
        /// 안 적혀 있으면, 지도를 이미 아는 사람만 배 배치를 알 수 있다 — 그것이 이 카드의
        /// 사고 절반이었다. 유령 줄에도 있다: 지도는 유령도 열 수 있는 보기 전용 화면이다.
        /// </summary>
        [Test]
        public void TheInputBarAdvertisesTheMapKey()
        {
            var host = new GameObject("player", typeof(LastShiftPlayerController));
            try
            {
                var player = host.GetComponent<LastShiftPlayerController>();

                Assert.That(player.InputLabel, Does.Contain("M 지도"), "조작줄에 지도 키가 없다");

                player.SetGhost(true);
                Assert.That(player.InputLabel, Does.Contain("M 지도"), "유령 조작줄에 지도 키가 없다");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static (LastShiftPlazaSpace Space, Rect Rect)[] RoomRects()
        {
            var plan = LastShiftMapView.Schematic(Screen);
            return LastShiftPlazaLayout.Footprints
                .Select(footprint => (footprint.Space,
                    plan.ToScreenRect(footprint.MinX, footprint.MaxX, footprint.MinZ, footprint.MaxZ)))
                .ToArray();
        }
    }
}
