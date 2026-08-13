using System;
using System.Collections.Generic;
using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 지도 아이콘 — 2026-08-13 사용자 지시("지도에는 이름보다 각 방에 맞는 아이콘")의 고침.
    ///
    /// <b>그림이 예쁜지는 기계가 못 잰다.</b> 여기서 재는 것은 셋이다 — <b>격자가 조각으로 정확히
    /// 접히는가</b>(모든 채운 셀이 한 번씩 덮이고 겹치지 않는가), <b>조각이 자기 상자·자기 방 안에
    /// 서는가</b>, <b>일곱 실루엣이 서로 다른가</b>. 첫째는 접는 방법이 그림을 바꾸는 사고를 잡고,
    /// 둘째는 아이콘이 이웃 방으로 넘치는 것을 잡고, 셋째는 두 방이 같은 그림을 쓰는 것을 잡는다.
    ///
    /// 산소실/냉각실이 실제로 눈에 갈리는가는 <b>여기서 못 잰다</b> — 사용자 플레이테스트 몫이다
    /// (<c>docs/onboarding-map-icons-and-waypoint-v1.md</c> §9-1).
    /// </summary>
    public sealed class LastShiftRoomIconTests
    {
        private static readonly Vector2 Screen = new(1920f, 1080f);

        /// <summary>격자 한 칸이 정확히 1 이 되는 상자. 조각을 셀 단위 정수로 되읽는다.</summary>
        private static readonly Rect UnitBox = new(0f, 0f, LastShiftRoomIcons.GridSize,
            LastShiftRoomIcons.GridSize);

        private static LastShiftPlazaSpace[] AllSpaces =>
            Enum.GetValues(typeof(LastShiftPlazaSpace)).Cast<LastShiftPlazaSpace>().ToArray();

        /// <summary>
        /// <b>접은 조각이 격자를 그대로 다시 그린다.</b> 조각으로 펴는 것은 최적화이고, 최적화가
        /// 그림을 바꾸면 문서의 격자와 화면의 아이콘이 갈린다 — 그 뒤로는 어느 쪽이 정본인지
        /// 아무도 모른다. 채운 셀은 정확히 한 번, 빈 셀은 한 번도 안 덮여야 한다.
        /// </summary>
        [Test]
        public void FoldingRedrawsTheGridExactly()
        {
            foreach (var space in AllSpaces)
                AssertCoversTheMask($"{space}", (column, row) => LastShiftRoomIcons.Filled(space, column, row),
                    into => LastShiftRoomIcons.Bands(space, UnitBox, into));

            AssertCoversTheMask("승강구", LastShiftRoomIcons.ShaftFilled,
                into => LastShiftRoomIcons.ShaftBands(UnitBox, into));
        }

        /// <summary>
        /// <b>아이콘 하나가 <see cref="LastShiftRoomIcons.MaxBands"/> 안에 접힌다.</b> 부르는 쪽은
        /// 그 크기 배열 하나를 프레임마다 돌려 쓰므로, 넘치면 조각이 조용히 잘려 아이콘 일부가
        /// 사라진다. 일곱을 합친 수도 같이 재둔다 — 지도 한 장의 조각 예산이 그 수다.
        /// </summary>
        [Test]
        public void EveryIconFoldsInsideTheScratch()
        {
            var roomy = new Rect[64];
            var total = 0;

            foreach (var space in AllSpaces)
            {
                var bands = LastShiftRoomIcons.Bands(space, UnitBox, roomy);
                Assert.That(bands, Is.LessThanOrEqualTo(LastShiftRoomIcons.MaxBands),
                    $"{space} 아이콘이 조각 상한을 넘는다 — {bands}");
                total += bands;
            }

            total += LastShiftRoomIcons.ShaftBands(UnitBox, roomy);

            Assert.That(total, Is.LessThanOrEqualTo(40),
                $"일곱 아이콘 조각이 프레임 예산을 넘는다 — {total}");
        }

        /// <summary>
        /// <b>모자란 배열에 억지로 안 쑤셔 넣는다.</b> 잘릴 때 앞쪽 조각만 남고 멈춰야, 넘친
        /// 상태에서도 배열 밖을 안 밟는다.
        /// </summary>
        [Test]
        public void FoldingStopsWhenTheScratchRunsOut()
        {
            var tiny = new Rect[2];

            Assert.That(LastShiftRoomIcons.ShaftBands(UnitBox, tiny), Is.EqualTo(2),
                "모자란 배열에 조각을 두 개보다 많이 적었다");
        }

        /// <summary>
        /// <b>일곱 실루엣이 서로 다르다.</b> 두 방이 같은 그림을 쓰면 아이콘이 방을 가르는 일을
        /// 못 하고, 그건 이름 셋이 전부 "조종석" 이던 것과 같은 종류의 사고다.
        /// </summary>
        [Test]
        public void IconSilhouettesAreDistinct()
        {
            var shapes = AllSpaces.Select(space => Signature(
                    (column, row) => LastShiftRoomIcons.Filled(space, column, row)))
                .Append(Signature(LastShiftRoomIcons.ShaftFilled))
                .ToArray();

            Assert.That(shapes.Distinct().Count(), Is.EqualTo(shapes.Length),
                "같은 격자를 쓰는 아이콘이 있다");
        }

        /// <summary>
        /// <b>머리표 두 줄이 방 여섯 전부에 들어간다.</b> 지금 배율에서 아이콘이 떨어지는 방이
        /// 하나라도 생기면 지도가 방마다 다른 줄 수로 읽히므로, 그 순간을 여기서 잡는다.
        /// </summary>
        [Test]
        public void EveryRoomFitsTheTwoLineHeader()
        {
            foreach (var room in RoomRects())
                Assert.That(LastShiftMapView.FitsIcon(room.Rect), Is.True,
                    $"{room.Space} 에 아이콘이 안 들어간다 — 지도 배율이 줄었다");
        }

        /// <summary>
        /// <b>아이콘 조각이 자기 방 안에 선다.</b> 넘치면 이웃 방 위에 그림이 얹혀 그 아이콘이
        /// 어느 방 것인지 모르게 되고, 그건 이름표가 넘칠 때와 같은 되돌아감이다.
        /// 이름 줄과도 안 겹쳐야 두 줄이 두 줄로 읽힌다.
        /// </summary>
        [Test]
        public void IconBandsStayInsideTheirRoomAndAboveTheName()
        {
            var scratch = new Rect[LastShiftRoomIcons.MaxBands];

            foreach (var room in RoomRects())
            {
                var box = LastShiftMapView.RoomIconRect(room.Rect);
                var name = LastShiftMapView.RoomNameRect(room.Rect);

                Assert.That(box.center.x, Is.EqualTo(room.Rect.center.x).Within(0.01f),
                    $"{room.Space} 아이콘이 방 가운데에서 좌우로 밀려 있다");
                Assert.That(box.yMax, Is.LessThanOrEqualTo(name.yMin + 0.01f),
                    $"{room.Space} 아이콘이 이름 줄을 덮는다");
                Assert.That(name.yMax, Is.LessThanOrEqualTo(room.Rect.yMax),
                    $"{room.Space} 머리표가 방 아래 테두리를 넘는다");

                var bands = LastShiftRoomIcons.Bands(room.Space, box, scratch);
                Assert.That(bands, Is.GreaterThan(0), $"{room.Space} 아이콘이 비었다");

                for (var band = 0; band < bands; band++)
                    Assert.That(box.Contains(scratch[band].min)
                                && scratch[band].xMax <= box.xMax + 0.01f
                                && scratch[band].yMax <= box.yMax + 0.01f, Is.True,
                        $"{room.Space} 아이콘 조각 {band} 가 상자 밖으로 나갔다");
            }
        }

        /// <summary>
        /// <b>좁아지면 아이콘이 사라지고 이름이 그 자리를 넘겨받는다.</b> 이름만 남는 갈래를 안
        /// 재면, 아이콘을 떨군 방에서 이름이 <c>18</c>px 아래에 그대로 떠서 아래 테두리를 넘는
        /// 상태를 아무도 못 본다.
        /// </summary>
        [Test]
        public void APinchedRoomDropsTheIconAndLiftsTheName()
        {
            var pinched = new Rect(0f, 0f, 200f, LastShiftMapView.RoomNameLine + 2f);

            Assert.That(LastShiftMapView.FitsIcon(pinched), Is.False,
                "이름 한 줄이 겨우 드는 방에 아이콘까지 들어간다고 답했다");
            Assert.That(LastShiftMapView.RoomNameRect(pinched).yMin,
                Is.EqualTo(pinched.yMin + LastShiftMapView.RoomOutline + LastShiftMapView.LabelPadding)
                    .Within(0.01f),
                "아이콘을 떨궜는데 이름이 아이콘 자리만큼 내려가 있다");
        }

        /// <summary>
        /// <b>승강구 아이콘이 코어 안에 선다.</b> 이름은 코어가 좁아 밖으로 냈지만
        /// (<see cref="LastShiftMapView.ShaftNameRect"/>) 아이콘은 안이라, 코어를 넘으면 광장
        /// 바닥 위에 사다리가 떠 있는 것으로 읽힌다. 크기는 방 아이콘과 같아야 한다.
        /// </summary>
        [Test]
        public void TheShaftIconSitsInsideTheCore()
        {
            var plan = LastShiftMapView.Schematic(Screen);
            var core = plan.ToScreenRect(
                -LastShiftPlazaLayout.CoreHalfExtent, LastShiftPlazaLayout.CoreHalfExtent,
                -LastShiftPlazaLayout.CoreHalfExtent, LastShiftPlazaLayout.CoreHalfExtent);

            var box = LastShiftMapView.ShaftIconRect(core);

            Assert.That(box.width, Is.EqualTo(LastShiftMapView.RoomIconBox).Within(0.01f),
                "승강구 아이콘 크기가 방 아이콘과 다르다");
            Assert.That(box.center.x, Is.EqualTo(core.center.x).Within(0.01f),
                "승강구 아이콘이 코어 한가운데에서 좌우로 밀려 있다");
            Assert.That(box.center.y, Is.EqualTo(core.center.y).Within(0.01f),
                "승강구 아이콘이 코어 한가운데에서 위아래로 밀려 있다");
            Assert.That(box.xMin, Is.GreaterThanOrEqualTo(core.xMin), "승강구 아이콘이 코어 왼쪽을 넘는다");
            Assert.That(box.xMax, Is.LessThanOrEqualTo(core.xMax), "승강구 아이콘이 코어 오른쪽을 넘는다");
            Assert.That(box.yMin, Is.GreaterThanOrEqualTo(core.yMin), "승강구 아이콘이 코어 위를 넘는다");
            Assert.That(box.yMax, Is.LessThanOrEqualTo(core.yMax), "승강구 아이콘이 코어 아래를 넘는다");
            Assert.That(box.Overlaps(LastShiftMapView.ShaftNameRect(core)), Is.False,
                "승강구 아이콘과 이름이 겹친다");
        }

        private static void AssertCoversTheMask(string label, Func<int, int, bool> filled,
            Func<Rect[], int> fold)
        {
            var scratch = new Rect[LastShiftRoomIcons.GridSize * LastShiftRoomIcons.GridSize];
            var bands = fold(scratch);
            var hits = new int[LastShiftRoomIcons.GridSize, LastShiftRoomIcons.GridSize];

            for (var band = 0; band < bands; band++)
            {
                var rect = scratch[band];
                Assert.That(rect.width, Is.GreaterThan(0f), $"{label} 조각 {band} 의 폭이 0 이다");
                Assert.That(rect.height, Is.GreaterThan(0f), $"{label} 조각 {band} 의 높이가 0 이다");

                for (var row = Mathf.RoundToInt(rect.yMin); row < Mathf.RoundToInt(rect.yMax); row++)
                for (var column = Mathf.RoundToInt(rect.xMin); column < Mathf.RoundToInt(rect.xMax); column++)
                    hits[column, row]++;
            }

            for (var row = 0; row < LastShiftRoomIcons.GridSize; row++)
            for (var column = 0; column < LastShiftRoomIcons.GridSize; column++)
                Assert.That(hits[column, row], Is.EqualTo(filled(column, row) ? 1 : 0),
                    $"{label} 격자 ({column},{row}) 를 {hits[column, row]} 번 덮었다");
        }

        private static string Signature(Func<int, int, bool> filled)
        {
            var cells = new List<char>();
            for (var row = 0; row < LastShiftRoomIcons.GridSize; row++)
            for (var column = 0; column < LastShiftRoomIcons.GridSize; column++)
                cells.Add(filled(column, row) ? '#' : '.');
            return new string(cells.ToArray());
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
